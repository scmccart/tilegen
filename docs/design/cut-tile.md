# Cut tile

## What it does

Cuts one isometric tile out of an earlier stage's image. The source pixels inside the tile's diamond are copied, and every other pixel of the tile's box becomes transparent black. The output is the tile's box, W × H, with the diamond in it. It is how a generated texture becomes a tile.

Which pixels are inside the diamond, and why tiles cut this way meet with no gap and no overlap, is the isometric geometry (`isometric-geometry.md`). This doc doesn't repeat it.

The cut reads one source, a stage earlier in the same pipeline, named through the handle the pipeline builder gave for it (`stage-inputs.md`). The run is one step, so it yields one frame, the size of the tile's box. In the replay, the source's last frame turns into the tile at the stage boundary.

It runs as a `CutTile` stage through `Generation.Run`, which looks up the source's last image in the run's `StageOutputs`. It can also be run directly, through `DiamondCut.Run(Image, CutTileParameters, Random)`.

In the built-in pipeline it reads the threshold's 128 × 128 map and cuts a 128 × 64 tile from its middle band, at left 0 and top 32, so the preview's last frame is a single diamond.

## How it works

`DiamondCut.Run` is an iterator. Calling it checks its arguments and does nothing else:

- a null source, parameters or random throws `ArgumentNullException`;
- a tile's box that doesn't lie wholly inside the source throws `ArgumentException`. A validated pipeline can't hold such a cut (see Validation), so at the call it is a bug, not an outcome.

Nothing is allocated or read until the first frame is requested. The one step does four things:

1. **Allocate.** It allocates a working `PixelBuffer` of W × H, the tile's box. The buffer escapes the run only through the frame's read-only view.
2. **Copy the inside.** For each row y of the box, `DiamondMask.Row(y)` gives the row's one run of inside pixels. Each pixel (x, y) in that run becomes the source pixel at (Left + x, Top + y), all four channels, alpha included.
3. **Clear the outside.** Every other pixel of the box becomes `DiamondCut.Outside`, transparent black (0, 0, 0, 0). It is written explicitly, not left to the buffer's zero fill.
4. **Yield.** The step yields a `Frame` over the working buffer.

The input is an `Image`, which has no writer, so the run can't change it (`functional-core R4`, `stage-inputs.md`). The frame is a view, valid until the next step. A caller that keeps it calls `ToImage()`, as with any technique. The run holds nothing that needs disposing.

The run takes a `Random`, as every technique does, so `Generation.Run` gives each technique the same arguments. It draws nothing from it. The output depends only on the source's pixels, the tile width and the position.

## Parameters

| Parameter | Range | Meaning and effect |
| --- | --- | --- |
| Source | An earlier stage of the same pipeline | The stage whose last image is cut. A description names it with a `StageHandle`, and validation turns that into the source's `StageId` (`stage-inputs.md`). |
| Tile width | 4 to 4096, a multiple of 4 | The width W of the tile's box. The height H is W ÷ 2 (`isometric-geometry.md`). The output is W × H. |
| Left | 0 to 4095, whole numbers | The x in the source of the box's left column. |
| Top | 0 to 4095, whole numbers | The y in the source of the box's top row. |

- **The position is the box's top-left corner,** not the diamond's centre.
- **No output size.** The output is the tile's box, so no other size can be given or be wrong.
- **Typed parameters.** `CutTileParameters` holds the source's `StageId`, an `IsoTileSize` and a `PixelPoint`. Its internal constructor throws unless each coordinate is from `MinPosition` to `MaxPosition`, 0 to 4095. Only validation builds it from a definition's values. `CutTileParameters.Create` is for constants and tests, and it can describe a box outside any source: that the box fits is a guarantee of the validated pipeline, not of the record.

## Validation

`Pipeline.Validate` checks a cut-tile stage in this order:

1. **The id,** as for every stage.
2. **The source,** through `CheckInput`, which reports a missing, unknown, self or later input (`stage-inputs.md`).
3. **The tile width.** A width outside 4 to 4096 gives an `OutOfRange` finding naming `Parameter.TileWidth`, the value and the range. Only when the width is in range, a width that isn't a multiple of 4 gives a `NotMultipleOf` finding naming `Parameter.TileWidth`, the value and the divisor 4. So a width out of range is never also reported as not a multiple of 4, and −4 and 4100 are reported only as out of range.
4. **Left.** Outside 0 to 4095 gives an `OutOfRange` finding naming `Parameter.Left`.
5. **Top.** Outside 0 to 4095 gives an `OutOfRange` finding naming `Parameter.Top`.
6. **The fit check.**

### The fit check

The tile's box must lie wholly inside its source's output:

    Left + W ≤ the source's width, and Top + H ≤ the source's height

Left and Top are already at least 0. A cut that breaks the rule gets a `TileOutsideSource` finding, giving the box's position, the tile's width and height, and the source's width and height. A tile larger than its source breaks the same rule, so it needs no finding of its own.

- **It runs only when** the source resolved to a stage that was built and the tile width, left and top added no finding: no bad input, and no value out of range or not a multiple of 4. A bad source's findings are reported once, against the source, and a stage with a bad value isn't also told that its tile doesn't fit.
- **A bad id doesn't stop it.** An invalid or repeated id and a box outside the source are separate mistakes, so both are reported, the id's finding first. The stage is still not built. So a `TileOutsideSource` finding is either its stage's only finding or follows that stage's id finding.
- **The source's size.** Validation keeps the output size of each stage it builds (`stage-inputs.md`): a noise stage's width × height, a threshold's source's size, and a cut's W × H box. So a cut that reads a threshold is checked against the noise behind it, and a cut that reads another cut is checked against that cut's box.
- **No overflow.** The sums are computed only once every value is in range, so they are at most 4095 + 4096.
- **Why at validation, not in the run.** A box outside its source is something a person wrote, so it is an expected failure (`railway R1`), and a technique can only yield frames or throw. The run's `ArgumentException` is the check behind it, for a caller that skips validation.

Desktop's `FindingText` renders the two new findings, for example:

- "Stage at index 2 ("tile"): tile width is 30, which is not a multiple of 4."
- "Stage at index 2 ("tile"): the 64 × 32 tile at (0, 40) doesn't fit inside its 64 × 64 source."

What can't be represented after validation:

- a tile width out of range or not a multiple of 4: `IsoTileSize` refuses it;
- a position outside 0 to 4095: `CutTileParameters` refuses it;
- a cut whose source is missing, later or itself (`stage-inputs.md`);
- a cut whose box runs outside its source. Only validation builds a `ValidatedPipeline`, and it builds a `CutTile` only when the box fits.

## Sources

None beyond `isometric-geometry.md`, whose sources and reasoning cover the diamond mask. The cut is a copy through that mask. Nothing was taken from a paper, an article or another program, and no code was copied.

## Decisions and limits

- **The output is the box,** W × H. It is not the source's size and not trimmed to the diamond, because the atlas grid and Tiled expect uniform W × H cells.
- **Transparent black outside,** one exact value, (0, 0, 0, 0), named on `DiamondCut` as `Outside`, so the golden and the tests pin it. The golden codec keeps alpha as 8-bit RGBA.
- **Alpha inside is copied.** The cut changes which pixels exist, not their colour. A translucent source pixel stays translucent.
- **The side columns are always clear.** The diamond's widest rows are W − 2 (`isometric-geometry.md`), so columns 0 and W − 1 are transparent black even where the source is opaque.
- **A top-left position.** A centre would read more naturally for a diamond, but it would be the one centred coordinate in tilegen.
- **No clipping.** A box that runs off its source is a finding, not a tile with a transparent part. Clipping would need no finding, but a typo in Top would give a half-empty tile with nothing to say why.
- **No randomness.** The same source, width and position give the same pixels with any seed.
- **One frame.** One pass over the box is cheap, so it is watched as one step.
- **Names.** The stage is `CutTile` and the technique is `DiamondCut`, so the record and the static class don't clash where both namespaces are in scope, as with `Threshold` and `TerrainThreshold`.
- **Memory.** The working buffer is 4 bytes a pixel, the size of the box: 32 MiB at 4096 × 2048. The source image belongs to the engine, not to the run.

## Where it lives

- `src/Tilegen.Core/Techniques/DiamondCut.cs`: `DiamondCut.Run` and the `Outside` colour.
- `src/Tilegen.Core/Techniques/CutTileParameters.cs`: the typed parameters and the position's range.
- `src/Tilegen.Core/Geometry/`: the tile size and the diamond mask (`isometric-geometry.md`).
- `src/Tilegen.Core/Pipelines/StageDescription.cs`: `CutTileDescription(Id, Source, TileWidth, Left, Top)`, the stage as a definition writes it.
- `src/Tilegen.Core/Pipelines/Stage.cs`: the `CutTile` stage.
- `src/Tilegen.Core/Pipelines/Finding.cs`: `Parameter.TileWidth`, `Left` and `Top`, and the `NotMultipleOf` and `TileOutsideSource` findings.
- `src/Tilegen.Core/Pipelines/Pipeline.cs`: `ValidateCutTile`, and `OutputSize`, which gives each built stage's output size.
- `src/Tilegen.Core/Imaging/ImageSize.cs`: a width and height of at least 1, the output size validation keeps.
- `src/Tilegen.Core/Pipelines/Generation.cs`: `Generation.Run`, which runs a `CutTile` stage on the last image of `Parameters.Source`.
- `src/Tilegen.Desktop/FindingText.cs`: the text for the two findings, and the names "tile width", "left" and "top".
- `src/Tilegen.Desktop/BuiltInPipeline.cs`: the preview's pipeline, whose last stage is the cut described above.
- `tests/Tilegen.Core.Tests/Techniques/CutTileTests.cs`:
  - inside copies and outside clears, on the spec's 16 × 16 image;
  - every pixel inside the diamond equal to the source at the offset, and every other `Outside`;
  - `Outside` is transparent black, and is written even where the source is opaque;
  - source alpha kept;
  - one frame of the tile's size, and a tile filling its source exactly;
  - the seed not mattering, and a whole run drawing nothing from the `Random`;
  - the input unchanged after the run;
  - arguments checked at the call: a missing argument, and a box off the right, off the bottom or wider than the source, while a box touching the far edges is accepted.
- `tests/Tilegen.Core.Tests/Techniques/CutTileProperties.cs`: CsCheck properties over generated sources up to 64 × 64 with random pixels, alpha included, and tile widths and positions whose box fits:
  - determinism;
  - one frame of W × H;
  - inside pixels equal to the source at the offset, and outside pixels equal to `Outside`;
  - the input unchanged after every yielded frame (`testing R9`).
- `tests/Tilegen.Core.Tests/Techniques/CutTileWrapperTests.cs`: the position's bounds, `Create` refusing a position out of range or a missing source or size, and equality.
- `tests/Tilegen.Core.Tests/Imaging/ImageSizeTests.cs`: a width and height of at least 1 kept, and below 1 refused.
- `tests/Tilegen.Core.Tests/Pipelines/PipelineValidationTests.cs`:
  - a negative position, and each position just outside its range;
  - a valid cut reading a noise stage;
  - a width out of range reported once, a width that isn't a multiple of 4, widths just outside the range reported only as out of range, and widths 4 and 4096 accepted;
  - a tile off the bottom, off the right and larger than its source, and a tile touching the source's far edges;
  - the size coming through a threshold, and a bad source not reported again;
  - a cut reading a cut, sized by the first cut's box, and a cut that fits inside the box it reads;
  - the order id, source, tile width, left, top, and `NotMultipleOf` in the tile width's place;
  - a tile outside its source reported after an invalid or a repeated id, and not reported when the tile width has a finding.
- `tests/Tilegen.Core.Tests/Pipelines/PipelineValidationProperties.cs`: generated descriptions include cut stages whose boxes fit the output of an earlier stage, a cut's value pushed out of range gives exactly that finding, and validating twice gives equal results (`stage-inputs.md`).
- `tests/Tilegen.Core.Tests/Pipelines/GenerationTests.cs`: a cut-tile stage gives the same frame through `Generation.Run` as through `DiamondCut.Run` on its source's image, and not on another stage's.
- **The golden,** in `tests/Tilegen.Core.Tests/Goldens/cut-tile/`:
  - `basic.golden.json`, its provenance (`testing R5`):
    - technique `cut-tile`;
    - input `value-noise/basic.png`, the approved value-noise golden, 64 × 64;
    - tile width 64, left 0 and top 16, which cuts the 64 × 32 tile from the middle band of the input, as wide as it;
    - stage seed 20261116, which the provenance format requires and the cut doesn't use;
    - .NET 11.0, and `approvedIn` `isometric-tile-geometry`, the change in which the owner approved it.
  - `basic.png`, 64 × 32: the noise inside the diamond and transparent black outside. The owner approved it by eye on 2026-11-17 (`testing R6`).
  - **Why noise and not the threshold golden.** Grey texture makes it easy to see that the edge pixels were copied rather than cleared. The tile spans the input's full width, so the empty side columns are exercised.
  - **The input is the stored value-noise golden, not a fresh run,** so a change to value noise doesn't move this golden. If the value-noise golden is ever re-recorded, this golden's input changes with it, and both go to the owner.
- **The golden test** is `CutTileGoldenTests.cs`. It reads the input's path, the tile width, left and top from the provenance, decodes the input PNG into an `Image`, runs the technique and compares the one frame.
  - The test project copies `Goldens/` to its output, and the test reads both directories from there.
  - On a failure it writes `basic.actual.png`, and on a mismatch `basic.expected.png` too, to `golden-results/cut-tile/` in the test output.
