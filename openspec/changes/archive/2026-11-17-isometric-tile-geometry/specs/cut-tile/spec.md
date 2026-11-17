# Spec Delta

## Purpose

The cut-tile technique cuts one isometric tile out of an earlier stage's image: the pixels inside the tile's diamond are kept, and everything else in the tile's box is transparent. It is how a generated texture becomes a tile.

## ADDED Requirements

### Requirement: A cut-tile stage has a source, a tile width and a position
A cut-tile stage SHALL have four parameters:
- its source, an earlier stage of the same pipeline;
- a tile width, which sets the tile size as the isometric-geometry capability defines it;
- left and top, the position in the source of the top-left corner of the tile's box, each a whole number from 0 to 4095.

Validation SHALL report a tile width outside 4 to 4096, a left outside 0 to 4095 and a top outside 0 to 4095 as out of range.

#### Scenario: A negative position is reported
- **WHEN** a description is validated with a cut-tile stage whose top is −1
- **THEN** the findings include an out-of-range finding for the top, giving the value −1 and the range 0 to 4095

#### Scenario: A valid cut-tile stage validates
- **WHEN** a description of a 128 by 128 noise stage followed by a cut-tile stage that reads it, with tile width 64, left 0 and top 16, is validated
- **THEN** the result is a validated pipeline holding both stages, with the cut reading the noise stage

### Requirement: A tile width is a multiple of 4
Validation SHALL report a tile width within 4 to 4096 that is not a multiple of 4, giving the value and the divisor 4. A width that is out of range SHALL NOT also be reported as not a multiple of 4.

#### Scenario: A width out of range is reported once
- **WHEN** a description is validated with a cut-tile stage whose tile width is 2
- **THEN** the findings include one out-of-range finding for the tile width, giving the value 2 and the range 4 to 4096, and no not-a-multiple finding

#### Scenario: A width that isn't a multiple of 4 is reported
- **WHEN** a description is validated with a cut-tile stage whose tile width is 30
- **THEN** the findings include a not-a-multiple finding for the tile width, giving the value 30 and the divisor 4

### Requirement: The tile fits inside its source
Validation SHALL report a cut-tile stage whose tile's box doesn't lie wholly inside its source's output: left + tile width greater than the source's width, or top + tile height greater than the source's height. The finding SHALL give the tile's position and size and the source's size.

#### Scenario: A tile that runs off the bottom of its source
- **WHEN** a description is validated with a 64 by 64 noise stage and a cut-tile stage reading it with tile width 64, left 0 and top 40
- **THEN** the findings include a tile-outside-source finding giving the position (0, 40), the tile size 64 by 32 and the source size 64 by 64

#### Scenario: A tile larger than its source
- **WHEN** a description is validated with a 32 by 32 noise stage and a cut-tile stage reading it with tile width 64, left 0 and top 0
- **THEN** the findings include a tile-outside-source finding

### Requirement: The fit is checked against a valid source's output size
The source's output size SHALL be worked out from the source stage: a noise stage's width and height, a threshold's source's size, and a cut tile's box. The fit SHALL be checked only when the source is itself valid and the cut's own parameters gave no finding.

#### Scenario: The size comes through a threshold
- **WHEN** a description is validated with a 64 by 64 noise stage, a threshold reading it, and a cut-tile stage reading the threshold with tile width 128
- **THEN** the findings include a tile-outside-source finding giving the source size 64 by 64

#### Scenario: A bad source is not reported again
- **WHEN** a description is validated whose noise stage has width 0 and whose cut-tile stage reads it with tile width 64
- **THEN** the findings are exactly one out-of-range finding for the noise stage's width

### Requirement: The cut keeps the diamond and clears the rest
The cut's output SHALL be the size of the tile's box, width × height. Each output pixel (x, y) inside the tile's diamond SHALL equal the source pixel (left + x, top + y), alpha included. Each output pixel outside the diamond SHALL be transparent black: red, green, blue and alpha all 0.

#### Scenario: Inside copies, outside clears
- **WHEN** a cut with tile width 8, left 2 and top 3 reads a 16 by 16 image whose pixels are all different
- **THEN** the output is 8 by 4, the pixel at (3, 0) equals the source pixel at (5, 3), and the pixel at (0, 0) is (0, 0, 0, 0)

#### Scenario: Source alpha is kept
- **WHEN** a cut reads an image whose pixels inside the diamond have alpha 100
- **THEN** those output pixels have alpha 100

### Requirement: The cut yields one frame
A cut run SHALL yield exactly one frame, the size of the tile's box. That frame is the stage's output.

#### Scenario: One frame of the tile's size
- **WHEN** a cut with tile width 64 reads a 128 by 128 image
- **THEN** the run yields one frame, 64 by 32

### Requirement: The cut draws no randomness
A cut run SHALL draw nothing from the random number generator it is given. Its output SHALL depend only on its source's pixels, its tile width and its position.

#### Scenario: The seed doesn't matter
- **WHEN** the same image is cut with the same parameters and two different stage seeds
- **THEN** the two outputs are equal, pixel for pixel

### Requirement: The cut leaves its input unchanged
A cut run SHALL NOT change its input. After each frame it yields, the input's pixels SHALL equal what they were before the run started.

#### Scenario: The input is unchanged after the run
- **WHEN** a cut run reads an image and is enumerated to the end
- **THEN** the image's pixels are equal to a copy taken before the run

### Requirement: The cut matches its approved golden
The cut SHALL reproduce its golden exactly. The golden's input is the approved value-noise golden, cut with tile width 64 at left 0 and top 16. The owner approves the golden by eye.

#### Scenario: The golden matches
- **WHEN** the cut reads the value-noise golden image with tile width 64, left 0 and top 16
- **THEN** its frame equals the cut-tile golden, pixel for pixel
