# Design

## Context

- **Starting point.**
  - `pipeline-spine` gave the description and validated pipeline, with `Noise` as the only stage, and value noise.
  - `engine-preview` gave `GenerationRun`, the timeline of copied `Frame`s, and the preview window running a built-in pipeline of two independent noise stages.
  - A stage today knows nothing of any other.
  - See proposal.md for why inputs come next and what is out of scope.
- **Rules that shape this change:**
  - `illegal-states R1` to `R3`, `R6`, `R8`, `R10`: a closed stage set, a pure-data description, one validation that checks cross-stage invariants, typed ids, and absence as a case;
  - `functional-core R2`, `R4` (a technique MUST NOT mutate an input), `R6`, `R8`, `R11`, `R12`;
  - `railway R2`, `R4`, `R6`, `R11`: findings are closed data, reported in one pass, with no catch-all case;
  - `testing R3` (every technique has a golden), `R5`, `R6`, `R8`, `R9` (the inputs-unchanged property), `R15`.
- **Owner decisions taken while proposing (2026-11-15):**
  - **Naming:** a stage names its input with a **typed handle** that a builder hands out, not with a stage-id string.
  - **Several inputs:** the model allows several **named slots** per technique. Threshold has one.
  - **Where declared:** input slots go **inside the technique's parameters record**, beside its numbers.
  - **Kind:** no input kinds in this change. Any stage can feed any slot, and the wrong-kind finding moves to change 4.
  - **Unchanged inputs:** enforced **at compile time by an immutable type**, with a property checked after every step and an Engine scenario as guards.
  - **Technique:** threshold to black and white, one frame, and a golden made from the value-noise golden.

## Goals / Non-Goals

**Goals:**

- A description in which a stage names an earlier stage in a way the compiler checks, and that is still pure data.
- Validation that reports each wrong reference as a closed finding, without reporting a source's own findings twice.
- A technique that can't write to its input, because nothing it is given has a writer.
- One technique with an input, proven by a golden, properties and Engine scenarios, and visible in the preview.

**Non-Goals:**

- No generic input list, no input kinds, no size checks between inputs (proposal, Out of scope).
- No definition loading. The builder is a Core value that tests and `BuiltInPipeline` use now, and a definition will use later.
- No change to stage seeds. A stage's seed still depends only on the run seed and its id, not on its inputs.
- No combinators over outcomes (`railway R10`).

## Layer mapping

| Piece | Layer | Notes |
| --- | --- | --- |
| `PipelineBuilder`, `StageHandle` | Core | The builder is mutable while a description is built and doesn't escape the definition that uses it. It needs the comment `functional-core R11` asks for. |
| `StageDescription(Id)`, `ThresholdDescription` | Core | Pure data (`illegal-states R2`). A handle is a Core record. |
| `Threshold`, `ThresholdParameters`, `ThresholdLevel` | Core | Built only by validation (`illegal-states R3`, `R8`) |
| `Input` enum; `MissingInput`, `UnknownInput`, `SelfInput`, `LaterInput`; `Parameter.Level` | Core | Closed findings, carrying data (`railway R4`) |
| `Image`, `Frame.ToImage()` | Core | Immutable kept pixels |
| `StageOutputs`, `Generation.Run(stage, outputs, random)` | Core | Core picks a stage's inputs from the outputs (`functional-core R2`) |
| `TerrainThreshold.Run` | Core | The technique: an iterator (`functional-core R4`) |
| Keeping each finished stage's last image, and passing the outputs | Engine | Bookkeeping, not a decision: the engine keeps every output and Core chooses |
| `TimelineEntry.Image` | Engine | The timeline keeps `Image`s |
| `BuiltInPipeline`, `FindingText`, `FrameTexture` | Desktop | Composition, text for a person (`railway R5`), and upload |
| Ports, adapters | None | Nothing here is platform |

## Decisions

### D1. A builder hands out handles; a handle names a position and an id

```csharp
// Illustrative; names are not final.
public sealed record class StageHandle
{
    internal StageHandle(int index, string id) { Index = index; Id = id; }
    public int Index { get; }   // position in the description the builder builds
    public string Id { get; }   // the id the stage was added with, as written ("" for null)
}

/// Mutable while a description is built (functional-core R11): it collects stages in order and
/// gives each a handle. It doesn't escape the code that builds the description.
public sealed class PipelineBuilder
{
    public StageHandle Add(StageDescription stage);    // appends; returns the stage's handle
    public PipelineDescription Build();                 // an immutable snapshot of what was added
}

var pipeline = new PipelineBuilder();
var height = pipeline.Add(new NoiseDescription("height", 128, 128, 5, 32, 0.5));
pipeline.Add(new ThresholdDescription("terrain", Source: height, Level: 128));
var description = pipeline.Build();
```

- **A handle has an internal constructor,** so the only way to get one is `Add`. `Add` returns it after the stage is appended, so a stage can't hold a handle to itself or to a stage after it.
- **A handle is a value, not a token.**
  - It records the position and the given id. Value equality keeps descriptions comparable and printable, which the validation spec's determinism requirement relies on.
  - A reference-identity token, such as a builder object or a GUID, would break that equality, and so the description would no longer be plain data.
- **`PipelineDescription` keeps its public constructor.** Tests build descriptions that a builder can't, such as a stage that reads a later stage through a handle from another builder, because validation must report those (D3). A definition will use the builder.
- **`Build` can be called more than once,** and each call snapshots what has been added so far. The builder holds no other state.
- **Alternatives rejected:**
  - A stage-id string: plain, but a typo becomes a finding instead of a compile error (owner decision).
  - A handle holding only the position: a handle from another builder would silently name whatever sits at that position.
  - An immutable builder, `(PipelineDescription, StageHandle) Add(...)`: no mutation, but every definition threads a tuple through each line. A builder that never escapes is the same trade `ImmutableArray.Builder` makes.

### D2. The id moves to the base `StageDescription`

`closed record class StageDescription(string Id)`, so `NoiseDescription(string Id, …) : StageDescription(Id)`. `PipelineBuilder.Add` and the input checks (D3) read a stage's id without a switch. This mirrors what `engine-preview` D1 did for `Stage`.

### D3. Inputs are slots in the parameters; validation resolves each one

```csharp
// Illustrative.
public sealed record class ThresholdDescription(string Id, StageHandle Source, int Level) : StageDescription(Id);

public sealed record class Threshold(StageId Id, ThresholdParameters Parameters) : Stage(Id);
public sealed record class ThresholdParameters(StageId Source, ThresholdLevel Level);   // internal ctor, Create for tests
public sealed record class ThresholdLevel { public const int Min = 0, Max = 255; … }

public enum Input { Source }     // names a slot in a finding, the way Parameter names a parameter

public sealed record class MissingInput(StageRef Stage, Input Input) : Finding;
public sealed record class UnknownInput(StageRef Stage, Input Input, int GivenIndex, string GivenId) : Finding;
public sealed record class SelfInput(StageRef Stage, Input Input) : Finding;
public sealed record class LaterInput(StageRef Stage, Input Input, int SourceIndex) : Finding;
```

- **Slots are fields in the parameters** (owner decision). The source is a parameter like the level. It is checked in declaration order, and the validated `ThresholdParameters` holds it as a `StageId`. A technique with several inputs, such as change 4's, has one field and one `Input` case per slot.
- **One helper checks any slot:** `CheckInput(at, slot, handle, description, findings)` returns the source's `StageId`, or null when the input gave a finding or can't resolve. Its cases, in order:
  1. The handle is null: `MissingInput`. A record can't stop a definition from passing null, and absence is a case (`illegal-states R10`).
  2. The handle's position is outside the description, or the stage there was given a different id: `UnknownInput`, with the position and id the handle gave. This is how a handle from another builder shows up.
  3. The position is the stage's own: `SelfInput`.
  4. The position is after the stage's own: `LaterInput`, with that position.
  5. Otherwise the handle names an earlier stage, and no finding is added.
- **"Unknown" is told apart from "later"** because the whole description is in hand before the loop starts. A handle is compared with `description.Stages[index].Id`, so a position after the current stage is a known stage, just a later one.
- **A source's own findings aren't reported again** (spec: "An input is not reported for its source's own findings"). The handle is matched against the id as written, not against a validated `StageId`, so a source with an invalid id, a duplicate id or a bad parameter still resolves. The reading stage then produces no validated stage, but it adds no finding either. The pipeline is invalid through the source's own findings. This follows the rule `Pipeline.Validate` already has: a stage is built only when it added no finding, and a validated pipeline exists only when there are no findings at all.
- **No cycle finding.** A resolved input is always an earlier stage, so the reads form a DAG in description order.
- **Not representable after validation:**
  - a `ValidatedPipeline` holding a stage whose input names no stage, a later stage or itself. Validation builds a `Threshold` only when its source resolved to an earlier, valid stage, and only validation builds a `ValidatedPipeline`. `ThresholdParameters.Create` is public for tests, so the guarantee belongs to the validated pipeline, not to the stage record;
  - a level outside 0 to 255 (`ThresholdLevel`).
- **Order within a stage:** the id, then the parameters in declaration order. For threshold that is the source, then the level.
- **Alternatives rejected:**
  - Slots beside the parameters record: the owner chose inside.
  - A generic `ImmutableArray<StageHandle> Inputs` on every stage: a slot's meaning would be a position, and a wrong count would need its own finding.

### D4. `Image`: kept pixels that nothing can write

```csharp
// Illustrative.
public sealed class Image
{
    private readonly ImmutableArray<Rgba32> _pixels;   // row-major, never exposed as a writable view
    public int Width { get; }
    public int Height { get; }
    public Rgba32 this[int x, int y] { get; }
    public void CopyTo(Span<Rgba32> destination);      // bulk read for the texture upload
    public PixelBuffer ToBuffer();                     // a mutable copy, for a technique that wants to start from it
}

// Frame and PixelBuffer gain ToImage(): an immutable copy of the pixels as they are now.
```

- **Why a type of our own.** The owner asked for inputs to be unchangeable at compile time. A technique receives an `Image`, and `Image` has no setter, no writable span and no way back to a mutable array. Writing to an input then doesn't compile, rather than being something a review has to catch.
- **The timeline keeps `Image`s.** `TimelineEntry(StageId Stage, int Step, Image Image)`, and the run keeps each yielded frame with `frame.ToImage()`, where it now calls `ToBuffer()`. A stage's input is its source's last entry's `Image`, so passing it costs no further copy. `Frame` stays as it is: a view, valid until the next step.
- **Closing the escape hatches.** `ImmutableArray` can be turned back into an array through `ImmutableCollectionsMarshal`, `MemoryMarshal` or `Unsafe`. **Proposed, pending the owner's confirmation:** all three types join `src/Tilegen.Core/BannedSymbols.txt`, with messages citing `functional-core R4`. They go beyond the one ban the owner heard about, so task 1.3 waits for a yes. Without them, the escape hatches stay with review, and the Enforcement section says so. The task checks that Core uses none of them today, and that each entry gives RS0030.
- **Alternatives rejected:**
  - `IReadOnlyList<Rgba32>`: a cast can recover the array, and an interface crossing into a technique sits awkwardly with `functional-core R6`.
  - `ReadOnlyMemory<Rgba32>`: `MemoryMarshal.TryGetArray` returns the array.
  - Passing `Frame`: it is read-only, but its contract is "valid until the next step", which is wrong for an input, and it wraps a mutable buffer.

### D5. The engine keeps every output; Core picks the inputs

```csharp
// Illustrative.
public sealed class StageOutputs        // immutable: With returns a new value
{
    public static StageOutputs Empty { get; }
    public StageOutputs With(StageId stage, Image last);
    public Image this[StageId stage] { get; }   // a missing stage is a bug: validation guarantees every source ran first
}

public static IEnumerable<Frame> Run(Stage stage, StageOutputs outputs, Random random) => stage switch
{
    Noise noise => ValueNoise.Run(noise.Parameters, random),
    Threshold threshold => TerrainThreshold.Run(outputs[threshold.Parameters.Source], threshold.Parameters, random),
};
```

- **The engine decides nothing about wiring.** When a stage's enumerator ends, the run adds that stage's last timeline entry to its `StageOutputs`. Each stage starts with the outputs so far. Which output a stage reads is Core's choice, made in `Generation.Run`'s exhaustive switch (`functional-core R2`).
- **Every stage yields at least one frame.** Noise yields one per octave, at least 1, and threshold yields 1. A stage that ends with no frame is a bug, and the run throws `InvalidOperationException`; it is not an outcome case. No test produces it: the stage set is closed, so there is no stand-in technique that yields nothing, the same limit the enumerator's disposal has in `timeline-and-playback.md`.
- **Lookups only.** `StageOutputs` is looked up by id and never enumerated, so hash order can't reach the pixels (`functional-core R8`). `StageId` compares ordinally.
- **The random is unchanged.** Each stage still gets `new Random(StageSeed.Derive(seed, stage.Id))`. Threshold draws nothing from it.
- **Alternatives rejected:**
  - The engine resolves each stage's inputs through a Core `StageInputs.Of(stage)` and passes them in slot order. That puts a second switch in Core, and position-matching on the engine side.
  - `Generation.Run` takes the timeline: Core can't see an Engine type.

### D6. Threshold

`TerrainThreshold.Run(Image source, ThresholdParameters parameters, Random random)` is an iterator.

1. Calling it checks its arguments, and nothing else happens before the first frame is requested.
2. Its one step allocates a working `PixelBuffer` of the source's size. Each pixel becomes opaque white when ⌊(R + G + B) / 3⌋ ≥ level, and opaque black otherwise. Alpha is ignored. The step yields the frame.

- **One frame.** One integer pass over the image is cheap and is watched as one step (`functional-core R4`). The replay shows the last noise frame turning into the map at the stage boundary. A row-by-row sweep was offered and not chosen.
- **Black and white as terrain 0 and 1.** The output is data for change 4, not art. Both colours are named constants on the technique, so a later reader of the map uses the same values.
- **Integer brightness.** No floating point, so there are no rounding questions, and determinism is trivial (`functional-core R8`). The mean of the channels, not a weighted luma: the input is grey noise, and the rule is easy to state and test.
- **No width or height.** The output is the source's size, so no size finding is needed.
- **The random is taken and not drawn from,** so every technique has the same shape for `Generation.Run`. A test checks that it draws nothing.
- **Names.** The stage case is `Threshold` and the technique is `TerrainThreshold`, so the static class and the record don't clash where both namespaces are in scope.
- **No published algorithm.** It is a binary threshold. The topic doc says so, and cites no source.

### D7. The built-in pipeline

```csharp
var pipeline = new PipelineBuilder();
var height = pipeline.Add(new NoiseDescription("height", Width: 128, Height: 128, Octaves: 5, CellSize: 32, Persistence: 0.5));
pipeline.Add(new ThresholdDescription("terrain", Source: height, Level: 128));
```

- Six frames: five octaves, then the map. The window shows the noise refining and then becoming two colours. Level 128 is about the middle of the noise's values, so both terrains appear.
- The start seed stays 1.
- The preview-window spec's "at least two stages" becomes "noise, then a threshold that reads it".

### D8. Desktop

- **`FindingText`** gains an arm for each new finding. `Parameter.Level` reads "level", and `Input.Source` reads "source". For example: "Stage at index 1 ("terrain"): source names the stage at index 3, which comes later." The switch has no discard arm, so the build fails until every arm exists.
- **`FrameTexture`** uploads from `TimelineEntry.Image.CopyTo`.
- **No new controls.**

### D9. Tests

- **Core, validation** (`tests/Tilegen.Core.Tests/Pipelines/`):
  - one test per scenario in the pipeline-validation delta. Each new finding is produced, asserted as its case (`testing R15`), and each case is produced by at least one test;
  - the builder: `Add` returns handles with increasing positions and the given ids, and `Build` snapshots;
  - a closed-type test switching over every `Finding` case with no discard arm.
  - "Later" and "self" are produced with handles from a second builder whose stages have matching ids.
- **Core, `Image`:** `Frame.ToImage()` and `PixelBuffer.ToImage()` copy, so a later write to the buffer doesn't change the image; `CopyTo` is row-major and rejects a short span; `ToBuffer` gives an independent copy.
- **Core, threshold** (`tests/Tilegen.Core.Tests/Techniques/`):
  - the spec scenarios: either side of the level, channel averaging, level 0, one frame of the input's size, no draws from the random;
  - CsCheck properties over generated images up to 32 × 32 with random pixels, and levels from 0 to 255:
    - determinism;
    - every pixel is opaque black or white, and white exactly where the brightness is at or above the level;
    - **inputs unchanged:** a `ToBuffer()` snapshot of the input equals the input after every yielded frame (`testing R9`). The type already makes a change impossible, so this guards against the type being weakened later.
- **Core, generation:** `Generation.Run` on a validated threshold gives the same frames as `TerrainThreshold.Run` on the same image.
- **The golden** `tests/Tilegen.Core.Tests/Goldens/threshold/basic.png`:
  - **Input:** the approved `Goldens/value-noise/basic.png`, decoded and turned into an `Image`. Level 128.
  - **Provenance:** `technique: "threshold"`, and `parameters: { "input": "value-noise/basic.png", "level": 128 }`. `stageSeed` is 20261115: required by `GoldenProvenance`, and unused, since the threshold draws nothing. Also `dotnet: "11.0"` and `approvedIn: "stage-inputs"`.
  - **Approval:** the implementer runs the test and shows the owner the actual image beside its input. It is copied into `Goldens/` by hand only after the owner approves it (`testing R6`).
- **Engine** (`tests/Tilegen.Engine.Tests/`): one test per scenario in the generation-run delta:
  - the threshold's frame equals `TerrainThreshold.Run` on the noise stage's last entry;
  - the noise entries equal snapshots taken before the threshold started, read between advances;
  - a source-less stage is unaffected by a different stage before it.

  Existing scenarios move from `.Frame` to `.Image`.
- **Architecture:** no change. Core gains no reference. The banned list is checked by the build.

## Risks / Trade-offs

- **[The builder arrives before definitions exist, and change 7 may want another shape]** → The builder is two methods, and a description is still plain records, so change 7 can wrap or replace it without touching validation.
- **[A handle from another builder that happens to match the position and id resolves silently]** → It then names a stage with that id at that position in this description, which is what an id-string reference would do too. Only a mismatch can be detected, and it is reported as `UnknownInput`.
- **[The threshold golden depends on the value-noise golden as its input]** → The input is the stored, approved PNG, not a fresh noise run, so a change to value noise doesn't move it. If the value-noise golden is ever re-recorded, the threshold golden's input changes, and its provenance names that dependency. Both go to the owner.
- **[`TimelineEntry.Frame` becomes `.Image`, which touches Engine tests and Desktop]** → A mechanical change in the shared-contracts group, and the build lists every use.
- **[The three marshal and unsafe bans could hit existing code]** → Core uses none of them today. The task checks with a grep before adding the entries.
- **[An `ImmutableArray` per kept frame changes nothing in memory]** → The engine copied with `ToBuffer()` before, and now copies with `ToImage()`, at the same size.

## Topic docs

- **Creates `docs/design/stage-inputs.md`:**
  - how a description names an input: the builder and handles (D1);
  - how validation resolves a handle, in order, and why a source's findings aren't repeated (D3);
  - `Image` and why it is immutable, with the banned escape hatches (D4);
  - how the engine keeps outputs and Core picks inputs (D5);
  - the inputs-unchanged checks;
  - decisions and limits: no kinds, no size checks, no cycles possible;
  - where it lives, and its tests.
- **Creates `docs/design/threshold.md`:** what it does, the brightness rule, one frame, its parameters and ranges, sources (none: a plain binary threshold), decisions (black and white, integer brightness, no randomness), where it lives, and the golden.
- **Updates `docs/design/timeline-and-playback.md`:**
  - entries hold an `Image`;
  - the run keeps each finished stage's output and passes the outputs to `Generation.Run`;
  - "a stage's frames are its technique run alone" now includes its inputs;
  - a stage with no frame throws;
  - the memory example uses the new built-in pipeline.
- **Updates `docs/design/value-noise.md`:** threshold now reads its output, and the note in "Where it lives" about there being no inputs-unchanged property points at threshold's.
- **Architecture docs:** the Enforcement section of `functional-core.md` (the inputs-unchanged property is in place, and the three new banned types), the README's "Enforcement status" ("nothing to check yet"), and any stale text in the Enforcement sections of `testing.md` and `illegal-states.md`. Tasks list each one.
