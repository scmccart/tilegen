# preview-window Specification

## Purpose
The preview window is where the author watches a pipeline generate and replays it step by step. Until definitions exist, it runs a pipeline description written in the app's own code. Its behaviour is checked by hand, because tilegen has no GUI tests.

## Requirements

### Requirement: The window runs its pipeline on launch
On launch, the window SHALL validate its built-in pipeline description. If the description is valid, the window SHALL start a run with the starting seed. The run SHALL advance while the window is open, and the newest frame SHALL be shown as it arrives. The built-in description SHALL have at least two stages.

#### Scenario: Launch shows generation live
- **WHEN** the app is launched
- **THEN** the window shows the frames of the first stage and then the second as they are generated, and the run is reported as completed at the end

### Requirement: Live generation keeps the replay's pace
The run SHALL advance one step for each whole frame interval of elapsed time, the same interval playback uses. Leftover time SHALL carry over to the next update, so generating and replaying move at the same rate, whatever the update rate.

#### Scenario: Generation keeps the replay's pace
- **WHEN** a run is generating, and time passes in updates totalling two and a half frame intervals
- **THEN** two steps have been taken, and half an interval is carried over

#### Scenario: Restart is watchable
- **WHEN** the user restarts a completed run
- **THEN** the frames arrive one per frame interval, at the same rate as a replay

### Requirement: An invalid description shows its findings
If the built-in description does not validate, the window SHALL NOT start a run. It SHALL list every finding as a line of text for a person, naming the stage and what is wrong.

#### Scenario: A bad built-in description
- **WHEN** the built-in description is temporarily edited to give a stage a negative width and an empty id, and the app is launched
- **THEN** no frame is shown, and the window lists one line for the empty id and one for the out-of-range width

### Requirement: The shown frame is labelled
Alongside the shown frame, the window SHALL show:
- the shown frame's stage id and its step within that stage;
- its position on the timeline and the timeline's length;
- the run seed;
- whether the run is running, completed or stopped;
- the playback mode.

#### Scenario: Labels follow the shown frame
- **WHEN** the user steps back across the boundary between the two stages
- **THEN** the stage id, step and position labels change to describe the frame now shown

### Requirement: Frames are shown sharp and fitted
The shown frame SHALL be drawn centred in the space left beside the controls. It SHALL be scaled by the largest whole-number factor that fits, and by at least 1. Each frame pixel SHALL be a sharp square, with no smoothing between pixels. The window SHALL be resizable, and the frame SHALL be fitted again when the window's size changes.

#### Scenario: Resize refits the frame
- **WHEN** the window is made larger and then smaller
- **THEN** the frame grows and shrinks in whole-number steps, stays centred, and its pixels stay sharp

### Requirement: Playback controls
The window SHALL offer controls to:
- play or pause;
- step back and step forward;
- jump to the first frame;
- jump to the latest frame.

Each control SHALL act as frame playback defines.

#### Scenario: Replay a finished run
- **WHEN** a run has completed and the user presses play
- **THEN** the frames are replayed from the first at a steady rate, and the window returns to showing the latest frame at the end

#### Scenario: Step through frames
- **WHEN** the user pauses and steps back and forward
- **THEN** one frame changes per press, and the labels follow

### Requirement: Restart and next seed
The window SHALL offer a control that restarts the run with the same seed, and one that starts a new run with the next seed. Either control SHALL stop the current run, empty the timeline, and return playback to following the latest frame.

#### Scenario: Restart gives the same images
- **WHEN** the user restarts a completed run
- **THEN** the same frames are generated again, and the seed label is unchanged

#### Scenario: Next seed gives new images
- **WHEN** the user moves to the next seed
- **THEN** the seed label shows the seed plus one, and different frames are generated
