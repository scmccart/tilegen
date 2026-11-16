# Spec Delta

## Purpose

Defines the isometric tile's shape: how big a tile can be, which pixels of its box belong to its diamond, and where tiles sit so that neighbours fit together exactly. Every technique that works inside a tile, and the atlas and tileset built from tiles, rely on it.

## ADDED Requirements

### Requirement: A tile is a 2:1 diamond whose width is a multiple of 4
A tile's size SHALL be given by its width alone. The width SHALL be a whole number from 4 to 4096 that is a multiple of 4. The tile's height SHALL be half its width. The tile occupies a box of width × height pixels, and its diamond touches the middle of each side of that box.

#### Scenario: The height is half the width
- **WHEN** a tile size of width 64 is made
- **THEN** its height is 32

#### Scenario: A width that isn't a multiple of 4 is not a tile size
- **WHEN** a tile size of width 30 is asked for
- **THEN** no tile size is made

### Requirement: A pixel is inside the diamond when its centre is
A pixel of the tile's box SHALL be inside the diamond exactly when its centre lies inside the diamond whose corners are the midpoints of the box's sides. No pixel centre lies exactly on the diamond's edge for any allowed width, so no pixel needs a tie-break. Which pixels are inside SHALL depend on the tile size alone.

#### Scenario: The smallest diamond with two rows of steps
- **WHEN** the mask of an 8 by 4 tile is read row by row, with `#` inside and `.` outside
- **THEN** the rows are `...##...`, `.######.`, `.######.` and `...##...`

#### Scenario: The tips and the sides of a 64 by 32 tile
- **WHEN** the mask of a 64 by 32 tile is read
- **THEN** the top row holds exactly the pixels at x 31 and 32, the two middle rows are each 62 pixels wide, and no pixel in column 0 or column 63 is inside

### Requirement: Each row of the mask is one centred span
Each row of the mask SHALL be one unbroken run of pixels, centred in the box. Counting rows from the nearer of the top and bottom edges, starting at 0, row r SHALL be 4r + 2 pixels wide.

#### Scenario: Rows grow by two pixels on each side
- **WHEN** the rows of a 16 by 8 tile's mask are measured from the top
- **THEN** their widths are 2, 6, 10, 14, 14, 10, 6 and 2, and each starts at 8 minus half its width

### Requirement: The mask is symmetric
The mask SHALL be the same when mirrored left to right, and the same when mirrored top to bottom.

#### Scenario: Mirrored pixels agree
- **WHEN** any pixel (x, y) of a W by H tile's mask is compared with (W − 1 − x, y) and with (x, H − 1 − y)
- **THEN** all three are inside, or all three are outside

### Requirement: The diamond holds half the box's pixels
The number of pixels inside the mask SHALL be exactly half the number of pixels in the box: width × height ÷ 2.

#### Scenario: Counting a 64 by 32 tile
- **WHEN** the pixels inside a 64 by 32 tile's mask are counted
- **THEN** there are 1024

### Requirement: Tiles in the isometric layout fit with no gap and no overlap
The tile at tile coordinate (i, j) SHALL have its box's top-left corner at pixel ((i − j) × width ÷ 2, (i + j) × height ÷ 2). With every tile placed this way, each pixel of the plane SHALL be inside the diamond of exactly one tile.

#### Scenario: A neighbour's position
- **WHEN** the tile at coordinate (1, 0) is placed for a 64 by 32 tile size
- **THEN** its box's top-left corner is at pixel (32, 16)

#### Scenario: Neighbours cover every pixel once
- **WHEN** tiles of one size are placed at every coordinate around (0, 0), and each pixel of a region well inside them is checked
- **THEN** each pixel is inside the diamond of exactly one tile
