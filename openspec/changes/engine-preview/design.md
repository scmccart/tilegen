# Design

## Context

- **Starting point.** The pipeline spine (archived as `2026-11-15-pipeline-spine`) left:
  - `Pipeline.Validate`, which turns a description into a `ValidatedPipeline` of closed `Stage` cases;
  - `StageSeed.Derive`, which gives a stage's seed from the run seed and the stage id;
  - `Generation.Run(stage, random)`, which dispatches a stage to its technique's iterator;
  - `Frame`, a view valid until the next step, and `PixelBuffer` with a bounds-checked indexer.

  Engine has only `EngineMarker`. Desktop is `SmokeGame`, a window showing one label. See proposal.md for why this change comes next and what is out of scope.
- **Rules that shape this change:**
  - `functional-core R2`, `R3`, `R4`, `R9`, `R10`: decisions in Core, time as an argument, frames copied before they are kept, one `Random` per stage built by the engine, and stopping by no longer enumerating;
  - `illegal-states R3`, `R4`, `R10`: the engine runs only a validated pipeline, mutually exclusive states are one closed type, and absence is a case;
  - `railway R2`, `R5`, `R8`, `R11`: exhaustive switches, text only in Desktop, stopping is not an error, and no catch-all cases;
  - `ports-and-adapters R3`, `R5`, `R11`, `R13`: no port without a platform, plain `new` composition, Desktop only presents, and no manager layers;
  - `testing R1`, `R2`, `R9`, `R12`: tests sit in the layer that answers the question, there are manual UI checks, invariants are properties, and tests run in parallel.

## Goals / Non-Goals

**Goals:**

- An engine run that Desktop can drive one step per frame and stop at any time, keeping copies of every frame.
- Playback rules that are pure Core functions over values, tested without a window.
- A Desktop that only gathers input, calls Engine and Core, and draws what they hold.

**Non-Goals:**

- No general scheduler and no step budget measured by time. The preview advances the run one step per frame interval, the same interval playback uses (D10).
- No abstraction over "something that produces frames". There is one run type.
- No Desktop test project. The window is a manual check (`testing R2`).

## Layer mapping

| Piece | Layer | Notes |
| --- | --- | --- |
| `Stage.Id` on the base record | Core | Lets the engine name a stage without a switch of its own |
| `PixelBuffer.CopyTo`, `Frame.CopyTo` | Core | A bulk read, for upload. Nothing writes through it. |
| `RunSeed.Next` | Core | Pure: the next seed, wrapping |
| `Playback` (closed) and its `Apply`, `Advance` and `Shown`; `PlaybackCommand`; `ShownFrame` (closed), in `Tilegen.Core.Replay` | Core | Every playback decision (`functional-core R2`, `R3`) |
| `Pacing.Steps` and `Paced`, in `Tilegen.Core.Replay` | Core | How many run steps an elapsed time is worth (D10) |
| `GenerationRun`, `RunState`, `Timeline`, `TimelineEntry` | Engine | Gathers frames, calls Core, keeps copies |
| `Preview` | Engine | Holds the current run and its playback, and forwards to Core |
| The built-in description, the starting seed, finding text, texture upload, layout, controls | Desktop | Presentation and input only |
| Ports, adapters | None | Nothing here is a platform (`ports-and-adapters R3`) |

Desktop's only branches are switches over what Core and Engine return: the `ValidationOutcome`, the `ShownFrame`, the `Playback` case (to label the play button), the `RunState` (to label the run), and the `Finding` cases (to render text). Plus which button was pressed. The integer fit scale is the one computation Desktop does itself. It is presentation: it doesn't change generated pixels, what is exported, what is valid, or what is run or drawn. It is not a decision under `functional-core R2`.

## Decisions

### D1. The stage id moves to the base `Stage` record

`Stage` becomes `closed record class Stage(StageId Id)`, and `Noise(StageId Id, NoiseParameters Parameters) : Stage(Id)`. The engine labels timeline entries and derives seeds from `stage.Id`, with no switch of its own. Without this, every new technique would add an arm to an Engine switch that does nothing but read a field.

- **Alternative:** a Core function `Stages.IdOf(Stage)` with one arm per case. Rejected: it is the same switch, moved, and the id belongs to every stage by definition.

### D2. The run is an object the caller advances; its state is a closed type

```csharp
// Illustrative; names are not final.
public closed record class RunState;
public sealed record class Running : RunState;
public sealed record class Completed : RunState;
public sealed record class Stopped : RunState;

public sealed class GenerationRun
{
    public static GenerationRun Start(ValidatedPipeline pipeline, RunSeed seed);
    public RunSeed Seed { get; }
    public RunState State { get; }
    public Timeline Timeline { get; }
    public void Advance();   // while Running: one frame onto the timeline, or Completed when none remain
    public void Stop();      // Running → Stopped; otherwise no change
}
```

- **One step per `Advance`.** The run holds the index of the current stage and that stage's enumerator. Each advance calls `MoveNext` on the enumerator. When the current stage has no frames left, the advance moves to the next stage in the same call. If no stage has a frame left, the run becomes `Completed` and adds no frame. A stage yields at least one frame, but the loop doesn't rely on it.
- **One `Random` per stage, built when that stage starts:** `new Random(StageSeed.Derive(seed, stage.Id).Value)`, passed to `Generation.Run` (`functional-core R3`, `R9`). Creating it lazily, rather than all up front, changes nothing, because each stage's stream is independent.
- **Stopping is the engine no longer enumerating** (`functional-core R10`, `railway R8`). `Stop` disposes the enumerator, and so does completing. Techniques hold nothing that needs releasing, so disposing is hygiene, not a requirement.
- **`RunState` is a closed record hierarchy, not an enum.** `railway.md` lists a generation run's outcome as "completed or stopped", and `railway R2` requires an outcome set to be a closed hierarchy or a union. `illegal-states R4` would allow an enum for cases with no data, but the outcome rule is the more specific one. Running is included as the third case, so the run's whole state is one closed type. Switches over it have no discard arm. Each case is produced by an Engine test (`railway.md`, Enforcement).
- **Ruled out:** a stopped or completed run that still enumerates (`Advance` returns at once), a run of an unvalidated description (`Start` takes `ValidatedPipeline` only), and a shared `Random` across stages.
- **Alternatives:**
  - A worker thread that runs to completion while the UI polls. Rejected for now: determinism stays easy, Core needs no cancellation, and the noise steps take milliseconds. The pull shape lets a later change move `Advance` onto a worker without changing Core.
  - A Core iterator over the whole pipeline. Not possible: it would have to construct each stage's `Random`, which Core must not do (`functional-core R3`).

### D3. The timeline keeps read-only copies

```csharp
public sealed record class TimelineEntry(StageId Stage, int Step, Frame Frame);   // Step counts from 1 within its stage

public sealed class Timeline
{
    public int Count { get; }
    public TimelineEntry this[int index] { get; }
    // Appending is internal to the run.
}
```

- **Each yielded frame is copied with `Frame.ToBuffer()`,** and the copy is wrapped in a new `Frame`. Nothing else holds the copy, so the entry's frame is read-only and permanent. Handing out the mutable `PixelBuffer` was rejected: any reader could change the history.
- **Everything is kept for the length of the run.** A new run starts with a new timeline. The cost is width × height × 4 bytes per frame. The preview's pipeline (two 128 × 128 stages, 10 frames) costs 640 KiB.
- **The step is a plain `int`,** an ordinal within the stage rather than a quantity with a unit (`illegal-states R7`, `R12`). A wrapper would only rename it.

### D4. Playback is a closed Core value with pure transitions

```csharp
// Tilegen.Core.Replay. Illustrative; names are not final. The namespace isn't `Playback`, because a type named after its own namespace fails to bind (CS0118).
public closed record class Playback
{
    public static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(250);
}
public sealed record class FollowingLatest : Playback;
public sealed record class Paused(int Index) : Playback;                      // 0-based
public sealed record class Playing(int Index, TimeSpan Carry) : Playback;     // 0 ≤ Carry < FrameInterval

public enum PlaybackCommand { Play, Pause, StepBack, StepForward, First, Latest }

public closed record class ShownFrame;
public sealed record class NoFrame : ShownFrame;
public sealed record class FrameAt(int Index) : ShownFrame;

public static class PlaybackRules   // or members on Playback
{
    public static ShownFrame Shown(Playback playback, int frameCount);
    public static Playback Apply(Playback playback, PlaybackCommand command, int frameCount);
    public static Playback Advance(Playback playback, TimeSpan elapsed, int frameCount);
}
```

- **The transitions, with `last = frameCount − 1`:**

  | From | Play | Pause | StepBack | StepForward | First | Latest |
  | --- | --- | --- | --- | --- | --- | --- |
  | `FollowingLatest` | `Playing(0, 0)`, or unchanged when `last == 0` | `Paused(last)` | `Paused(max(last − 1, 0))` | `Paused(last)` | `Paused(0)` | unchanged |
  | `Paused(i)` | `Playing(i == last ? 0 : i, 0)`, or `FollowingLatest` when `last == 0` | unchanged | `Paused(max(i − 1, 0))` | `Paused(min(i + 1, last))` | `Paused(0)` | `FollowingLatest` |
  | `Playing(i, c)` | unchanged | `Paused(i)` | `Paused(max(i − 1, 0))` | `Paused(min(i + 1, last))` | `Paused(0)` | `FollowingLatest` |

  - **Precedence.**
    - A `Paused` or `Playing` whose index is at or past `frameCount` is a bug and throws. It is checked first, so with `frameCount == 0` only `FollowingLatest` is a valid input, and every command then leaves it unchanged.
    - No transition produces `Playing` at `last`: Play on a one-frame timeline gives `FollowingLatest`, and `Advance` turns reaching `last` into `FollowingLatest`. The timeline only grows within a run, so `Playing(i)` always has `i < last` from then on.
  - `Advance` moves `Playing(i, c)` forward ⌊(c + elapsed) / interval⌋ frames, and keeps the remainder as the new carry. When the result reaches `last`, playback becomes `FollowingLatest`. Every other case is returned unchanged.
  - Elapsed time comes in as a `TimeSpan` argument (`functional-core R3`). `TimeSpan` arithmetic is exact in ticks, so splitting the same total over many updates gives the same frame as passing it at once.
- **Invalid states ruled out:**
  - Playing with nothing to play, or playing at the newest frame: no transition produces either.
  - A carry of a whole interval or more: `Advance` always reduces it.
  - Flags such as `isPaused` and `isLive` that could disagree: there is one closed type.
  - An index of `frameCount` or more passed in is a bug, and throws. Within a run the timeline only grows, and a new run resets playback.
- **The interval is a Core constant,** because how fast replay goes changes what is drawn when. Making it configurable is out of scope.
- **Alternative:** playback state as fields in Desktop. Rejected: it is a decision about what is drawn (`functional-core R2`), and it would be state Desktop keeps (`ports-and-adapters R11`).

### D5. `Preview` holds the current run and its playback

```csharp
public sealed class Preview
{
    public Preview(ValidatedPipeline pipeline, RunSeed seed);
    public GenerationRun Run { get; }
    public Playback Playback { get; }
    public ShownFrame Shown { get; }                 // PlaybackRules.Shown(Playback, Run.Timeline.Count)
    public void Update(TimeSpan elapsed);            // Run.Advance() Pacing.Steps times, then PlaybackRules.Advance
    public void Apply(PlaybackCommand command);
    public void Restart();                           // stop, new run, same seed, FollowingLatest
    public void NextSeed();                          // stop, new run with RunSeed.Next, FollowingLatest
}
```

- **It owns the mutable engine state Desktop reads each frame,** so Desktop keeps none of its own (`ports-and-adapters R11`). Every method forwards to Core or to the run. It isn't a manager layer (`R13`), because without it Desktop would hold the playback value itself.
- **`Update` advances the run once for each step `Pacing.Steps` gives (D10),** keeping the pacing carry between updates, and then advances playback. `Restart` and `NextSeed` reset the carry to zero with the new run.

### D10. Live generation is paced at the frame interval

At one step per update, the built-in run of 10 frames finished in a sixth of a second, so live generation and Restart looked like a flash, while Play's replay was watchable. The preview now paces the run by the same `Playback.FrameInterval`, so generating and replaying move at the same rate.

```csharp
// Illustrative; names are not final.
public readonly record struct Paced(int Steps, TimeSpan Carry);   // 0 ≤ Carry < FrameInterval
public static class Pacing
{
    public static Paced Steps(TimeSpan carry, TimeSpan elapsed);   // floor((carry + elapsed) / interval), and the rest
}
```

- **It is a Core function,** because how many steps an elapsed time is worth is a decision (`functional-core R2`). `Preview` keeps the carry and enacts the count.
- **It throws on a carry outside [0, FrameInterval) or a negative elapsed time,** like `PlaybackRules.Advance`.
- **Leftover time carries over,** so the rate doesn't depend on the update rate. A long update, such as after the window stalls, takes every step it is owed at once.
- **The first frame of a new run arrives one interval after it starts,** and the run reports completed one interval after its last frame, on the advance that finds no frame left. Both follow from one uniform rule, so neither gets a special case.
- **Alternative:** a step budget measured in wall-clock time. Rejected: it reads a clock, and the frame interval already exists.
- **Cost:** at 4 steps a second, a run with hundreds of steps takes minutes to watch. A speed control or a step budget comes with the first technique that needs one.

### D6. Bulk pixel read and upload

- **Core:** `PixelBuffer.CopyTo(Span<Rgba32> destination)` copies the row-major pixels, and throws if `destination` is shorter than width × height. `Frame.CopyTo` forwards to it. Core exposes no span over its own array, so the buffer stays the only writer.
- **Desktop:**
  - It keeps one `Rgba32[]` and one `Color[]`, sized to the shown frame, and converts between them field by field. It doesn't rely on matching struct layouts.
  - It calls `Texture2D.SetData` only when the shown frame changes, keyed by the run and the timeline index.
  - The cached texture is not mirrored state (`ports-and-adapters R11`). It is a GPU copy of an immutable timeline frame, identified by which frame it is. Nothing reads it back, and a key change replaces it.
- **Drawing** uses `SpriteBatch` with `SamplerState.PointClamp`, for sharp pixels, and `BlendState.NonPremultiplied`, because `Rgba32` isn't premultiplied (MonoGame's default `AlphaBlend` assumes it is). The destination rectangle is the frame size times the largest whole scale of 1 or more that fits the area beside the controls, centred.

### D7. Desktop composition and the built-in description

- **`SmokeGame` becomes `TilegenGame`.** `LoadContent` composes with plain `new` (`ports-and-adapters R5`):
  - no adapters yet;
  - then engine state: validate `BuiltInPipeline.Description` with `Pipeline.Validate`, and keep the outcome;
  - then the loop wiring.
- **The outcome is switched each frame,** with no discard arm:
  - `Valid` builds or reads the `Preview`, created once from the pipeline and `BuiltInPipeline.StartSeed`;
  - `Invalid` lists the findings.
- **Findings become text in one Desktop function,** with one arm per `Finding` case (`railway R5`). The whole set is rendered now, rather than treating a bad built-in description as a bug. Definitions will need the same text, and the switch has to be complete either way.
- **The built-in description:**
  - stage `coarse`: 128 × 128, 4 octaves, cell size 32, persistence 0.5;
  - stage `fine`: 128 × 128, 6 octaves, cell size 32, persistence 0.6;
  - starting seed 1.

  Two stages make the timeline cross a stage boundary. It lives in Desktop as a constant and is deleted by the definitions change.
- **The UI:** Apos.Gui, rebuilt each frame from `Preview` (`ports-and-adapters R11`). A fixed-width column on the left holds:
  - the labels the spec lists;
  - the play/pause, step back, step forward, first, latest, restart and next-seed buttons.

  The frame is drawn in the rest of the window. The window allows resizing. Control names are confirmed against Apos.Gui 3.1 when the work is done (the package's XML docs list `MenuPanel`, `Dock`, `StackPanel` and others).
- **Adapter references.** `DependencyTests` requires Desktop to depend on both adapter assemblies, and neither has a real type yet. The composition root keeps a commented `typeof(ScriptingMarker)` and `typeof(ExportMarker)` reference until the definitions and export changes replace them. This is the same device `SmokeGame` used.

### D8. Next seed

`RunSeed.Next()` returns `new RunSeed(unchecked(Value + 1))`, so `int.MaxValue` wraps to `int.MinValue`. Every 32-bit value is a valid run seed (`stage-seeding`), so wrapping loses nothing.

### D9. Tests

- **Core (`Tilegen.Core.Tests`):**
  - playback: one test per scenario in `specs/frame-playback/spec.md`, and every cell of D4's table;
  - CsCheck properties:
    - the shown index is always in range;
    - splitting elapsed time over updates gives the same playback as one update;
    - commands on an empty timeline give `FollowingLatest`;
  - `CopyTo`: the same pixels in row-major order, and a short span throws;
  - `RunSeed.Next`, including the wrap;
  - `Pacing.Steps`: whole intervals and the carry, the argument checks, and a CsCheck property that splitting elapsed time over updates gives the same total steps and carry.
- **Engine (`Tilegen.Engine.Tests`):** one test per scenario in `specs/generation-run/spec.md`, and a few for `Preview`:
  - `Update` steps the run once per whole frame interval, carrying the rest, and then the playback;
  - `Restart` keeps the seed and empties the timeline;
  - `NextSeed` moves the seed;
  - `Apply` forwards to Core.

  They compare pixels with the test kit's `Pixels.Snapshots` and `Pixels.Difference`, and need no fakes, because there are no ports. The placeholder test is deleted.
- **No goldens.** Value noise is unchanged, and the engine's frames are checked against the technique run alone.
- **Desktop:** manual checks, one per scenario in `specs/preview-window/spec.md`, reported in the PR.

## Risks / Trade-offs

- **[A slow step blocks the window, since `Advance` runs on the UI thread]** → Noise steps at preview sizes take milliseconds. D2's pull shape lets a later change move stepping to a worker without changing Core or the run's API much.
- **[The timeline grows without bound: a 4096 × 4096, 12-octave stage keeps 768 MiB]** → The built-in pipeline is small, and the risk is listed. A keep policy (a cap, or every nth frame) is a Core decision for a later change, and the topic doc names it as a known limit.
- **[Pacing at the frame interval makes long runs slow to watch: 4 steps a second]** → It's fine for octaves. A speed control or a budget comes with the first technique that has many cheap steps (D10).
- **[Moving `Id` to the base `Stage` touches validation and its tests]** → It's a mechanical change in task group 1, covered by the existing validation tests.
- **[Apos.Gui layout and control names may differ from what's assumed]** → Tasks describe behaviour, and the implementer confirms the names against the package. The scaffold already uses `MenuPanel` and `Label`.
- **[The adapter `typeof` references are a scaffold device kept alive]** → Each is commented with the change that removes it.

## Topic docs

- **Creates `docs/design/timeline-and-playback.md`:**
  - what a run is, and how it steps: stage order, one `Random` per stage, one frame per advance, and completing and stopping;
  - what the timeline keeps (copies, everything, the memory cost), and why entries are read-only;
  - the playback modes, the transition table, the fixed interval and the carry, and following the latest frame;
  - the decisions and limits: the UI thread, the unbounded timeline, pacing at the frame interval;
  - where it lives, and the tests that prove it.
- No technique's topic doc changes.
- Architecture docs that mention `EngineMarker` or `SmokeGame`, and `.claude/rules/*.md`, are updated in tasks.md, if they mention either one.
