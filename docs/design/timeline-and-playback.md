# Timeline and playback

## What it does

A generation run steps a validated pipeline one technique step at a time and keeps a copy of every frame on a timeline. Playback decides which frame of that timeline the preview shows, and how the user's controls and the passing of time move it. Playback works the same whether the run is still generating or has finished: it sees only how many frames the timeline holds.

Playback is a value in Core with pure transition functions. Nothing in it reads a clock, and nothing in it knows about the window. The preview passes in the frame count and the elapsed time, and draws the frame that playback says is shown.

## How it works

### The run

_To be written with the generation run (`engine-preview`, task 3.4): stage order, one `Random` per stage, one frame per advance, and completing and stopping._

### The timeline

_To be written with the timeline (`engine-preview`, task 3.4): what is kept and its memory cost, and why entries are read-only._

### Playback

#### The modes

`Playback` is a closed type with exactly one of three cases, so no pair of flags such as "paused" and "live" can disagree:

- **`FollowingLatest`:** shows the newest frame, which changes as frames are added. A new run starts here.
- **`Paused(Index)`:** holds one frame. Frames added later don't change what is shown.
- **`Playing(Index, Carry)`:** moves forward one frame per frame interval of elapsed time. `Carry` is the time since the last move.

Indexes are 0-based positions on the timeline. `Paused` and `Playing` throw on a negative index, and `Playing` throws on a carry below zero or of a whole interval or more. Their properties are get-only, so a `with` expression can't skip those checks.

#### What is shown

`PlaybackRules.Shown(playback, frameCount)` gives a closed `ShownFrame`:

- `NoFrame` when following an empty timeline. Absence is a case, not a null or a −1.
- Otherwise `FrameAt(Index)`: the newest frame (`frameCount − 1`) while following, or the paused or playing index.

#### The controls

`PlaybackRules.Apply(playback, command, frameCount)` gives the playback after the user presses a control. The commands are the `PlaybackCommand` enum: `Play`, `Pause`, `StepBack`, `StepForward`, `First` and `Latest`. With `last = frameCount − 1`:

| From | Play | Pause | StepBack | StepForward | First | Latest |
| --- | --- | --- | --- | --- | --- | --- |
| `FollowingLatest` | `Playing(0, 0)`, or unchanged when `last == 0` | `Paused(last)` | `Paused(max(last − 1, 0))` | `Paused(last)` | `Paused(0)` | unchanged |
| `Paused(i)` | `Playing(i == last ? 0 : i, 0)`, or `FollowingLatest` when `last == 0` | unchanged | `Paused(max(i − 1, 0))` | `Paused(min(i + 1, last))` | `Paused(0)` | `FollowingLatest` |
| `Playing(i, c)` | unchanged | `Paused(i)` | `Paused(max(i − 1, 0))` | `Paused(min(i + 1, last))` | `Paused(0)` | `FollowingLatest` |

In words:

- **Pause** holds the frame shown at that moment.
- **Step back and step forward** pause one frame away, and stop at the first and the last frame.
- **First** pauses at the first frame. **Latest** returns to following.
- **Play** plays from a paused frame. From the last frame, or while following, it plays from the first frame, so a finished run replays from the start. Playing while already playing changes nothing.
- **With only one frame there is nothing to play,** so Play gives `FollowingLatest`.
- **Pausing or stepping while playing drops the carry.** Play always starts with a carry of zero.

Two rules take precedence over the table:

- **An index at or past the frame count is a bug.** `Shown`, `Apply` and `Advance` all throw on one, and they check it before any rule applies, even when the command would have dropped the index. On an empty timeline, then, only `FollowingLatest` is a valid input, and every command leaves it unchanged. The empty row is not in the table because the table's `last` would be −1.
- **No transition produces `Playing` at the last frame.** Play on a one-frame timeline gives `FollowingLatest`, and `Advance` turns reaching the last frame into `FollowingLatest`. The timeline only grows within a run, so a reachable `Playing(i)` always has `i < last`.

#### The interval and the carry

`PlaybackRules.Advance(playback, elapsed, frameCount)` gives the playback after `elapsed` has passed. Only `Playing` moves. `FollowingLatest` and `Paused` are returned unchanged.

`Playing(i, c)` moves forward ⌊(c + elapsed) / interval⌋ frames, and keeps the remainder as its new carry. The interval is `Playback.FrameInterval`, 250 ms. For example, playing from the first of 10 frames with a carry of zero, updates totalling two and a half intervals reach the third frame (index 2) with half an interval carried.

The arithmetic is in whole ticks, so it is exact. The same total elapsed time gives the same playback whether it arrives in one update or many, and the replay rate doesn't depend on the window's frame rate. Elapsed time is divided by the interval before the carry is added, so even `TimeSpan.MaxValue` can't overflow. The carry and the remainder together are less than two intervals.

#### Following the latest frame

When playing reaches the last frame, playback becomes `FollowingLatest`, and the carry is dropped. The preview then keeps showing the newest frame as the run adds more, as it does at the start of a run. Playing past the end in one long update, such as after the window was stalled, also ends there.

## Inputs

Playback has no parameters. Its inputs, all passed in by the caller, are:

- **The frame count:** how many frames the timeline holds, 0 or more. A negative count throws.
- **The command,** for `Apply`.
- **The elapsed time,** for `Advance`, 0 or more. A negative time throws. It is a `TimeSpan` argument: Core reads no clock.

The frame interval is a Core constant, not a parameter (see Decisions and limits).

## Sources

There is no published algorithm. The modes and the transition table were designed for tilegen in the `engine-preview` change (design D4). No code was copied.

## Decisions and limits

- **Playback is a Core value, not state in Desktop.** Which frame is drawn is a decision, so it belongs in Core. Kept in Desktop, it would also be state the window holds.
- **The interval is fixed at 250 ms,** because how fast replay goes changes what is drawn when. Making it configurable is out of scope.
- **A bad index throws instead of being clamped.** Within a run the timeline only grows, and a new run resets playback, so an index past the end means the caller passed the wrong count or a stale playback. Clamping would hide that.
- **A negative frame count or elapsed time also throws.** Design D4 doesn't mention either. Both can only come from a bug, and splitting elapsed time only gives the same result when no part is negative.
- **A `Playing` at the last frame is not checked for.** No transition produces one, so it isn't rejected on input either. Passed in directly, it behaves as the table and `Advance` say: `Advance` turns it into `FollowingLatest`, even with zero elapsed time.
- **The run's limits** (the UI thread, an unbounded timeline, one step per update) are written up with the run and the timeline (`engine-preview`, task 3.4).

## Where it lives

- `src/Tilegen.Core/Replay/Playback.cs`: `Playback`, `FrameInterval`, `FollowingLatest`, `Paused` and `Playing`.
- `src/Tilegen.Core/Replay/PlaybackCommand.cs`: the commands.
- `src/Tilegen.Core/Replay/ShownFrame.cs`: `ShownFrame`, `NoFrame` and `FrameAt`.
- `src/Tilegen.Core/Replay/PlaybackRules.cs`: `Shown`, `Apply` and `Advance`.
- The namespace is `Tilegen.Core.Replay`, not `Playback`, because a type named after its own namespace fails to bind (CS0118).
- `tests/Tilegen.Core.Tests/Replay/PlaybackScenarioTests.cs`: one test per scenario in the frame-playback spec.
- `tests/Tilegen.Core.Tests/Replay/PlaybackRulesTests.cs`: every cell of the transition table (both sides of each condition and clamp), the empty-timeline row, the `Advance` rules, and the bad-index, negative-count and negative-time throws.
- `tests/Tilegen.Core.Tests/Replay/PlaybackTypeTests.cs`: switches over `Playback`, `ShownFrame` and `PlaybackCommand` with no discard arm; the constructor checks; the interval.
- `tests/Tilegen.Core.Tests/Replay/PlaybackProperties.cs`: CsCheck properties over playback reached by generated runs of added frames, commands and elapsed times from a new run (0 to 50 frames). The shown frame is `NoFrame` exactly when the timeline is empty, and is otherwise on it. Splitting elapsed time over updates gives the same playback. Any command on an empty timeline gives `FollowingLatest`. Zero elapsed time changes nothing. A reached `Playing` is always before the last frame.
- The run and the timeline: _to be written with task 3.4._
