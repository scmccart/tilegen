# Isometric geometry

## What it does

Defines the shape of one isometric tile and how tiles sit next to each other. A tile is a 2:1 diamond inside a box of W × H pixels, with H = W ÷ 2. The geometry says which pixels of the box are inside the diamond, where each tile of the isometric layout puts its box, and why neighbouring diamonds meet with no gap and no overlap.

It is a set of pure values and functions in `Tilegen.Core.Geometry`, with no technique and no frames. The cut-tile technique uses the mask to decide which pixels to copy (`cut-tile.md`). Later changes use the layout for the tile atlas and for edge matching between neighbours.

Everything here depends on the tile's width alone. There is no position, source image or seed in it.

## How it works

### The tile size

A tile's size is one number, its width W. `IsoTileSize` accepts a whole W from 4 to 4096 that is a multiple of 4. The height is W ÷ 2, computed on request and never stored, so a tile that is not 2:1 can't be written down.

**Why a multiple of 4, not just even.** Tiles in the layout sit at offsets of (±W/2, H/2) from their neighbours. Those are whole pixels only when H is even, and with W = 2H that means W is a multiple of 4. Take W = 6, so H = 3, as the smallest example:

- H is odd, so there is one middle row instead of two, and it is the full 6 pixels wide, not W − 2. Counting by hand, the rows are 2, 6 and 2 pixels, 10 in all, where W · H ÷ 2 would give 9.
- The neighbour's half-height offset is 1.5 pixels, which falls between pixels.

During the proposal a script found the same extra pixel at widths 10 and 30. These widths are in range and even, so the multiple-of-4 rule is its own check, reported on its own (`IsoTileSize.IsMultipleOfStep`, apart from `IsInRange`).

### The pixel-centre rule

A pixel (x, y) of the box, counted from the top-left with x to the right and y down, has its centre at (x + ½, y + ½). The diamond's four corners are the midpoints of the box's four sides: (W/2, 0), (W, H/2), (W/2, H) and (0, H/2). A pixel is inside when its centre is inside that diamond:

    |x + ½ − W/2| / (W/2) + |y + ½ − H/2| / (H/2) ≤ 1

Multiplying through by W, and using W = 2H, gives a test in whole numbers:

    |2x + 1 − W| + 2 · |2y + 1 − H| ≤ W

`DiamondMask.Contains(x, y)` is exactly this line, in `int` arithmetic. There is no floating point and no rounding.

### Why there are no ties

A tie would be a pixel centre exactly on the diamond's edge: the left side equal to W. It can't happen.

- W is even, so 2x + 1 − W is odd, and its absolute value is odd.
- The second term, 2 · |2y + 1 − H|, is twice a whole number, so it is even.
- An odd number plus an even number is odd. W is even. The sum never equals W.

So every pixel centre is strictly inside or strictly outside, and no rule is needed for the edge. This holds for every tile in the layout too, because the layout's offsets are whole pixels: a tile's pixel centres are on the same half-integer lattice as every other tile's. The multiple-of-4 rule is not what rules out ties. It is what keeps H even, and with it the two middle rows, the offsets and the count below.

This argument, and the choice of the pixel-centre rule, are tilegen's own reasoning. They are not taken from a source (see Sources).

### The rows

Count rows from the nearer of the top and bottom edges, starting at 0, so d = min(y, H − 1 − y). In the top half, 2y + 1 − H = −(H − 1 − 2y), and with W = 2H the test becomes |2x + 1 − W| ≤ 4y + 2, which is 4d + 2 there. The mirror image holds for the bottom half. The values 2x + 1 − W are odd, so the pixels that pass are one unbroken run:

- Row d is **4d + 2 pixels wide**: 2, 6, 10, 14, and so on.
- The run starts at x = W/2 − (4d + 2)/2, which is centred in the box. Each row is two pixels wider on each side than the one above it, the usual 2:1 pixel line.
- H is even, so the widest rows are the two in the middle, where d = H/2 − 1. Each is 4(H/2 − 1) + 2 = 2H − 2 = **W − 2** pixels wide. Columns 0 and W − 1 are empty in every row. The tips of the left and right neighbours fill them.
- The count is the sum of the row widths: exactly **W · H ÷ 2**, half the box. For 64 × 32 it is 1024.

`DiamondMask.Row(y)` returns this run as a `RowSpan(First, Width)`, for callers that work a row at a time.

### The 8 × 4 picture

The smallest diamond with two rows of steps, `#` inside and `.` outside:

    ...##...
    .######.
    .######.
    ...##...

The widths are 2, 6, 6 and 2. The middle rows are 6 = W − 2, columns 0 and 7 are empty, and 16 pixels are inside, which is 8 × 4 ÷ 2. A 16 × 8 tile has rows of 2, 6, 10, 14, 14, 10, 6 and 2.

### The layout

Tiles are placed on a diamond grid. A tile's coordinate is `TileCoordinate(I, J)`. **I runs down and to the right, J runs down and to the left**, and one step in either moves the box by half a tile in each direction. The top-left corner of the box of tile (i, j) is the pixel

    ((i − j) · W/2, (i + j) · H/2)

`IsoLayout.Origin` computes it, and returns a `PixelPoint`. For a 64 × 32 tile:

| Tile | Box's top-left pixel |
| --- | --- |
| (0, 0) | (0, 0) |
| (1, 0) | (32, 16) |
| (0, 1) | (−32, 16) |

A `PixelPoint` may be negative: origins are negative for i < j. A `TileCoordinate` and a `PixelPoint` are both pairs of `int`s and are separate types so that one can't be passed for the other. The arithmetic is `checked`, so a coordinate far enough out to overflow throws an `OverflowException` instead of wrapping.

**Why the tiles fit.** Draw each tile's diamond in the plane, not on pixels. Its centre is the centre of its box, and the diamonds' centres form a lattice generated by (W/2, H/2) and (−W/2, H/2). Those two vectors are the diamond's own edge directions, so the diamonds are translates that meet edge to edge and cover the plane, sharing only their boundaries. No pixel centre is on a boundary (no ties), so each pixel centre is strictly inside exactly one diamond. That is the claim: each pixel belongs to exactly one tile. The tests check it by counting (see Where it lives), not by proof.

## Parameters

The one input is the tile width.

| Parameter | Range | Meaning and effect |
| --- | --- | --- |
| Tile width W | 4 to 4096, a multiple of 4 | The box's width. The height is W ÷ 2, the diamond's corners are the midpoints of the box's sides, and half of the box's pixels are inside. |

- **Typed.** `IsoTileSize` has an internal constructor that throws unless the width is in range and a multiple of 4. `IsoTileSize.Create` is for constants and tests. Values from a definition are checked by validation, which reports the two failures as different findings (`cut-tile.md`).
- **`DiamondMask`** is made from a size and nothing else. A null size throws. `Contains` and `Row` throw `ArgumentOutOfRangeException` for a pixel or row outside the box, which is a bug, not an outcome.
- **`IsoTileSize.Min`, `Max` and `WidthStep`** (4, 4096, 4) are public constants, so validation and the messages use the same numbers.

## Sources

- **Tiled's manual, "Editing Maps",** https://doc.mapeditor.org/en/stable/manual/maps/. Its text says that for an isometric map the map orientation controls "how the tile grid is projected and how tiles are positioned relative to each other", and that "Tiles are still arranged in a grid, but are drawn as diamonds", and that Tiled does not transform the art, so the user must provide diamond-shaped tiles. Tilegen cites it only for the fact that an isometric map's tiles are diamonds that the tile art must already be.
- **Tiled's TMX map format, the map element's `orientation` attribute,** https://doc.mapeditor.org/en/stable/reference/tmx-map-format/. It lists "isometric" as one of the orientations Tiled supports. Tilegen cites it only as the name of the orientation the exported tileset is for.
- **What those pages do not say.** Neither page gives a 2:1 ratio, which direction a tile's x and y coordinates run, or where an isometric tile's image is placed in the grid. The JSON map format page (https://doc.mapeditor.org/en/stable/reference/json-map-format/) was also read and is the same: it lists `isometric` and says nothing of the layout. So nothing in the geometry above is taken from Tiled's documentation.
- **Tilegen's own reasoning:**
  - the 2:1 ratio and the multiple-of-4 rule;
  - the pixel-centre rule and its no-ties argument;
  - the layout's origin formula and the direction of i and j;
  - the row widths, the W − 2 middle rows and the W · H ÷ 2 count.

  Whether Tiled places a tile image on its grid exactly as this layout does is not checked here. It is checked when the tileset is written and the terrain brush is tried in Tiled by hand (a later change in `docs/roadmap.md`). If it differs, the layout or the atlas cell gains an offset there, and the mask doesn't change.
- **No article on 2:1 pixel-art tiles is cited.** No code was copied from any source, and Tiled's source code was not read.

## Decisions and limits

- **Pixel centres, not a full-width diamond.** The common hand-drawn 2:1 diamond has rows of 4, 8, …, W, W, …, 8, 4. At 64 × 32 that is 1088 pixels, where the area allows 1024. It has 2H extra pixels, so neighbours overlap along their edges, and "no overlap" fails by construction. The pixel-centre rule is the symmetric rule that tiles exactly. The cost is a shape that surprises: the widest rows are W − 2, not W.
- **No half-open rule.** Taking the left and top edges in and the right and bottom out needs a tie-break that pixel centres never meet, and it is not symmetric.
- **No sampling at pixel corners.** It is asymmetric and has a tie on every edge.
- **One width, no height.** The height is derived, so there is no ratio finding. A separate height would make "not 2:1" a state to report.
- **No other ratios.** Only 2:1 is supported. Other ratios, and every projection other than this one, are deferred.
- **No tile height.** A tile has no extent above its diamond: no cliffs, no walls, no stacking. Tiles with height are deferred.
- **Computed, not stored.** The mask is a size and two formulas, not a grid of bools that could disagree with them. `Row` exists because the cut works a row at a time, and a property keeps it in step with `Contains`.
- **Size extremes.** A width of 4 is a 4 × 2 box whose two rows are 2 pixels wide each. The smallest tile with two rows of steps is 8 × 4. At 4096 the box is 4096 × 2048, which is 32 MiB at 4 bytes a pixel, far larger than a tile needs.
- **No tile coordinate range.** `TileCoordinate` takes any `int`, and `Origin` throws on overflow rather than limiting the range.

## Where it lives

- `src/Tilegen.Core/Geometry/IsoTileSize.cs`: the size, its range and step, and the derived height.
- `src/Tilegen.Core/Geometry/DiamondMask.cs`: `Contains` and `Row`.
- `src/Tilegen.Core/Geometry/RowSpan.cs`: a row's first pixel and width.
- `src/Tilegen.Core/Geometry/IsoLayout.cs`: `Origin`.
- `src/Tilegen.Core/Geometry/TileCoordinate.cs` and `PixelPoint.cs`: the two point types.
- `tests/Tilegen.Core.Tests/Geometry/IsoTileSizeTests.cs`:
  - `The_range_and_step_are_published_as_constants`;
  - `A_width_in_range_and_a_multiple_of_four_is_accepted` for 4, 64 and 4096;
  - `A_width_out_of_range_or_not_a_multiple_of_four_is_refused` for 0, 2, 30 and 4100;
  - `The_two_predicates_report_range_and_step_separately`;
  - `The_height_is_half_the_width`.
- `tests/Tilegen.Core.Tests/Geometry/DiamondMaskTests.cs`, the examples:
  - `The_mask_of_an_eight_by_four_tile_is_the_smallest_diamond_with_two_rows_of_steps`, the picture above written out literally;
  - `The_rows_of_a_sixteen_by_eight_tile_grow_by_two_pixels_on_each_side`;
  - `A_sixty_four_by_thirty_two_tile_has_its_tips_in_the_middle_and_empty_side_columns`;
  - `The_two_middle_rows_of_a_sixty_four_by_thirty_two_tile_are_sixty_two_pixels_wide`;
  - `A_sixty_four_by_thirty_two_tile_has_1024_pixels_inside`;
  - `Contains_outside_the_box_throws` and `Row_outside_the_box_throws`;
  - `A_null_size_is_refused`.
- `tests/Tilegen.Core.Tests/Geometry/IsoLayoutTests.cs`: the origins of (0, 0), (1, 0) and (0, 1) for 64 × 32 (`The_tile_at_zero_zero_sits_at_the_origin`, `The_tile_at_one_zero_sits_half_a_width_right_and_half_a_height_down`, `The_tile_at_zero_one_sits_half_a_width_left_and_half_a_height_down`), and `A_coordinate_far_enough_out_to_overflow_throws`.
- `tests/Tilegen.Core.Tests/Geometry/DiamondMaskProperties.cs`: CsCheck properties over widths 4 to 256 in steps of 4, 64 iterations each:
  - `The_mask_is_the_same_mirrored_left_to_right_and_top_to_bottom`;
  - `Exactly_half_the_pixels_of_the_box_are_inside`;
  - `Each_row_is_one_contiguous_run_that_agrees_with_Row`, which also checks the 4d + 2 width and the centred start;
  - `Tiles_placed_by_the_layout_cover_every_pixel_around_the_origin_tile_exactly_once`: tiles are placed with `IsoLayout.Origin` at every coordinate with |i| and |j| at most 3, and each pixel of the 3W × 3H region around the origin tile's box, found through `Contains`, is covered exactly once. The region is well inside the placed tiles, so no pixel there depends on a tile that is missing.
- There are no goldens: the geometry draws nothing. The cut's golden, which uses it, is described in `cut-tile.md`.
