# generation-run Specification

## Purpose
A generation run executes a validated pipeline with a run seed, one technique step at a time. It keeps every frame on a timeline so the app can show generation live and replay it.

## Requirements

### Requirement: Only a validated pipeline is run
A run SHALL start only from a validated pipeline and a run seed. A description that has not passed validation SHALL NOT be runnable.

#### Scenario: A validated pipeline starts a run
- **WHEN** a description validates and a run is started from the validated pipeline with a run seed
- **THEN** the run is running and its timeline is empty

### Requirement: Stages run in pipeline order
A run SHALL run the stages in the order of the validated pipeline. Every frame of a stage SHALL come before any frame of the next stage.

#### Scenario: Two stages run one after the other
- **WHEN** a pipeline of a 3-octave noise stage `a` followed by a 2-octave noise stage `b` is run to completion
- **THEN** the timeline holds 5 frames: 3 from `a`, then 2 from `b`

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

### Requirement: A run advances one step at a time
A run SHALL do work only when it is asked to advance. While the run is running, each advance SHALL add exactly one frame to the timeline. The only exception is when no frame remains: that advance SHALL complete the run and add none.

#### Scenario: Each advance adds one frame
- **WHEN** a running run of a 3-octave noise stage is advanced three times
- **THEN** the timeline holds 3 frames and the run is still running

#### Scenario: The advance after the last frame completes the run
- **WHEN** that run is advanced a fourth time
- **THEN** the run is completed and the timeline still holds 3 frames

#### Scenario: Advancing a finished run does nothing
- **WHEN** a completed run is advanced again
- **THEN** it stays completed and its timeline is unchanged

### Requirement: A run can be stopped
Stopping a running run SHALL leave it stopped: later advances add no frame, and the frames already on the timeline are kept. Stopping SHALL NOT be reported as an error. Stopping a completed run SHALL leave it completed.

#### Scenario: Stopping partway through
- **WHEN** a run of a 4-octave noise stage is advanced twice and then stopped
- **THEN** the run is stopped, its timeline holds 2 frames, and a further advance adds none

#### Scenario: Stopping a completed run
- **WHEN** a completed run is stopped
- **THEN** it stays completed

### Requirement: The timeline keeps every frame with its place in the run
Each timeline entry SHALL record the stage it came from and its step number within that stage, counting from 1. Its pixels SHALL be a copy taken when it was yielded, and later steps SHALL NOT change them. The timeline SHALL be readable but not writable by its readers.

#### Scenario: Entries name their stage and step
- **WHEN** a pipeline of a 2-octave stage `a` followed by a 2-octave stage `b` is run to completion
- **THEN** the entries are, in order, (`a`, 1), (`a`, 2), (`b`, 1) and (`b`, 2)

#### Scenario: Kept frames survive later steps
- **WHEN** a 3-octave noise stage is run to completion
- **THEN** each timeline entry equals the corresponding frame of the technique run alone, copied as it was yielded

### Requirement: A run is deterministic
Two runs of the same validated pipeline with the same run seed SHALL produce timelines of the same length, with the same stage and step for each entry and the same pixels, on the same .NET version and platform.

#### Scenario: Same pipeline and seed, same timeline
- **WHEN** the same validated pipeline is run to completion twice with run seed 42
- **THEN** the two timelines are equal entry for entry, pixels included

#### Scenario: A different run seed changes the frames
- **WHEN** the same pipeline of one 64 by 64 noise stage is run with run seeds 1 and 2
- **THEN** the last frames of the two runs differ

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
