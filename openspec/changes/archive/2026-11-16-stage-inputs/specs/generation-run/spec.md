# Spec Delta

## MODIFIED Requirements

### Requirement: Each stage draws from its own seeded randomness
Each stage SHALL be run with a random number generator that is seeded with that stage's seed. The stage seed is derived from the run seed and the stage id. A stage's frames SHALL equal the frames its technique gives when it is run alone with that stage seed and, for each of its inputs, the last kept frame of the stage that input names.

#### Scenario: A stage's frames match its technique run alone
- **WHEN** a pipeline with stages `a` and `b` is run to completion with run seed 7
- **THEN** the frames of `b` equal, pixel for pixel, the frames of `b`'s technique run alone with the stage seed derived from run seed 7 and id `b`

#### Scenario: Another stage does not change a stage's frames
- **WHEN** stage `b`, which reads no input, is run in a pipeline with `a` before it, and again in a pipeline with a different stage `c` before it, with the same run seed
- **THEN** the frames of `b` are equal in both runs

#### Scenario: A stage with an input matches its technique run alone on that input
- **WHEN** a pipeline of a noise stage `a` followed by a threshold stage `t` that reads `a` is run to completion
- **THEN** the frames of `t` equal, pixel for pixel, the frames of the threshold technique run alone on `a`'s last kept frame with `t`'s level and stage seed

## ADDED Requirements

### Requirement: A stage receives its sources' last kept frames
When a stage starts, the run SHALL give it, for each of its inputs, the last frame kept on the timeline from the stage that input names. Every stage SHALL yield at least one frame, so a source always has a last frame.

#### Scenario: A threshold reads the last octave
- **WHEN** a pipeline of a 3-octave noise stage `a` followed by a threshold stage `t` that reads `a` is run to completion
- **THEN** the input `t` was given equals `a`'s third and last timeline entry, pixel for pixel

### Requirement: A stage leaves its sources' kept frames unchanged
Running a stage SHALL NOT change any frame already on the timeline, including the frames of the stages it reads.

#### Scenario: The source's entries survive the reader
- **WHEN** a pipeline of a noise stage `a` followed by a threshold stage `t` that reads `a` is run to completion
- **THEN** each of `a`'s timeline entries equals, pixel for pixel, a copy taken before `t` started
