# Spec Delta

## MODIFIED Requirements

### Requirement: A description holds plain values
A pipeline description SHALL be an ordered list of stage descriptions made only of plain values (text, whole numbers and real numbers) and handles to stages of the same description. It holds no behaviour. The set of stage kinds is fixed by tilegen, and a description can choose among them but cannot add one. The stage kinds are noise and threshold.

#### Scenario: A noise stage is described with plain values
- **WHEN** a description is built with one noise stage whose id is "height" and whose parameters are given as numbers
- **THEN** the description holds that stage, in the order given, with exactly those values

## ADDED Requirements

### Requirement: A builder hands out a handle for each stage added
A description SHALL be built by adding stages in order to a pipeline builder. Adding a stage SHALL return a handle that names that stage by its position and the id it was added with. A handle SHALL only be obtainable by adding a stage, so a stage can name as its input only a stage added before it.

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
