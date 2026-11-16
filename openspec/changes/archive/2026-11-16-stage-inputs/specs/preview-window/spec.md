# Spec Delta

## MODIFIED Requirements

### Requirement: The window runs its pipeline on launch
On launch, the window SHALL validate its built-in pipeline description. If the description is valid, the window SHALL start a run with the starting seed. The run SHALL advance while the window is open, and the newest frame SHALL be shown as it arrives. The built-in description SHALL be a noise stage followed by a threshold stage that reads it.

#### Scenario: Launch shows generation live
- **WHEN** the app is launched
- **THEN** the window shows the frames of the first stage and then the second as they are generated, and the run is reported as completed at the end

#### Scenario: Noise turns into a two-colour map
- **WHEN** the app is launched and the run completes
- **THEN** the last frame shown is a black-and-white map whose white regions follow the bright regions of the noise frame before it
