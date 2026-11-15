# Spec Delta

## Purpose

The value-noise technique generates a grey image of smooth random variation, summed over octaves, as a source stage for later techniques such as thresholding terrain. Generation is shown step by step, one octave at a time.

## ADDED Requirements

### Requirement: Noise parameters and their ranges
A noise stage SHALL take these parameters, and validation SHALL report any value outside its range:
- width and height, in pixels: 1 to 4096;
- octaves: 1 to 12;
- cell size, the lattice spacing of the first and coarsest octave, in pixels: 1 to 4096;
- persistence, the amplitude of each octave relative to the one before: greater than 0, at most 1.

#### Scenario: Values at the edges of their ranges are accepted
- **WHEN** noise stages with width 1, width 4096, octaves 1, octaves 12 with cell size 2048, persistence 1 and persistence 0.0001 (with the other parameters in range) are validated
- **THEN** each validates without findings

#### Scenario: Values just outside their ranges are reported
- **WHEN** noise stages with width 0, height 4097, octaves 13, cell size 0 and persistence 0 are validated
- **THEN** each gives an out-of-range finding for that parameter

### Requirement: The finest octave is at least one pixel
Each octave SHALL halve the lattice spacing of the one before. Validation SHALL report a noise stage whose cell size is smaller than 2 raised to the power of (octaves − 1), because its finest octave would be finer than a pixel. This finding SHALL name both parameters and the smallest cell size that would be valid.

#### Scenario: Too many octaves for the cell size
- **WHEN** a noise stage with cell size 8 and octaves 5 is validated
- **THEN** the findings include one that names cell size and octaves, and gives 16 as the smallest valid cell size

#### Scenario: The finest octave is exactly one pixel
- **WHEN** a noise stage with cell size 8 and octaves 4 is validated
- **THEN** no finding is reported for that combination

### Requirement: One frame per octave
Running a noise stage SHALL yield exactly one frame per octave, in order from the coarsest. Frame k SHALL be a complete image of octaves 1 to k summed and normalised by the sum of their amplitudes. The last frame SHALL be the stage's output. Every frame SHALL have the stage's width and height.

#### Scenario: Four octaves give four frames
- **WHEN** a noise stage with 4 octaves, width 64 and height 32 is run
- **THEN** it yields 4 frames, each 64 by 32 pixels

#### Scenario: The first frame is the first octave alone
- **WHEN** a noise stage with 3 octaves is run
- **THEN** its first frame equals the output of the same stage with 1 octave, run with the same seed

### Requirement: Output is opaque grey
Every pixel of every frame SHALL be grey: red, green and blue equal, and alpha fully opaque (255). The grey level SHALL be the normalised noise value in [0, 1] scaled to 0 to 255 and rounded to the nearest whole number.

#### Scenario: Every pixel is opaque grey
- **WHEN** a noise stage is run with any valid parameters and seed
- **THEN** every pixel of every frame has equal red, green and blue, and alpha 255

### Requirement: Noise is deterministic and seeded
Running a noise stage with the same validated parameters and the same stage seed SHALL yield the same frames, pixel for pixel, on the same .NET version and platform. Its only source of randomness SHALL be the random number generator seeded from its stage seed.

#### Scenario: Same seed, same pixels
- **WHEN** the same noise stage is run twice with the same stage seed
- **THEN** both runs yield the same number of frames, and corresponding frames are equal pixel for pixel

#### Scenario: Different seeds give different images
- **WHEN** a noise stage of width 64 and height 64 is run with two different stage seeds
- **THEN** the two outputs differ

### Requirement: Generation can stop after any frame
A noise run SHALL be safe to abandon after any frame, holding nothing that needs releasing. A frame SHALL be readable until the next frame is requested, and a caller that keeps a frame SHALL be able to copy it.

#### Scenario: A kept copy is unaffected by later steps
- **WHEN** a copy of the first frame of a 3-octave run is taken, and the run then continues to its end
- **THEN** the copy still equals the first frame as it was yielded

### Requirement: Noise matches its approved golden
The output of a fixed noise stage and seed SHALL equal, pixel for pixel, a stored golden image approved by the owner by eye. The golden's parameters, seed, .NET version and approving change SHALL be recorded beside it.

#### Scenario: The golden matches
- **WHEN** the noise stage recorded in the golden's provenance is run with the recorded seed
- **THEN** its output equals the golden image when both are compared as decoded pixels
