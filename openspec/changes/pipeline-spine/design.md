# Design

## Context

- **Starting point.** The scaffold (archived as `2026-11-14-scaffold-solution`) left five marker-only projects, the architecture tests, the banned-API lists and an empty `Tilegen.TestKit`. See proposal.md for why the spine comes next and what is out of scope.
- **Rules that shape this change:**
  - `functional-core R3`, `R4`, `R8`, `R9`, `R11`, `R12`: techniques are iterators that take the stage's `Random`, Core never constructs one, and the pixel buffer is the one mutable type.
  - `illegal-states R1` to `R3`, `R6` to `R8`, `R13`: a closed stage hierarchy, a pure-data description, an engine that accepts only a validated pipeline, typed ids and checked parameters.
  - `railway R2`, `R4`, `R6`, `R11`, `R13`: validation returns every finding as data.
  - `testing R3` to `R6`, `R9`, `R12`, `R15`: goldens, provenance, owner approval, properties, no shared statics.
- **Owner decisions taken while proposing (2026-11-14):**
  - **PNG:** StbImageSharp to decode and StbImageWriteSharp to encode, in the test kit now and in Export later.
  - **Technique:** value noise summed over octaves.
  - **Buffer:** one RGBA8 `PixelBuffer`.
  - **Description:** plain values, which validation parses into typed stages.

## Goals / Non-Goals

**Goals:**

- The types that every later technique and the engine will build on: buffer, frame, description, validated pipeline, findings, seeds.
- One technique that runs end to end in a Core test, proven by a golden and by properties.
- A golden comparer that makes the right thing easy: decode, compare, write the actual image out, and never record anything by itself.

**Non-Goals:**

- No runner in Core or Engine that walks a pipeline. A Core test enumerates the technique directly.
- No general parameter-description framework. Each technique states its parameters by hand.
- No combinators over outcomes (`railway R10`).

## Layer mapping

| Piece | Layer | Notes |
| --- | --- | --- |
| `Rgba32`, `PixelBuffer`, `Frame` | Core | Core's own colour value (`functional-core R7`). `PixelBuffer` is the one mutable type (`R11`). |
| `PipelineDescription`, `StageDescription` and its `NoiseDescription` case | Core | Pure data (`illegal-states R2`) |
| `Pipeline.Validate`, `ValidationOutcome`, `Finding` | Core | A pure function, returning data (`railway R13`) |
| `ValidatedPipeline`, `Stage` and its `Noise` case, `StageId`, `NoiseParameters` and its wrappers | Core | Built only by validation (`illegal-states R3`) |
| `RunSeed`, `StageSeed`, `StageSeed.Derive` | Core | Pure (`functional-core R9`) |
| `ValueNoise.Run`, `Generation.Run` (dispatch from a `Stage` to its technique) | Core | Iterators (`functional-core R4`) |
| Golden comparer, PNG decode and encode, provenance reader | Test kit | Test-only IO. Not a layer. |
| Engine, ports, adapters, Desktop | None | Only `EngineMarker` changes, because `CoreMarker` goes |

Nothing in this change decides anything outside Core. The test kit compares pixels and reports; it never chooses what a technique does.

## Decisions

### D1. The description holds plain values; validation parses them into typed stages

`StageDescription` is a closed hierarchy whose cases hold `string`, `int` and `double`. `Stage` is a second closed hierarchy whose cases hold `StageId` and checked parameter types. `Pipeline.Validate` is the only producer of `Stage` values and of `ValidatedPipeline`.

```csharp
// Illustrative; names are not final.
public closed record class StageDescription;
public sealed record class NoiseDescription(string Id, int Width, int Height, int Octaves, int CellSize, double Persistence)
    : StageDescription;
public sealed record class PipelineDescription(ImmutableArray<StageDescription> Stages);

public closed record class Stage;
public sealed record class Noise(StageId Id, NoiseParameters Parameters) : Stage;
public sealed class ValidatedPipeline { public ImmutableArray<Stage> Stages { get; } internal ValidatedPipeline(...) }
```

- **Why:** the owner chose this so one save reports every mistake (`railway R6`).
- **Alternative rejected:** typed parameters in the description, with throwing factories. One shape per technique, but a definition would throw at its first bad value, so the user would see one mistake per save.
- **Cost:** two shapes per technique. Adding a technique adds a case to each hierarchy. The compiler then lists both switches in `Pipeline.Validate` and `Generation.Run`.
- **Records and equality:** both hierarchies use sealed records for value equality. Neither derives a record from a record that must compare equal to it (`illegal-states` anti-pattern).
- **No seed in the description.** The run seed is an input to a run, beside the validated pipeline. Whether a definition can set a default seed is left to the definition change.

### D2. Validation is the checking factory for definition values

`illegal-states R8` asks for a checking factory that returns an outcome wherever a value comes from a definition. Here every such value arrives through `Pipeline.Validate`, so validation is that factory for the whole parameter set.

- **The wrappers:** `StageId`, `PixelSize` (width and height), `Octaves`, `CellSize` and `Persistence`.
  - Each has an internal constructor that validation calls after its range check.
  - Each also has a public throwing factory for constants and tests, where a bad value is a bug.
  - Each exposes its range as constants, so the check and the finding read one source.
- **`NoiseParameters`** has an internal constructor that also enforces the cross-field rule: cell size ≥ 2^(octaves − 1).
- **Alternative rejected:** an outcome type for each wrapper's factory. That makes five small outcome types, and validation would only unpack them into findings anyway.

### D3. The outcome types

```csharp
// Illustrative.
public closed record class ValidationOutcome;
public sealed record class Valid(ValidatedPipeline Pipeline) : ValidationOutcome;
public sealed record class Invalid(ImmutableArray<Finding> Findings) : ValidationOutcome;   // internal ctor; never empty

public readonly record struct StageRef(int Index, string GivenId);       // position, and the id as written

public closed record class Finding;
public sealed record class EmptyPipeline : Finding;
public sealed record class InvalidStageId(StageRef Stage) : Finding;                          // empty, or contains whitespace
public sealed record class DuplicateStageId(StageRef Stage, int FirstIndex) : Finding;
public sealed record class OutOfRange(StageRef Stage, Parameter Parameter, double Value, ParameterRange Range) : Finding;
public sealed record class FinestOctaveBelowOnePixel(StageRef Stage, int CellSize, int Octaves, int SmallestValidCellSize) : Finding;

public enum Parameter { Width, Height, Octaves, CellSize, Persistence }
public readonly record struct ParameterRange(double Min, bool MinInclusive, double Max);    // Max is always inclusive
```

- **The invalid states these rule out:**
  - a pipeline the engine can run that wasn't validated (`ValidatedPipeline` has no public constructor);
  - a result that is both valid and has findings, or is invalid with no findings (two cases, and `Invalid` refuses an empty list);
  - a noise stage with octaves out of range, or a finest octave under a pixel (`NoiseParameters` can't be built that way);
  - a stage id used as a plain string after validation (`StageId`).
- **Findings carry data, not text** (`railway R4`, `R5`). Desktop will render them later. Tests assert on the cases (`testing R15`), and each case is produced by at least one test.
- **`StageRef.GivenId` is the raw text,** because the id may be the thing that's wrong. It is not a `StageId`, and it is never null: an empty id is `""`.
- **Order:**
  - Stages are checked in description order. Within a stage, the id comes first, then its parameters in declaration order, then the cross-field rule.
  - The cross-field rule is checked only when octaves and cell size are both in range, so one wrong value doesn't report twice.
  - An empty pipeline gives exactly one finding.
- **`Parameter` is shared by all techniques.** With one technique, a per-technique enum would be speculation. It's split if it ever grows confusing.
- **Ranges:**
  - width and height: 1 to 4096;
  - octaves: 1 to 12. The cap is a choice, not forced: 13 octaves would still fit a cell size of 4096. A 12th octave already needs a cell size of at least 2048;
  - cell size: 1 to 4096;
  - persistence: (0, 1], and a non-finite value is out of range.

### D4. Stage seeds: FNV-1a over the run seed and the id

```
bytes = little-endian 4 bytes of RunSeed.Value  ++  UTF-8 bytes of StageId.Value
h     = FNV-1a 64(bytes)
seed  = (int)((h ^ (h >> 32)) & 0x7FFF_FFFF)
```

- **Why FNV-1a:**
  - it's a published, documented hash with published test vectors;
  - it has no per-process randomization (`functional-core R9`) and no culture;
  - it's a few lines of code.
  - It isn't an RNG, so "don't write an RNG" holds.
- **Masked to non-negative:** `new Random(s)` and `new Random(-s)` appear to give the same sequence, because the seeded legacy algorithm takes `Math.Abs`. Task 3.1 confirms that before it's relied on. Masking keeps distinct derivations from colliding that way.
- **Alternatives rejected:**
  - `HashCode.Combine` and `string.GetHashCode`: forbidden, because they are randomized per process.
  - Position in the pipeline: inserting a stage would shift every seed after it.
  - SHA-256 via `System.Security.Cryptography`: more machinery than needed, and it pulls a crypto namespace into Core for no gain.
  - A SplitMix-style finalizer: a better mixer, but it edges towards writing an RNG.
- **Tests:**
  - the hash is checked against FNV's published test vectors;
  - a pinned table of (run seed, id) → stage seed catches any change;
  - a property checks that derivation is unchanged under another current culture. `CultureInfo.CurrentCulture` is async-local, so setting it inside one test doesn't leak (`testing R12`).

### D5. Value noise

The run takes `NoiseParameters` and `Random`. For each octave k = 0 … n−1, in one step:

1. **Lattice.** Spacing is s = CellSize / 2^k, at least 1 by D2. Draw a lattice of `random.NextDouble()` values, row-major, sized ⌊(W−1)/s⌋+2 by ⌊(H−1)/s⌋+2. The draws happen at the start of the octave's own step, so the first frame of an n-octave run equals a 1-octave run's output: the spec scenario.
2. **Sample and accumulate.** For pixel (x, y), take g = (x/s, y/s), its cell (i, j) = ⌊g⌋, and fractions t = g − ⌊g⌋. Blend the four corners bilinearly with smoothstep weights t²(3 − 2t). Add amplitude × value into a `double` accumulator. The amplitude starts at 1 and is multiplied by persistence each octave; repeated multiplication avoids `Math.Pow`.
3. **Write the frame.** Normalise each accumulator cell by the running sum of amplitudes, giving [0, 1]. No clamp is needed, because the lerp is written `a + (b − a)·w`. Write grey `(byte)Math.Round(v × 255, MidpointRounding.AwayFromZero)` with alpha 255 into the working buffer. Yield it as a frame.

- **Buffers:** the working buffer and the accumulator are created by the run and never escape (`functional-core R12`). The run has no input buffer.
- **Determinism:** only basic IEEE double operations in a fixed order, and lattice draws in a fixed order. No unordered collections (`functional-core R8`).
- **Lacunarity is fixed at 2** and isn't a parameter. Spacing halves exactly, so the finest-octave rule is a simple power of two.
- **No wrapping at the edges** (out of scope).
- **Sources:**
  - Ebert, Musgrave, Peachey, Perlin and Worley, *Texturing & Modeling: A Procedural Approach*, 3rd ed. (2003): lattice value noise, and Musgrave's fBm.
  - Scratchapixel, "Value Noise and Procedural Patterns".
  - Inigo Quilez, "fbm", for octave summation by repeated multiplication. It does not normalise; normalising by the amplitude sum is tilegen's own step.
  - The implementer checks each citation against the text and records it in the topic doc. No code is copied.

### D6. Frames

`Frame` is a read-only view over the run's working buffer: width, height, a pixel indexer, and `ToBuffer()`, which returns a fresh copy. It is valid until the next `MoveNext` (`functional-core R4`). The engine, and tests, call `ToBuffer()` to keep a frame. A frame carries no step number or stage id, because the timeline that needs them is the engine's.

### D7. The golden comparer (test kit)

- **Layout:** goldens live in `tests/Tilegen.Core.Tests/Goldens/<technique>/<name>.png`. Each has its provenance beside it in `<name>.golden.json`, holding:
  - `technique`;
  - `parameters` (the description's plain values);
  - `stageSeed`;
  - `dotnet` (the runtime's major.minor);
  - `approvedIn` (the OpenSpec change name).
- **The golden test reads its parameters and seed from the provenance file,** so the two can't drift apart.
  - The provenance records the stage seed rather than a run seed and id. The golden then tests the technique alone, and a change to seed derivation doesn't move it.
  - The files are copied to the test output with `CopyToOutputDirectory`.
- **API:**
  - `Golden.Compare(...)` returns a closed `GoldenComparison`: `Matches`, `Missing`, `SizeDiffers` or `PixelsDiffer` (count, first differing coordinate).
  - `Golden.AssertMatches(...)` throws on anything but `Matches`. That is the test-only unwrap allowed by `railway R9`. Its message gives the paths written, the pixel difference, and the recorded and current .NET versions.
- **On anything but a match,** it writes `<name>.actual.png`, and for a mismatch `<name>.expected.png`, under `golden-results/` in the test output directory. Paths are unique per golden, so parallel tests don't collide.
- **No record mode.** The comparer has no switch, environment variable or API that writes into `Goldens/`. A new or changed golden is copied in by hand after the owner approves it (`testing R6`).
- **PNG decoding** uses StbImageSharp (`ImageResult.FromMemory`, forced to RGBA). **Encoding** uses StbImageWriteSharp (`ImageWriter.WritePng`). The comparer compares decoded `Rgba32` values, never bytes (`testing R4`).
- **Tests for the comparer:**
  - one per `GoldenComparison` case;
  - a round trip: encode, decode, same pixels.
  - They live in `Tilegen.Core.Tests`, beside the goldens that use the comparer, because the test kit has no test project of its own. Each test that writes files uses a unique temporary directory and removes it (`testing R12`).

### D8. Packages and the allowlist

| Package | Version | Licence | Project |
| --- | --- | --- | --- |
| StbImageSharp | 2.30.16 | Unlicense or MIT | `Tilegen.TestKit` |
| StbImageWriteSharp | 1.16.7 | Public domain (stated in its README) | `Tilegen.TestKit` |
| CsCheck | 5.0.1 | Apache-2.0 (to be confirmed when it's added) | `Tilegen.Core.Tests` |

- **The allowlist.** `DeclaredReferenceTests`' row for `Tilegen.TestKit` gains the two Stb packages, which are not private: the comparer exposes nothing of theirs, but tests run their code. The row's comment and the note under the table in `ports-and-adapters.md` say so.
- **CsCheck needs no row,** because only `src/` projects and the test kit have rows.
- **`CoreMarker`** is deleted. `Layers.cs` and `EngineMarker` name a real Core type (`PixelBuffer`).

## Risks / Trade-offs

- **[`System.Random`'s seeded sequence could change in a future .NET]** → The golden's provenance records the runtime version, and the comparer's failure message prints both versions. Re-recording for an upgrade is a legitimate, owner-approved reason (`testing R7`).
- **[Floating-point results differ across platforms]** → The promise is the same .NET version and platform only (`functional-core R8`). CI and the owner both use Windows.
- **[StbImageWriteSharp hasn't been released since 2022]** → It's used only in tests, to write failure images. Losing it costs a debugging aid, not correctness. Export picks its encoder in its own change.
- **[A 4096 × 4096 stage peaks at about 320 MiB: a 64 MiB buffer, a 128 MiB accumulator, and a lattice of up to 128 MiB when the spacing is 1]** → It's within the ranges, but far beyond tile-sized use. If it matters, lower the maximum in a later change.
- **[The first golden blocks the task group until the owner looks at it]** → The group's tasks are ordered so everything else lands first. The owner is shown the image and approves before it's copied in.
- **[`CultureInfo.CurrentCulture` in a test]** → It's async-local, so it doesn't leak into parallel tests. It's still restored in a `finally`.

## Topic docs

- **Creates `docs/design/value-noise.md`:** what it does; the per-octave step and its frame; parameters and ranges; the finest-octave rule; sources (D5); decisions and limits (fixed lacunarity, no wrapping, quantization); where it lives, and the golden.
- **Creates `docs/design/stage-seeds.md`:** the derivation (D4), why FNV-1a, the non-negative mask, what the seed is independent of, the pinned table, and its sources (the FNV specification and test vectors).
- Architecture docs that change are not topic docs. They are listed in tasks.md: `testing.md`, `ports-and-adapters.md`, `functional-core.md`, `railway.md`, `README.md`, and `.claude/rules/tests.md`.
