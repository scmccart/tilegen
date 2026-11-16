# Stage inputs

## What it does

Lets a stage read the output of an earlier stage in the same pipeline. A definition names the stage to read with a typed handle, which the pipeline builder hands out when that stage is added. Validation resolves each handle against the description, reports each wrong one as a closed finding, and builds a stage only when its input names an earlier stage that is itself valid. The technique then receives that stage's last image as an `Image`, which nothing can write to.

The threshold (`threshold.md`) is the first technique with an input. It has one slot, its source.

## How a description names an input

A definition builds its description with a `PipelineBuilder`:

```csharp
var pipeline = new PipelineBuilder();
var height = pipeline.Add(new NoiseDescription("height", 128, 128, 5, 32, 0.5));
pipeline.Add(new ThresholdDescription("terrain", Source: height, Level: 128));
var description = pipeline.Build();
```

- **`Add` returns a handle** after it appends the stage. The handle names the stage by its position and by the id it was added with, as written: `""` for a null id. Within one builder, a stage can therefore hold a handle only to a stage added before it.
- **A handle can only come from `Add`.** `StageHandle` has an internal constructor and get-only properties, so it can't be made, or changed with `with`, any other way.
- **A handle is a value.** Two handles with the same position and id are equal. Descriptions stay plain data that can be compared and printed (`illegal-states R2`).
- **`Build` snapshots.** It returns an immutable description of the stages added so far, and can be called again. A later `Add` doesn't change a description already built.
- **The builder is mutable** while a description is built, and doesn't escape the code that builds it (`functional-core R11`). An immutable builder would make every definition thread a (description, handle) pair through each line.
- **`PipelineDescription` keeps its public constructor.** Tests build descriptions a builder can't, such as a stage that holds a handle from another builder, because validation must report those.
- **An input slot is a parameter.** A technique's inputs are fields of its description and of its validated parameters, beside its numbers. The threshold's source is a `StageHandle` in `ThresholdDescription` and a `StageId` in `ThresholdParameters`. A technique with several inputs has one field, and one `Input` case, per slot.

## How validation resolves an input

`Pipeline.Validate` checks stages in description order. Within a stage it checks the id, then each parameter in declaration order, inputs included, then any cross-field rule. For a threshold that is the id, then the source, then the level.

Each input slot is resolved by `CheckInput`. The whole description is in hand before the loop starts, so the handle's position can be looked up even when it is after the current stage. The cases, in order:

1. **The handle is null:** a `MissingInput` finding. A record can't stop a definition from passing null, and absence is a case (`illegal-states R10`).
2. **The position is outside the description, or the stage there was added with a different id:** an `UnknownInput` finding, giving the position and id the handle gave. The id is compared ordinally, so `"Height"` doesn't match `"height"`. This is how a handle from another builder shows up.
3. **The position is the stage's own:** a `SelfInput` finding.
4. **The position is after the stage's own:** a `LaterInput` finding, giving that position.
5. **Otherwise the handle names an earlier stage,** and no finding is added.

Each finding names the stage, as a `StageRef`, and the slot, as an `Input` case. `Input` names a slot the way `Parameter` names a parameter. Desktop's `FindingText` turns each one into text; for example, "Stage at index 0 ("terrain"): source names the stage at index 1, which comes later."

### A source's own findings aren't reported again

The handle is matched against the source's id as written, not against a validated `StageId`. So a source with an invalid id, a repeated id or a parameter out of range still resolves, and the reading stage adds no finding for it. The source's findings are reported once, against the source.

A stage is built only when it added no finding and each of its inputs names an earlier stage that was built. Validation keeps the stage built at each position, or null where none was, and `CheckInput` returns the source's id only when its position holds a built stage. A threshold reading a bad stage, or reading a threshold that reads one, is therefore not built either, and adds no finding. The pipeline is invalid through the source's own findings, because a validated pipeline exists only when there are no findings at all.

What can't be represented after validation:

- a `ValidatedPipeline` holding a stage whose input names no stage, a later stage or itself. Only validation builds a `ValidatedPipeline`, and it builds a `Threshold` only when its source resolved to an earlier, valid stage. `ThresholdParameters.Create` can name any stage, for tests, so the guarantee belongs to the validated pipeline, not to the stage record.
- a cycle. A resolved input is always an earlier stage, so the reads form a directed acyclic graph in description order.

## Inputs a technique can't change

A technique must not mutate an input, which is another stage's output (`functional-core R4`). Tilegen makes that a compile error rather than a review finding.

- **`Image`** holds kept pixels in an `ImmutableArray<Rgba32>`, row-major, at least 1 by 1. It has `Width`, `Height`, a read-only indexer, `CopyTo(Span<Rgba32>)` for a bulk read such as a texture upload, and `ToBuffer()` for a mutable copy a technique can start from. It has no public constructor, no setter and no writable view.
- **Making one copies.** `PixelBuffer.ToImage()` and `Frame.ToImage()` copy the pixels as they are now, so a later write to the buffer doesn't reach the image.
- **A technique is given its inputs as `Image`s.** `TerrainThreshold.Run` takes an `Image`, so writing to its source doesn't compile.
- **The escape hatches are banned in Core.** `ImmutableCollectionsMarshal`, `MemoryMarshal` and `Unsafe` can each turn read-only pixels back into something writable. All three are in `src/Tilegen.Core/BannedSymbols.txt`, with messages citing `functional-core R4`, and RS0030 is an error.
- **Alternatives rejected:**
  - `IReadOnlyList<Rgba32>`: a cast can recover the array.
  - `ReadOnlyMemory<Rgba32>`: `MemoryMarshal.TryGetArray` returns the array.
  - Passing the source's `Frame`: it is read-only, but it is valid only until its run's next step, and it wraps a mutable buffer.

### The checks

The type already makes a change impossible, so these guard against it being weakened later:

- `ThresholdProperties.The_input_is_unchanged_after_every_frame`: over generated images and levels, the input equals a `ToBuffer()` snapshot taken before the run, after every yielded frame (`testing R9`).
- `ThresholdTests.The_input_is_unchanged_after_the_run`: the threshold spec's scenario.
- `ImageTests`: a write to the buffer or frame after `ToImage()` doesn't change the image, a buffer from `ToBuffer()` is independent, and `Image` has no public constructor and no public way to write its pixels.
- The build: RS0030 on the three banned types.

## In a run

_Placeholder. The engine half of this mechanism is described with the engine's scenarios (task 4.2 of the `stage-inputs` change): how the run keeps each finished stage's last image in `StageOutputs`, and how `Generation.Run` picks a stage's inputs from them._

## Decisions and limits

- **No input kinds.** Every output is the same RGBA8 image, and none carries a meaning, so any stage can feed any slot. A wrong-kind finding arrives with the first technique whose inputs differ in meaning.
- **No size checks between inputs.** The threshold has one input and takes its size from it, so nothing can mismatch. The check arrives with the first technique that has two inputs.
- **No cycle finding.** An input can only resolve to an earlier stage, so a cycle can't be written.
- **A foreign handle that matches resolves silently.** A handle from another builder whose position and id both match this description names the stage there, as an id string would. Only a mismatch can be detected, and it is an `UnknownInput`.
- **Stage seeds don't depend on inputs.** A stage's seed still depends only on the run seed and its id (`stage-seeds.md`).

## Where it lives

- `src/Tilegen.Core/Pipelines/PipelineBuilder.cs` and `StageHandle.cs`: the builder and handles.
- `src/Tilegen.Core/Pipelines/StageDescription.cs`: the id on the base `StageDescription`, and `ThresholdDescription`, whose source is a handle.
- `src/Tilegen.Core/Pipelines/Finding.cs`: the `Input` enum and the `MissingInput`, `UnknownInput`, `SelfInput` and `LaterInput` findings.
- `src/Tilegen.Core/Pipelines/Pipeline.cs`: `Validate`, `ValidateThreshold` and `CheckInput`.
- `src/Tilegen.Core/Imaging/Image.cs`, with `ToImage` in `PixelBuffer.cs` and `Frame.cs`.
- `src/Tilegen.Core/BannedSymbols.txt`: the three banned escape hatches.
- `src/Tilegen.Desktop/FindingText.cs`: the text for each input finding, and the name of each `Input`.
- `tests/Tilegen.Core.Tests/Pipelines/PipelineBuilderTests.cs`: handles at increasing positions with the ids given, `""` for a null id, `Build` in order and as a snapshot, and handle equality.
- `tests/Tilegen.Core.Tests/Pipelines/PipelineValidationTests.cs`:
  - a stage reading an earlier stage through its handle;
  - a missing input;
  - an unknown input from another builder, past the end, and differing only in case;
  - self and later inputs, using handles from a second builder whose stages have matching ids;
  - a source with a bad parameter, an invalid, null or repeated id, or a bad source of its own, giving only the source's findings;
  - a threshold reading a threshold.
- `tests/Tilegen.Core.Tests/Pipelines/PipelineValidationProperties.cs`: generated descriptions built with the builder, including thresholds that read earlier stages. An in-range description validates with each threshold reading the stage its handle named; one parameter pushed out of range gives exactly that finding, even when the pushed stage is a threshold's source; and validating twice gives equal results.
- `tests/Tilegen.Core.Tests/Pipelines/ClosedTypeTests.cs`: switches over every `StageDescription`, `Stage`, `Finding`, `Parameter` and `Input` case with no discard arm.
- `tests/Tilegen.Core.Tests/Imaging/ImageTests.cs` and `tests/Tilegen.Core.Tests/Techniques/ThresholdProperties.cs`: the checks above.
