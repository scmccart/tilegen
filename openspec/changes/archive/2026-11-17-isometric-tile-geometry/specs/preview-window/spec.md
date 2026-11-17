# Spec Delta

## MODIFIED Requirements

### Requirement: The window runs its pipeline on launch
On launch, the window SHALL validate its built-in pipeline description. If the description is valid, the window SHALL start a run with the starting seed. The run SHALL advance while the window is open, and the newest frame SHALL be shown as it arrives. The built-in description SHALL be a noise stage, then a threshold stage that reads it, then a cut-tile stage that reads the threshold.

#### Scenario: Launch shows generation live
- **WHEN** the app is launched
- **THEN** the window shows the frames of the first stage, then the second, then the third as they are generated, and the run is reported as completed at the end

#### Scenario: Noise turns into a two-colour map
- **WHEN** the app is launched and the run reaches the threshold's frame
- **THEN** the frame shown is a black-and-white map whose white regions follow the bright regions of the noise frame before it

#### Scenario: The run ends on a diamond tile
- **WHEN** the app is launched and the run completes
- **THEN** the last frame shown is a single 2:1 diamond cut from the black-and-white map, with transparent pixels outside the diamond, scaled up so that its stepped edges are visible

### Requirement: The shown frame is labelled
Alongside the shown frame, the window SHALL show:
- the shown frame's stage id and its step within that stage;
- its position on the timeline and the timeline's length;
- the run seed;
- whether the run is running, completed or stopped;
- the playback mode.

#### Scenario: Labels follow the shown frame
- **WHEN** the user steps back across the boundary between two stages, such as from the tile to the black-and-white map
- **THEN** the stage id, step and position labels change to describe the frame now shown
