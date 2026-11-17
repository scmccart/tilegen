# Threshold

## What it does

Turns an earlier stage's image into a two-terrain map by brightness. Each pixel whose brightness is at or above a level becomes opaque white, which is terrain 1. Every other pixel becomes opaque black, which is terrain 0. The output is data for the techniques that build terrain from it, not art.

It is the first technique that reads another stage's output. Its source is a stage earlier in the same pipeline, named through the handle the pipeline builder gave for it (`stage-inputs.md`). The run is one step, so it yields one frame, the size of its input. In the replay, the source's last frame turns into the map at the stage boundary.

It runs as a `Threshold` stage through `Generation.Run`, which looks up the source's last image in the run's `StageOutputs`. It can also be run directly, through `TerrainThreshold.Run(Image, ThresholdParameters, Random)`.

In the built-in pipeline it reads the noise stage, and the cut-tile stage reads its output, cutting a tile from the middle band of the map (`cut-tile.md`).

## How it works

`TerrainThreshold.Run` is an iterator. Calling it checks its arguments and does nothing else: nothing is allocated or read until the first frame is requested. Its one step does four things:

1. **Allocate.** It allocates a working `PixelBuffer` of the input's width and height. The buffer escapes the run only through the frame's read-only view.
2. **Measure.** For each pixel, row by row, it computes the input pixel's brightness, ⌊(R + G + B) / 3⌋. That is integer division of a non-negative sum, so it rounds down and gives 0 to 255. Alpha is ignored.
3. **Write.** Where the brightness is at or above the level, the output pixel becomes `TerrainThreshold.AtOrAbove`, opaque white (255, 255, 255, 255). Elsewhere it becomes `TerrainThreshold.Below`, opaque black (0, 0, 0, 255).
4. **Yield.** The step yields a `Frame` over the working buffer.

The input is an `Image`, which has no writer, so the run can't change it (`functional-core R4`, `stage-inputs.md`). The frame is a view, valid until the next step. A caller that keeps it calls `ToImage()`, as with any technique. The run holds nothing that needs disposing.

The run takes a `Random`, as every technique does, so `Generation.Run` gives each technique the same arguments. It draws nothing from it. The output depends only on the input's pixels and the level.

## Parameters

| Parameter | Range | Meaning and effect |
| --- | --- | --- |
| Source | An earlier stage of the same pipeline | The stage whose last image is read. A description names it with a `StageHandle`, and validation turns that into the source's `StageId` (`stage-inputs.md`). |
| Level | 0 to 255, whole numbers | The brightness at which terrain 1 starts. A higher level gives less terrain 1. At 0 every pixel is white. At 255 only a pixel whose red, green and blue are all 255 is white. |

- **No width or height.** The output is the input's size, so no size can be wrong and no size finding exists.
- **Validation reports a bad level.** A level outside 0 to 255 gets an `OutOfRange` finding naming `Parameter.Level`, the value and the range. Within a threshold stage the id is checked first, then the source, then the level.
- **Typed parameters.** `ThresholdLevel` and `ThresholdParameters` have internal constructors, and only validation builds them from a definition's values. Their `Create` methods are for constants and tests.

## Sources

None. It is a plain binary threshold on the mean of the colour channels. Nothing was taken from a paper, an article or another program, and no code was copied.

## Decisions and limits

- **One frame.** One integer pass over the image is cheap, so it is watched as one step. A row-by-row sweep, with a frame per row, was offered and not chosen.
- **Black and white as terrains 0 and 1.** Both colours are named on `TerrainThreshold`, as `Below` and `AtOrAbove`, so a later reader of the map compares against the same values. Terrain colours and textures come later.
- **The mean of the channels, not a weighted luma.** The input is grey noise, and the rule is easy to state and test. For a grey pixel the brightness is its channel value. For a coloured input the mean can't tell hues apart: pure red, pure green and pure blue all have brightness 85.
- **Integer brightness.** There is no floating point, so there are no rounding questions. Rounding down means a pixel is white exactly when R + G + B ≥ 3 × level.
- **Alpha is ignored and the output is opaque.** A transparent pixel is judged by its colour.
- **No randomness.** The same input and level give the same pixels with any seed. Nothing here depends on `System.Random`'s sequence, but generation is still promised only on the same .NET version and platform (`functional-core R8`).
- **Memory.** The working buffer is 4 bytes a pixel, the size of the input: 64 MiB at 4096 × 4096. The input image belongs to the engine, not to the run.

## Where it lives

- `src/Tilegen.Core/Techniques/TerrainThreshold.cs`: `TerrainThreshold.Run`, and the two terrain colours `Below` and `AtOrAbove`. The class is `TerrainThreshold` so that it doesn't clash with the `Threshold` stage where both namespaces are in scope.
- `src/Tilegen.Core/Techniques/ThresholdParameters.cs` and `ThresholdLevel.cs`: the typed parameters and the level's range.
- `src/Tilegen.Core/Pipelines/StageDescription.cs`: `ThresholdDescription(Id, Source, Level)`, the stage as a definition writes it.
- `src/Tilegen.Core/Pipelines/Stage.cs`: the `Threshold` stage.
- `src/Tilegen.Core/Pipelines/Pipeline.cs`: validation of the id, the source (`stage-inputs.md`) and the level.
- `src/Tilegen.Core/Pipelines/Generation.cs`: `Generation.Run`, which runs a `Threshold` stage on the last image of `Parameters.Source`.
- `tests/Tilegen.Core.Tests/Techniques/ThresholdTests.cs`:
  - pixels either side of the level;
  - the channel mean, rounded down;
  - alpha ignored;
  - level 0 making every pixel white;
  - white exactly at or above the level, over every brightness;
  - the two named colours, opaque black and opaque white;
  - one frame of the input's size;
  - the seed not mattering, and a whole run drawing nothing from the `Random`;
  - the input unchanged after the run;
  - arguments checked at the call.
- `tests/Tilegen.Core.Tests/Techniques/ThresholdProperties.cs`: CsCheck properties over generated images up to 32 × 32 with random pixels, alpha included, and levels from 0 to 255:
  - determinism;
  - one frame of the input's size;
  - every pixel opaque black or white, and white exactly where the brightness is at or above the level;
  - the input unchanged after every yielded frame (`testing R9`).
- `tests/Tilegen.Core.Tests/Techniques/ThresholdWrapperTests.cs`: the level's bounds, and `Create` refusing a value out of range.
- `tests/Tilegen.Core.Tests/Pipelines/PipelineValidationTests.cs`: a level out of range, a valid threshold reading a noise stage, and the order id, source, level. The input findings are listed in `stage-inputs.md`.
- `tests/Tilegen.Core.Tests/Pipelines/GenerationTests.cs`: a threshold stage gives the same frame through `Generation.Run` as through `TerrainThreshold.Run` on its source's image, and not on another stage's.
- **The golden,** in `tests/Tilegen.Core.Tests/Goldens/threshold/`:
  - `basic.golden.json`, its provenance (`testing R5`):
    - technique `threshold`;
    - input `value-noise/basic.png`, the approved value-noise golden, 64 × 64;
    - level 128;
    - stage seed 20261115, which the provenance format requires and the threshold doesn't use;
    - .NET 11.0, and `approvedIn` `stage-inputs`, the change in which the owner approved it.
  - `basic.png`, 64 × 64, black where the noise is darker than the level and white elsewhere. The owner approved it by eye on 2026-11-16 (`testing R6`).
  - **The input is the stored value-noise golden, not a fresh run,** so a change to value noise doesn't move this golden. If the value-noise golden is ever re-recorded, this golden's input changes with it, and both go to the owner.
- **The golden test** is `ThresholdGoldenTests.cs`. It reads the input's path and the level from the provenance, decodes the input PNG into an `Image`, runs the technique and compares the one frame.
  - The test project copies `Goldens/` to its output, and the test reads both directories from there.
  - On a failure it writes `basic.actual.png`, and on a mismatch `basic.expected.png` too, to `golden-results/threshold/` in the test output.
