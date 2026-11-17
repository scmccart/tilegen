# Proposal

## Why

Every later piece of phase one works inside an isometric tile: the transition technique blends terrains inside a diamond, and the atlas and Tiled tileset lay diamonds out on a grid. Today Core has no idea what a tile is. This change gives Core the tile's shape, which pixels it owns, and how tiles fit together, before anything depends on them (`docs/roadmap.md`, change 2).

## What Changes

- **A tile size (Core).** Tiles are 2:1 diamonds. A tile's size is one number, its width, which is a multiple of 4 from 4 to 4096; its height is always half its width. A multiple of 4 is what makes the layout's half-tile offsets whole pixels (owner decision, 2026-11-16).
- **A diamond mask (Core).** A pure value, made from the tile size alone, that says which pixels of the tile's width × height box are inside the diamond. A pixel is inside when its centre is inside the diamond. No pixel centre ever lies on an edge, so there is no tie-break, and the rows are 2, 6, 10, … pixels wide, with the two middle rows width − 2 wide (owner decision, 2026-11-16).
- **The isometric layout (Core).** Where a tile at a tile coordinate sits, in pixels. With it, the masks of neighbouring tiles cover the plane with no gap and no overlap, which property tests check.
- **The cut-tile technique (Core).** It reads one input and cuts one tile out of it: given a tile width and the top-left of the tile's box in the source, it copies the source's pixels inside the diamond and writes transparent black outside. It yields one frame, the size of the tile's box. It has a golden, made from the approved value-noise golden.
- **New findings (Core).** Validation reports, in its single pass:
  - a tile width, left or top out of range, with the existing out-of-range finding;
  - a tile width that is not a multiple of 4;
  - a tile that doesn't fit inside its source. Validation now works out each stage's output size to check this.
- **The built-in pipeline gains a cut (Desktop),** so the preview ends on a single diamond tile of the black-and-white map. Desktop has text for each new finding.

No definition files exist yet, so nothing here breaks anything outside the repo.

## Capabilities

### New Capabilities

- `isometric-geometry`: the tile's size rule, the diamond mask and its edge rule, and the layout under which tiles fit together without gaps or overlaps.
- `cut-tile`: the cut-tile technique. Its parameters and their findings, including a tile that doesn't fit its source; what it copies and what it leaves transparent; its single frame and size; that it draws no randomness and leaves its input unchanged; and its golden.

### Modified Capabilities

- `pipeline-validation`: the stage kinds are now noise, threshold and cut tile.
- `preview-window`: the built-in description is noise, then a threshold that reads it, then a cut that reads the threshold, so the run ends on a diamond tile.

## Out of scope

- **Other ratios and projections.** Only 2:1 diamonds. True isometric angles, hexagons and staggered layouts are deferred (CLAUDE.md, Direction).
- **Tiles with height.** Cliffs and walls are deferred, so the tile is a flat diamond in a width × height box with no extra space above it.
- **Size checks between several inputs.** The cut has one input. A finding for two inputs of different sizes arrives with the first technique that has two inputs, change 4. This change does add the output size of each stage that the check will need.
- **Placing several tiles, a sheet or an atlas.** The cut makes one tile. Sheets come with change 4, and the atlas grid with change 5.
- **Wang sets and transitions** (changes 3 and 4), and **export** (changes 5 and 6).
- **A backdrop behind transparent pixels in the preview.** The window draws a transparent pixel as its panel colour. If the screenshot shows the black terrain too close to that colour, a backdrop is a Desktop-only follow-up, not part of the cut.

## Impact

- **Core:**
  - a new `Tilegen.Core.Geometry` namespace with `IsoTileSize`, `DiamondMask`, `IsoLayout`, `TileCoordinate` and `PixelPoint`;
  - `CutTileDescription`, the `CutTile` stage, `CutTileParameters` and the technique `DiamondCut`;
  - `Parameter.TileWidth`, `Parameter.Left` and `Parameter.Top`, and the findings `NotMultipleOf` and `TileOutsideSource`;
  - `Pipeline.Validate` keeps each built stage's output size, and `Generation.Run` gains an arm for the cut.
- **Engine:** no change. The run already passes each stage the last images of the stages before it, and a frame may change size between stages.
- **Desktop:** `BuiltInPipeline` adds the cut, and `FindingText` covers the new findings and parameters.
- **Tests:**
  - properties and examples for the size, the mask and the layout;
  - cut tests, properties and its golden;
  - validation tests for each new finding;
  - the closed-type test gains the new cases.
- **Docs:**
  - new topic docs `docs/design/isometric-geometry.md` and `docs/design/cut-tile.md`;
  - an update to `docs/design/stage-inputs.md` for the output-size check;
  - row 2 of `docs/roadmap.md`.
- **No change** to the adapters, CI or packages.
