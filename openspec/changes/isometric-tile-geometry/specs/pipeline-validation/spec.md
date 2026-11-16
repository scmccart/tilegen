# Spec Delta

## MODIFIED Requirements

### Requirement: A description holds plain values
A pipeline description SHALL be an ordered list of stage descriptions made only of plain values (text, whole numbers and real numbers) and handles to stages of the same description. It holds no behaviour. The set of stage kinds is fixed by tilegen, and a description can choose among them but cannot add one. The stage kinds are noise, threshold and cut tile.

#### Scenario: A noise stage is described with plain values
- **WHEN** a description is built with one noise stage whose id is "height" and whose parameters are given as numbers
- **THEN** the description holds that stage, in the order given, with exactly those values
