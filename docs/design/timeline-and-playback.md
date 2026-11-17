# Timeline and playback

## What it does

A generation run steps a validated pipeline one technique step at a time and keeps a copy of every frame on a timeline. Playback decides which frame of that timeline the preview shows, and how the user's controls and the passing of time move it. Playback works the same whether the run is still generating or has finished: it sees only how many frames the timeline holds.

The run and its timeline are in Engine. Playback is a value in Core with pure transition functions. Nothing in it reads a clock, and nothing in it knows about the window. `Preview`, in Engine, holds the current run and its playback. On each update its caller passes in the elapsed time. `Preview` asks Core how many run steps that time is worth, one for each whole frame interval, advances the run that many times, passes playback the frame count and the elapsed time, and reports the frame that playback says is shown. Generating therefore moves at the same rate as Play.

## How it works

### The run

`GenerationRun.Start(pipeline, seed)` takes a `ValidatedPipeline` and a `RunSeed`. Only validation builds a validated pipeline, so a raw description can't be run. The run starts `Running` with an empty timeline, and generates nothing until it is first advanced.

- **Stage order.** Stages run in the validated pipeline's order. Every frame of one stage comes before any frame of the next.
- **One `Random` per stage.** When a stage starts, the run builds `new Random(StageSeed.Derive(runSeed, stage.Id).Value)` and passes it to `Generation.Run`, which hands the stage to its technique. Core derives the seed, and the engine constructs the `Random`, which Core never does. Each stage draws from its own stream. A stage's frames are therefore the frames its technique gives when it is run alone with that stage seed and, for each of its inputs, the last kept image of the stage that input names. A stage before it that it doesn't read doesn't change them. The `Random` is built when the stage starts rather than when the run starts. That changes nothing, because the streams are independent.
- **Outputs and inputs.** The run keeps the last image of every finished stage in a `StageOutputs`, and passes the outputs so far to `Generation.Run` when each stage starts. When a stage's enumerator ends, the run adds that stage's last timeline entry's image, the same `Image` the timeline holds, with no further copy. Which outputs a stage reads is Core's choice, made in `Generation.Run`'s switch; the engine doesn't look (`stage-inputs.md`, In a run).
- **One frame per advance.** `Advance` asks the current stage's enumerator for its next frame. When there is one, the run puts a copy on the timeline (see below) and returns. When the stage has none left, the run disposes the enumerator, adds the stage's last image to the outputs, moves to the next stage and asks again, in the same call. So an advance adds exactly one frame whenever any stage still has one. Between advances the run does nothing: the caller sets the pace.
- **A stage that yields no frame throws.** Every technique yields at least one frame, so every stage has a last image to pass on. A stage that ends without one is a bug, and the run throws `InvalidOperationException` rather than passing on the previous stage's image. It is not a `RunState` case.
- **Completing.** The advance that finds no frame left in any stage completes the run and adds no frame. A run of one 3-octave noise stage gains a frame on each of its first three advances and completes on the fourth.
- **Stopping.** `Stop` turns a running run into a stopped one. Stopping is the engine no longer enumerating: the run disposes the current stage's enumerator and never asks it for another frame. The frames already kept stay. Stopping is not an error, and stopping a completed run leaves it completed.
- **The state** is the closed `RunState`, with the cases `Running`, `Completed` and `Stopped`. `Advance` and `Stop` each switch over it with an arm per case, and change nothing in a completed or stopped run.
- **Determinism.** The same validated pipeline and run seed give the same timeline: the same length, the same stage and step for each entry, and the same pixels, on the same .NET version and platform.

### The timeline

`Timeline` holds a `TimelineEntry` for every frame the run has yielded, in order. An entry records:

- the id of the stage that yielded the frame;
- the frame's step within that stage, counting from 1;
- the frame's pixels, as an `Image`.

A step below 1 throws.

- **Copies.** A frame a technique yields is a view over its working buffer, and is valid only until its next step. The run copies each frame with `Frame.ToImage()` as it is yielded. An `Image` has no way to write its pixels, so an entry's pixels never change, even when a later stage is given the same `Image` as its input.
- **Read-only to its readers.** `Timeline` has a `Count` and an indexer, and nothing else. Only the run that owns it appends, through an internal method. Nothing removes or replaces an entry. Handing out a mutable `PixelBuffer` instead would let any reader, or any stage that reads it, change the history.
- **Everything is kept** for the length of the run. A new run starts with a new timeline. Each frame costs width × height × 4 bytes. The built-in pipeline is a 128 × 128 noise stage of 5 octaves, a threshold that reads it, and a cut that reads the threshold with tile width 128. It keeps 7 frames, 416 KiB: six of 128 × 128 at 64 KiB each, and one of 128 × 64 at 32 KiB. One 4096 × 4096 stage of 12 octaves keeps 768 MiB.
- **Frames on one timeline may differ in size.** Each stage outputs its own size, and each entry's `Image` carries its own width and height. In the built-in run the cut's frame is 128 × 64, after six frames of 128 × 128. Nothing in the engine reads a frame's size. The preview makes a new texture when the size of the frame it shows changes.

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

### The preview

`Preview` holds the current `GenerationRun` and the `Playback` over its timeline, so whatever shows the preview reads that state from Engine and keeps none of its own. Constructed from a validated pipeline and a run seed, it starts a run and follows the latest frame. Each method forwards to the run or to `PlaybackRules`, and decides nothing itself.

- **`Update(elapsed)`** asks `Pacing.Steps` how many steps `elapsed` is worth, given the carry `Preview` kept from the last update. It keeps the new carry, advances the run once for each step, then advances playback by `elapsed` over the frame count those steps left. The run goes first, so a frame added in this update is already on the timeline playback sees. For example, playing from the first of two frames, an update of one interval adds a third frame and moves playback to the second frame (index 1). With playback first, the same update would reach the last of two frames and follow the latest instead. A completed or stopped run doesn't change, so only playback moves. `Preview` only keeps the carry and counts the calls: how many steps there are is Core's decision.
- **`Apply(command)`** gives the playback that `PlaybackRules.Apply` gives for the current frame count.
- **`Shown`** is `PlaybackRules.Shown` for the current playback and frame count, computed each time it is read.
- **`Restart()`** stops the run and starts a new one with the same seed. **`NextSeed()`** does the same with `RunSeed.Next()`, which wraps from `int.MaxValue` to `int.MinValue`. Either way playback returns to `FollowingLatest`, because the new timeline is empty and any held index would be off it, and the pacing carry returns to zero, so time left over from the old run doesn't count toward the new run's first step.

#### Pacing live generation

`Pacing.Steps(carry, elapsed)` gives a `Paced(Steps, Carry)`: ⌊(carry + elapsed) / interval⌋ steps, and the remainder as the new carry. The interval is the same `Playback.FrameInterval` playback uses, so generating and replaying move at the same rate. It is a Core function because how many steps a time is worth is a decision, and it works in whole ticks like `Advance`, so the same total elapsed time gives the same total steps and the same final carry however it is split into updates. A long update, such as after the window was stalled, takes every step it is owed at once.

- **The first frame of a new run arrives one interval after it starts,** and the run reports completed one interval after its last frame, on the advance that finds no frame left. One rule covers both, so neither is a special case. The built-in run of 7 frames therefore takes about 1.75 seconds to generate, and 2.0 to complete.
- **It throws** on a carry below zero or of a whole interval or more, and on a negative elapsed time, like `Advance`. It also throws on an elapsed time worth more steps than an `int` holds, about 17 years, which can only be a bug.

## Inputs

A run has no parameters of its own. It takes:

- **A validated pipeline.** How many frames each stage yields is its technique's business, but every stage yields at least one.
- **A run seed:** any 32-bit signed whole number.

`Preview` takes the same two values when it is constructed. After that, its caller passes the elapsed time to each `Update` and a command to each `Apply`.

Playback has no parameters. Its inputs, all passed in by the caller, are:

- **The frame count:** how many frames the timeline holds, 0 or more. A negative count throws.
- **The command,** for `Apply`.
- **The elapsed time,** for `Advance`, 0 or more. A negative time throws. It is a `TimeSpan` argument: Core reads no clock.

The frame interval is a Core constant, not a parameter (see Decisions and limits).

## Sources

There is no published algorithm. The run, the timeline, the preview, the playback modes and the transition table were designed for tilegen in the `engine-preview` change (design D2 to D5). No code was copied.

## Decisions and limits

- **Playback is a Core value, not state in Desktop.** Which frame is drawn is a decision, so it belongs in Core. Kept in Desktop, it would also be state the window holds.
- **The interval is fixed at 250 ms,** because how fast replay goes changes what is drawn when. Making it configurable is out of scope.
- **A bad index throws instead of being clamped.** Within a run the timeline only grows, and a new run resets playback, so an index past the end means the caller passed the wrong count or a stale playback. Clamping would hide that.
- **A negative frame count or elapsed time also throws.** Design D4 doesn't mention either. Both can only come from a bug, and splitting elapsed time only gives the same result when no part is negative.
- **A `Playing` at the last frame is not checked for.** No transition produces one, so it isn't rejected on input either. Passed in directly, it behaves as the table and `Advance` say: `Advance` turns it into `FollowingLatest`, even with zero elapsed time.
- **The run advances on its caller's thread, one step per call.** In the preview that is the UI thread, so a slow step blocks the window for as long as it takes. Value-noise steps at small sizes take milliseconds. There is no worker thread, `Task` or cancellation token. Stopping is just no longer asking for frames. The caller pulls each step, so a later change could move stepping onto a worker without changing Core.
- **Generation is paced at the frame interval.** The run takes one step per 250 ms, 4 steps a second, whatever the update rate. At one step per update, at 60 updates a second, the built-in 7-frame run would finish in about an eighth of a second, so Restart would look like a flash. The cost is that a run with hundreds of steps takes minutes to watch. A speed control or a step budget comes with the first technique that needs one. The pacing reads no clock: the caller passes the elapsed time in.
- **The timeline is unbounded.** Every frame is kept, with no cap, no eviction and no down-sampling, so a large stage with many steps can use a lot of memory (see The timeline). Which frames to keep would be a Core decision, returned as data.
- **`RunState` is a closed record hierarchy, not an enum.** Its cases carry no data, so `illegal-states R4` alone would allow an enum. But `railway R2` requires an outcome set to be a closed hierarchy or a union, and the more specific rule wins. `Running` is a case too, so the run's whole state is one type.
- **Disposing an enumerator is hygiene.** A technique holds nothing that needs releasing, so it can be abandoned after any frame. The run still disposes the enumerator on stopping and on completing, so the iterator ends as soon as the run is done with it. No test observes the disposal: the stage set is closed, so there is no stand-in technique that could record it.

## Where it lives

- `src/Tilegen.Core/Replay/Playback.cs`: `Playback`, `FrameInterval`, `FollowingLatest`, `Paused` and `Playing`.
- `src/Tilegen.Core/Replay/PlaybackCommand.cs`: the commands.
- `src/Tilegen.Core/Replay/ShownFrame.cs`: `ShownFrame`, `NoFrame` and `FrameAt`.
- `src/Tilegen.Core/Replay/PlaybackRules.cs`: `Shown`, `Apply` and `Advance`.
- `src/Tilegen.Core/Replay/Pacing.cs`: `Pacing.Steps` and `Paced`.
- The namespace is `Tilegen.Core.Replay`, not `Playback`, because a type named after its own namespace fails to bind (CS0118).
- `tests/Tilegen.Core.Tests/Replay/PlaybackScenarioTests.cs`: one test per scenario in the frame-playback spec.
- `tests/Tilegen.Core.Tests/Replay/PlaybackRulesTests.cs`: every cell of the transition table (both sides of each condition and clamp), the empty-timeline row, the `Advance` rules, and the bad-index, negative-count and negative-time throws.
- `tests/Tilegen.Core.Tests/Replay/PlaybackTypeTests.cs`: switches over `Playback`, `ShownFrame` and `PlaybackCommand` with no discard arm; the constructor checks; the interval.
- `tests/Tilegen.Core.Tests/Replay/PlaybackProperties.cs`: CsCheck properties over playback reached by generated runs of added frames, commands and elapsed times from a new run (0 to 50 frames). The shown frame is `NoFrame` exactly when the timeline is empty, and is otherwise on it. Splitting elapsed time over updates gives the same playback. Any command on an empty timeline gives `FollowingLatest`. Zero elapsed time changes nothing. A reached `Playing` is always before the last frame.
- `tests/Tilegen.Core.Tests/Replay/PacingTests.cs`: zero, partial and whole intervals, two and a half intervals, an existing carry, each argument check, and a property that splitting elapsed time over updates gives the same total steps and final carry.
- `src/Tilegen.Engine/GenerationRun.cs`: `GenerationRun`, with `Start`, `Advance` and `Stop`, and the `StageOutputs` it keeps and passes to each stage (`StageOutputs` itself is in Core; see `stage-inputs.md`).
- `src/Tilegen.Engine/RunState.cs`: `RunState`, `Running`, `Completed` and `Stopped`.
- `src/Tilegen.Engine/Timeline.cs` and `src/Tilegen.Engine/TimelineEntry.cs`: the timeline and its entries.
- `src/Tilegen.Engine/Preview.cs`: `Preview`, the current run and its playback.
- The Engine types are in the `Tilegen.Engine` namespace.
- `tests/Tilegen.Engine.Tests/GenerationRunScenarioTests.cs`: one test per scenario in the generation-run spec. Each stage's kept frames are compared, pixel for pixel, with its technique run alone with the derived stage seed: through `Generation.Run` for a stage with no input, and through `TerrainThreshold.Run` on the source's kept image for a threshold. A threshold is shown to read its source's last entry, not an earlier one, and the source's entries are shown unchanged after the threshold runs. Between them the tests produce every `RunState` case. No test produces the throw for a stage with no frame: the stage set is closed, so there is no stand-in technique that yields nothing.
- `tests/Tilegen.Engine.Tests/RunTypeTests.cs`: a switch over `RunState` with no discard arm; the timeline's public surface, which has no way to add, remove or replace an entry; the step counting from 1.
- `tests/Tilegen.Engine.Tests/PreviewTests.cs`: a new preview; `Update` stepping the run at the frame interval's pace before playback, the carry resetting on `Restart` and `NextSeed`, and only playback once the run has completed; `Restart` keeping the seed and `NextSeed` moving it, each stopping the old run and returning to `FollowingLatest` on an empty timeline; `Apply` matching `PlaybackRules.Apply` for every command.
- `tests/Tilegen.Engine.Tests/TestPipelines.cs`: builds the tests' pipelines with a `PipelineBuilder` and `Pipeline.Validate`, and runs a stage's technique alone for comparison.
- The Engine tests need no fakes: nothing here is a port.
