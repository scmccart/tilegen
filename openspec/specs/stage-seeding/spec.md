# stage-seeding Specification

## Purpose
Gives each stage of a run its own random seed, derived from the run seed and the stage's id, so a stage's output depends only on its own description, its inputs and the run seed, and is the same on every launch.

## Requirements

### Requirement: Each stage's seed comes from the run seed and its id
The seed for a stage SHALL be a function of the run seed and the stage's id alone. The run seed is a 32-bit signed whole number. The stage seed is a 32-bit signed whole number, used to seed that stage's random number generator.

#### Scenario: The same inputs give the same seed
- **WHEN** the seed is derived twice for run seed 42 and stage id "height"
- **THEN** both derivations give the same stage seed

#### Scenario: Different ids give different seeds
- **WHEN** seeds are derived for run seed 42 and stage ids "height" and "moisture"
- **THEN** the two stage seeds differ

#### Scenario: Different run seeds give different seeds
- **WHEN** seeds are derived for stage id "height" with run seeds 1 and 2
- **THEN** the two stage seeds differ

### Requirement: A stage's seed does not depend on other stages
A stage's seed SHALL NOT depend on the stage's position in the pipeline or on any other stage. Adding, removing, reordering or editing another stage SHALL leave it unchanged.

#### Scenario: Adding a stage leaves the other seeds alone
- **WHEN** a pipeline with stages "a" and "b" gains a stage "c" before them, with the same run seed
- **THEN** the seeds of "a" and "b" are the same as before

### Requirement: Seeds are stable across launches
The derivation SHALL give the same seed in every process on the same .NET version and platform. It SHALL NOT depend on anything that varies between processes, such as per-process randomized hash codes. It SHALL NOT depend on the current culture. Known pairs of inputs and seeds SHALL be pinned by tests, so that a change in the derivation is caught.

#### Scenario: A pinned seed holds
- **WHEN** the seed is derived for each run seed and stage id in the pinned table
- **THEN** each result equals the seed recorded for it in the table

#### Scenario: Culture does not change a seed
- **WHEN** the seed for a stage id containing non-ASCII text is derived under two different current cultures
- **THEN** both derivations give the same seed
