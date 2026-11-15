# Spec Delta

## Purpose

Frame playback decides which frame of a timeline is shown, and how the user's controls and the passing of time move it. It works the same whether the run is still generating or has finished.

## ADDED Requirements

### Requirement: Playback is in one of three modes
Playback SHALL always be in exactly one of three modes:
- following the latest frame;
- paused at a frame;
- playing from a frame.

A new run SHALL start in the following mode. In that mode the shown frame SHALL be the newest one on the timeline, and SHALL change as frames are added.

#### Scenario: Following shows the newest frame
- **WHEN** playback is following and the timeline grows from 2 frames to 3
- **THEN** the shown frame is the 3rd

#### Scenario: Nothing is shown before the first frame
- **WHEN** playback is following and the timeline is empty
- **THEN** no frame is shown

### Requirement: Pausing holds a frame
Pausing SHALL hold the frame shown at that moment. Frames added afterwards SHALL NOT change the shown frame.

#### Scenario: Pause while following
- **WHEN** playback is following with 4 frames on the timeline and the user pauses
- **THEN** the 4th frame is shown, and it is still shown after the timeline grows to 6

### Requirement: Stepping moves one frame and pauses
Stepping forward or back SHALL pause at the next or previous frame. It SHALL stay at the first frame when stepping back from the first. It SHALL stay at the last frame when stepping forward from the last.

#### Scenario: Step back from following
- **WHEN** playback is following with 5 frames and the user steps back
- **THEN** playback is paused at the 4th frame

#### Scenario: Step forward from following
- **WHEN** playback is following with 5 frames and the user steps forward
- **THEN** playback is paused at the 5th frame

#### Scenario: Step at the ends
- **WHEN** playback is paused at the 1st frame and the user steps back, or paused at the last frame and the user steps forward
- **THEN** the shown frame is unchanged

### Requirement: Jumping to the first or latest frame
Jumping to the first frame SHALL pause at the 1st frame. Jumping to the latest SHALL return to following the latest frame.

#### Scenario: Jump to first, then latest
- **WHEN** playback is following with 5 frames, the user jumps to the first frame, and then to the latest
- **THEN** after the first jump it is paused at the 1st frame, and after the second it is following and shows the 5th

### Requirement: Playing advances at a fixed interval
While playing, the shown frame SHALL move forward one frame for each whole frame interval of elapsed time. Leftover time SHALL carry over to the next update, so the rate does not depend on how often updates happen. When playing reaches the newest frame, playback SHALL switch to following.

#### Scenario: Elapsed time moves the frame
- **WHEN** playback is playing from the 1st of 10 frames, and time passes in updates totalling two and a half intervals
- **THEN** the 3rd frame is shown, and half an interval is carried over

#### Scenario: The update rate does not matter
- **WHEN** the same total elapsed time is passed as one update, and again as many small updates
- **THEN** both show the same frame

#### Scenario: Reaching the newest frame
- **WHEN** playback is playing at the 9th of 10 frames and more than one interval passes
- **THEN** playback is following and shows the 10th frame

### Requirement: Play starts from where playback is
Playing from a paused frame SHALL play from that frame. Playing from the last frame, or while following, SHALL play from the 1st frame. Playing while already playing SHALL change nothing. With only one frame there is nothing to play, so playback SHALL follow the latest frame.

#### Scenario: Replay from the start
- **WHEN** a completed run's playback is following and the user plays
- **THEN** playback is playing from the 1st frame

#### Scenario: One frame has nothing to play
- **WHEN** the timeline holds 1 frame and the user plays
- **THEN** playback is following and shows that frame

#### Scenario: Resume from a paused frame
- **WHEN** playback is paused at the 3rd of 6 frames and the user plays
- **THEN** playback is playing from the 3rd frame

### Requirement: Controls wait for the first frame
While the timeline is empty, every control SHALL leave playback following the latest frame.

#### Scenario: Pause before any frame
- **WHEN** the timeline is empty and the user pauses, steps, jumps or plays
- **THEN** playback is still following, and the first frame added is shown

### Requirement: Time comes only from the caller
Playback SHALL use no clock of its own. Elapsed time SHALL be passed in by the caller, and the same playback, frame count and elapsed time SHALL always give the same result.

#### Scenario: No time, no movement
- **WHEN** playback is playing and is updated with zero elapsed time
- **THEN** the shown frame does not change
