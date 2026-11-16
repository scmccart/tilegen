# Tasks

Unless a task says otherwise, every task is verified by:

- `dotnet build`, with 0 warnings;
- `dotnet test -- --minimum-expected-tests 1`, passing.

Group 3 touches Desktop's `FindingText`. That edit is the mechanical one needed to keep the build green after Core's closed `Finding` and `Parameter` sets grow.

## 1. Shared contracts: geometry (Core)

- [ ] 1.1 Add `Tilegen.Core.Geometry` with `PixelPoint(int X, int Y)`, `TileCoordinate(int I, int J)` and `IsoTileSize` (design D1, D3).
  - `IsoTileSize` has `Min` 4, `Max` 4096 and `WidthStep` 4, `IsInRange`, `IsMultipleOfStep`, an internal constructor that throws unless both hold, a computed `Height`, and `Create`.

  Verify with `tests/Tilegen.Core.Tests/Geometry/IsoTileSizeTests.cs`:
  - widths 4, 64 and 4096 are accepted;
  - 0, 2, 30 and 4100 are refused, and `Create` throws for each;
  - the height is width ÷ 2.
- [ ] 1.2 Add `DiamondMask` with `Contains(x, y)` and `Row(y)` returning a `RowSpan(First, Width)`, both computed from the size with the whole-number rule, and both throwing outside the box (design D2). Verify with example tests in `tests/Tilegen.Core.Tests/Geometry/DiamondMaskTests.cs`, one per scenario in `specs/isometric-geometry/spec.md` that is about the mask:
  - the 8 × 4 picture;
  - the 64 × 32 tips, empty side columns, 62-wide middle rows and 1024 pixels;
  - the 16 × 8 row widths and starts;
  - throwing outside the box.
- [ ] 1.3 Add `IsoLayout.Origin(IsoTileSize, TileCoordinate)` returning a `PixelPoint`, with checked arithmetic (design D3). Verify with example tests: for 64 × 32, (1, 0) gives (32, 16), (0, 1) gives (−32, 16), and (0, 0) gives (0, 0).
- [ ] 1.4 Add `tests/Tilegen.Core.Tests/Geometry/DiamondMaskProperties.cs`: CsCheck properties over widths from 4 to 256 in steps of 4 (design D8):
  - symmetry, left to right and top to bottom;
  - the count, exactly W·H/2;
  - rows: each row is one contiguous run that agrees with `Row(y)`;
  - **no gap, no overlap:** tiles placed with `IsoLayout.Origin` at every coordinate with |i|, |j| ≤ 3 cover each pixel of the 3W × 3H region around the origin tile's box exactly once.

  Verify that the properties pass. Then break the rule temporarily, once with `≤ W + 2` in place of `≤ W` and once with the `+ 1` terms dropped, and check that the count and cover properties fail for each. Revert. (Changing `≤` to `<` changes nothing, because no pixel centre lies on an edge: design D2.)
- [ ] 1.5 Create `docs/design/isometric-geometry.md`, following `docs/design/README.md` and the design's "Topic docs" section.
  - Find Tiled's documentation of the isometric orientation and cite the page, checked in its text, for the layout. Cite any article used for the 2:1 pixel convention the same way.
  - Say plainly where the pixel-centre rule and its no-ties argument are tilegen's own reasoning.
  - Copy no code.

  Verify by reading it against 1.1 to 1.4 as built.

## 2. The cut-tile technique (Core)

- [ ] 2.1 Add `CutTileParameters(StageId Source, IsoTileSize Size, PixelPoint Position)`, with an internal constructor that throws unless each coordinate is 0 to 4095, plus `Create`, `MinPosition` and `MaxPosition` (design D4). Verify with wrapper tests: the position bounds, and `Create` throwing outside them.
- [ ] 2.2 Implement `DiamondCut.Run(Image source, CutTileParameters parameters, Random random)` and the named `DiamondCut.Outside` colour (design D4).
  - The call checks its arguments, including that the box fits the source, which throws `ArgumentException`.
  - The one step allocates W × H, copies each row's span from (Left + x, Top + y), writes `Outside` elsewhere, and yields.

  Verify with `tests/Tilegen.Core.Tests/Techniques/CutTileTests.cs`, one test per scenario in `specs/cut-tile/spec.md` apart from the validation and golden ones. That includes no draws from the random: the random's next value equals a fresh same-seed random's first. Add the `ArgumentException` for a box outside the source.
- [ ] 2.3 Add `CutTileProperties.cs`: CsCheck properties over generated sources up to 64 × 64 with random pixels, alpha included, and tile widths and positions that fit (design D8):
  - determinism;
  - one frame of W × H;
  - inside pixels equal the source at the offset, and outside pixels equal `Outside`;
  - the input unchanged after every yielded frame.

  Verify that the properties pass.
- [ ] 2.4 Add the cut-tile golden test, `CutTileGoldenTests.cs`, in the style of `ThresholdGoldenTests.cs`.
  - It decodes `Goldens/value-noise/basic.png` into an `Image`, reads the tile width, left and top from `Goldens/cut-tile/basic.golden.json`, runs the technique, and compares the frame with `Golden`.
  - Write the provenance described in design D8.
  - Run the test once. It reports `Missing` and writes `golden-results/cut-tile/basic.actual.png`.
  - **Stop and show the owner** that image beside its input. Copy it to `tests/Tilegen.Core.Tests/Goldens/cut-tile/basic.png` by hand only after the owner approves it (`testing R6`).

  Verify that the golden test passes after approval.
- [ ] 2.5 Create `docs/design/cut-tile.md`, following `docs/design/README.md` and the design's "Topic docs" section, with the golden and its provenance. Leave a headed placeholder for the validation findings and the fit check, which 3.4 fills. Verify by reading it against 2.1 to 2.4 as built.

## 3. Cut-tile stages and their findings (Core)

- [ ] 3.1 Add the `CutTile(StageId Id, CutTileParameters Parameters)` stage and its arm in `Generation.Run`, which reads `outputs[parameters.Source]` (design D4). Verify that a `GenerationTests` case, building the stage with `CutTileParameters.Create`, gives the same frame through `Generation.Run` as through `DiamondCut.Run` on the same image.
- [ ] 3.2 Add `CutTileDescription(string Id, StageHandle Source, int TileWidth, int Left, int Top)` together with its validation, because `Pipeline.Validate`'s switch has no discard arm. Add:
  - `Parameter.TileWidth`, `Left` and `Top`;
  - `ImageSize`;
  - the `NotMultipleOf` and `TileOutsideSource` findings.

  In `Pipeline.Validate` (design D5):
  - keep each built stage's output size beside `built`, from one private exhaustive switch over `Stage`;
  - implement `ValidateCutTile` in the order id, source, tile width (range, then multiple of 4 only when in range), left, top, then the fit check;
  - run the fit check only when the source was built and the cut added no finding.

  Add Desktop's `FindingText` arms and parameter names (design D7). Verify that `ClosedTypeTests.cs` switches over every `StageDescription`, `Stage`, `Finding` and `Parameter` case with no discard arm.
- [ ] 3.3 Write the validation tests in `PipelineValidationTests.cs`:
  - one test per validation scenario in `specs/cut-tile/spec.md`, each finding asserted as its case (`testing R15`);
  - the order within a cut stage;
  - a cut reading a cut, sized by the first cut's box.

  Extend `PipelineValidationProperties.cs` so that its generated descriptions include cut stages whose boxes fit their sources, and check that its existing properties still hold. Verify that the tests pass.
- [ ] 3.4 Update the docs for validation:
  - `docs/design/stage-inputs.md`: validation keeps each built stage's output size, the fit check is its first use, the "No size checks between inputs" limit points at it, and "Where it lives" lists the new tests;
  - `docs/design/cut-tile.md`: fill the placeholder from 2.5 with the findings, their order and the fit check.

  Verify by reading each against 3.2 and 3.3 as built.
- [ ] 3.5 Update the Enforcement texts that list each technique's properties:
  - `docs/architecture/functional-core.md`, Enforcement, the Tests bullet;
  - `docs/architecture/testing.md`, "Property tests in place";
  - the README's "Enforcement status", "The technique properties".

  Each now names `CutTileProperties.cs` for the cut's determinism and inputs-unchanged properties. Grep `docs/architecture/` and `.claude/rules/` for other text this change makes stale; `illegal-states R7`'s typed units are now partly in place with `TileCoordinate` and `PixelPoint`. Verify by reading each against the code.

## 4. The built-in pipeline (Desktop)

- [ ] 4.1 Change `BuiltInPipeline` to the three stages of design D6, and update its comment. Verify:
  - launch with `TILEGEN_SCREENSHOT` set and `TILEGEN_SCREENSHOT_AT` past the run's end (about 2.25 s for 7 frames at 250 ms), and check that the screenshot shows a single diamond with stepped 2:1 edges;
  - judge the contrast between the black terrain and the panel. If the diamond's outline is hard to see, raise it with the owner before adding a backdrop (design D6);
  - send the PNG to the owner.
- [ ] 4.2 Check the invalid-description path by hand. Temporarily set the cut's `Top` to 100, and check by screenshot that the window lists a tile-outside-source finding as text. Revert. Verify that `git diff src/Tilegen.Desktop/BuiltInPipeline.cs` shows only 4.1's change.
- [ ] 4.3 Update `docs/design/timeline-and-playback.md`:
  - 7 frames and 416 KiB;
  - about 1.75 s to generate and 2.0 to complete;
  - "the built-in 6-frame run" becomes 7;
  - frames on one timeline may differ in size.

  Update `docs/design/threshold.md`: the built-in cut reads its output. Verify by reading each against 4.1.

## 5. Integration check

- [ ] 5.1 Run `dotnet build`, `dotnet test -- --minimum-expected-tests 1` and `openspec validate isometric-tile-geometry --strict`. Check that each topic doc named in design.md's "Topic docs" section matches the code, and that `docs/roadmap.md` row 2 reads "in progress (openspec: isometric-tile-geometry)". Verify that all three commands pass.

## Workflow follow-up

- Open the PR, listing the manual checks from 4.1 and 4.2 (`testing R2`) and the golden approval from 2.4.
- Archive the change inside the PR before it merges. Set roadmap row 2 to "done" and add it to the Done table with its archive folder.
