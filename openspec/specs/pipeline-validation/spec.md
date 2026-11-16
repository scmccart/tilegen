# pipeline-validation Specification

## Purpose
Turns the pipeline description a definition returns into a pipeline tilegen can run, or into the complete list of what is wrong with it, so that the user sees every mistake from one save and nothing invalid is ever run.

## Requirements

### Requirement: A description holds plain values
A pipeline description SHALL be an ordered list of stage descriptions made only of plain values (text, whole numbers and real numbers) and handles to stages of the same description. It holds no behaviour. The set of stage kinds is fixed by tilegen, and a description can choose among them but cannot add one. The stage kinds are noise and threshold.

#### Scenario: A noise stage is described with plain values
- **WHEN** a description is built with one noise stage whose id is "height" and whose parameters are given as numbers
- **THEN** the description holds that stage, in the order given, with exactly those values

### Requirement: Validation yields a runnable pipeline or findings
Validating a description SHALL produce exactly one of two results: a validated pipeline, or a non-empty list of findings. Only a validated pipeline can be run. A description that has findings produces no pipeline.

#### Scenario: A valid description validates
- **WHEN** a description with one noise stage with in-range parameters is validated
- **THEN** the result is a validated pipeline holding that stage with the same id and parameter values

#### Scenario: An invalid description gives findings and no pipeline
- **WHEN** a description with a noise stage whose octave count is 0 is validated
- **THEN** the result is a list of findings, and no validated pipeline is produced

### Requirement: Validation reports every finding in one pass
Validation SHALL check the whole description and report every finding it contains, not only the first. Each finding SHALL name the stage it concerns by its position in the description and, where the stage has one, its id. Findings SHALL be reported in description order.

#### Scenario: Several mistakes are reported together
- **WHEN** a description is validated in which one noise stage has width 0 and octave count 0, and a second stage repeats the first stage's id
- **THEN** the findings include the width, the octave count and the duplicate id, each naming its stage

### Requirement: A pipeline has at least one stage
Validation SHALL report a finding when the description has no stages.

#### Scenario: An empty description is invalid
- **WHEN** a description with no stages is validated
- **THEN** the findings contain exactly one finding, saying the pipeline is empty

### Requirement: Stage ids are present and unique
Every stage SHALL have a non-empty id that contains no whitespace. Ids SHALL be unique within a description, compared exactly (case-sensitive, ordinal). Validation SHALL report an empty or blank id, and each repetition of an id, naming the stage where the id was first used.

#### Scenario: An empty id is reported
- **WHEN** a description is validated with a stage whose id is "" or "  "
- **THEN** the findings include an invalid-id finding for that stage

#### Scenario: A repeated id is reported against its first use
- **WHEN** a description is validated whose stages at positions 0 and 2 both have the id "height"
- **THEN** the findings include a duplicate-id finding for position 2 that names position 0 as the first use

#### Scenario: Ids differing only in case are distinct
- **WHEN** a description is validated with stages whose ids are "height" and "Height"
- **THEN** no duplicate-id finding is reported

### Requirement: Out-of-range parameters are reported
Validation SHALL report each stage parameter whose value is outside its allowed range, naming the parameter, the value and the allowed range. A real-number parameter that is not finite SHALL be reported as out of range. The ranges for noise are given in the value-noise capability.

#### Scenario: An out-of-range value names its range
- **WHEN** a description is validated with a noise stage whose persistence is 1.5
- **THEN** the findings include an out-of-range finding for persistence, giving the value 1.5 and the allowed range

#### Scenario: A non-finite value is out of range
- **WHEN** a description is validated with a noise stage whose persistence is NaN
- **THEN** the findings include an out-of-range finding for persistence

### Requirement: Validation is deterministic and leaves the description unchanged
Validating the same description SHALL always give the same result, with the same findings in the same order. Validation SHALL NOT change the description.

#### Scenario: Validating twice gives the same findings
- **WHEN** the same invalid description is validated twice
- **THEN** both results hold equal findings in the same order, and the description is equal to what it was before

### Requirement: A builder hands out a handle for each stage added
A description SHALL be built by adding stages in order to a pipeline builder. Adding a stage SHALL return a handle that names that stage by its position and the id it was added with. A handle SHALL only be obtainable by adding a stage, so within one builder a stage can name as its input only a stage added before it. A handle used in another description is checked by validation.

#### Scenario: A stage reads an earlier stage through its handle
- **WHEN** a noise stage "height" is added to a builder, and then a threshold stage "terrain" is added with the handle that adding "height" returned
- **THEN** the built description holds "height" then "terrain", and the threshold's source names position 0 with id "height"

### Requirement: A missing input is reported
Validation SHALL report a missing-input finding for each input of a stage that names no stage at all, naming the stage and the input.

#### Scenario: An input left empty
- **WHEN** a description is validated with a threshold stage whose source was left empty
- **THEN** the findings include a missing-input finding for that stage's source

### Requirement: An input from elsewhere is reported
Validation SHALL report an unknown-input finding for each input whose handle names a position that is outside this description or holds a stage with a different id, such as a handle from another builder. The finding SHALL name the stage, the input, and the position and id the handle gave.

#### Scenario: A handle from another builder
- **WHEN** a description whose stage at position 0 is "height" is validated with a threshold whose source is a handle to position 0, id "other", from another builder
- **THEN** the findings include an unknown-input finding for the threshold's source, giving position 0 and id "other"

### Requirement: An input names an earlier stage
Validation SHALL report a finding for each input that names the stage itself, and one for each input that names a later stage. The later-stage finding SHALL give the later stage's position.

#### Scenario: A stage that reads itself
- **WHEN** a description is validated whose only stage is a threshold "t" whose source names position 0, id "t"
- **THEN** the findings include a self-input finding for that stage's source

#### Scenario: A stage that reads a later stage
- **WHEN** a description is validated whose stage at position 0 is a threshold whose source names position 1, and position 1 holds that stage with that id
- **THEN** the findings include a later-input finding for the threshold's source, giving position 1

### Requirement: An input is not reported for its source's own findings
An input that names an earlier stage of the description SHALL NOT be reported, even when that stage has findings of its own. That stage's findings are reported once, against that stage.

#### Scenario: Reading a stage that has a bad parameter
- **WHEN** a description is validated whose noise stage at position 0 has width 0, and a threshold at position 1 reads it through its handle
- **THEN** the findings are exactly one out-of-range finding for the noise stage's width
