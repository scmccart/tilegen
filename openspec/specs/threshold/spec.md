# threshold Specification

## Purpose
The threshold technique turns an earlier stage's image into a two-terrain map: each pixel is one terrain or the other, according to its brightness. It is the first technique that reads another stage's output.

## Requirements

### Requirement: A threshold stage has a source and a level
A threshold stage SHALL have two parameters: its source, which is an earlier stage of the same pipeline, and a level, a whole number from 0 to 255. Validation SHALL report a level outside that range as out of range. A threshold stage SHALL have no width or height of its own.

#### Scenario: A level out of range is reported
- **WHEN** a description is validated with a threshold stage whose level is 256
- **THEN** the findings include an out-of-range finding for the level, giving the value 256 and the range 0 to 255

#### Scenario: A valid threshold stage validates
- **WHEN** a description of a noise stage followed by a threshold stage with level 128 that reads it is validated
- **THEN** the result is a validated pipeline holding both stages, with the threshold reading the noise stage

### Requirement: Brightness at or above the level is the second terrain
The threshold SHALL write each output pixel from the input pixel at the same place. A pixel's brightness is the sum of its red, green and blue values divided by 3, rounded down; alpha is ignored. A pixel whose brightness is at or above the level SHALL become opaque white, which is terrain 1. Every other pixel SHALL become opaque black, which is terrain 0.

#### Scenario: Pixels either side of the level
- **WHEN** a threshold with level 128 reads an image whose pixels are grey 127, grey 128 and grey 255
- **THEN** the output pixels are opaque black, opaque white and opaque white

#### Scenario: Brightness averages the colour channels
- **WHEN** a threshold with level 100 reads a pixel with red 255, green 30 and blue 15, whose brightness is 100
- **THEN** the output pixel is opaque white

#### Scenario: Level 0 makes every pixel the second terrain
- **WHEN** a threshold with level 0 reads any image
- **THEN** every output pixel is opaque white

### Requirement: The output is the input's size, in one frame
A threshold run SHALL yield exactly one frame, of the same width and height as its input. That frame is the stage's output.

#### Scenario: One frame of the input's size
- **WHEN** a threshold reads a 40 by 24 image
- **THEN** the run yields one frame, 40 by 24

### Requirement: The threshold draws no randomness
A threshold run SHALL draw nothing from the random number generator it is given. Its output SHALL depend only on its input's pixels and its level.

#### Scenario: The seed doesn't matter
- **WHEN** the same input is thresholded at the same level with two different stage seeds
- **THEN** the two outputs are equal, pixel for pixel

### Requirement: The threshold leaves its input unchanged
A threshold run SHALL NOT change its input. After each frame it yields, the input's pixels SHALL equal what they were before the run started.

#### Scenario: The input is unchanged after the run
- **WHEN** a threshold run reads an image and is enumerated to the end
- **THEN** the image's pixels are equal to a copy taken before the run

### Requirement: The threshold matches its approved golden
The threshold SHALL reproduce its golden exactly. The golden's input is the approved value-noise golden and its level is 128. The owner approves the golden by eye.

#### Scenario: The golden matches
- **WHEN** the threshold reads the value-noise golden image at level 128
- **THEN** its frame equals the threshold golden, pixel for pixel
