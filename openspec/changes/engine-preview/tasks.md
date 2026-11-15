# Tasks

Unless a task says otherwise, every task is verified by:

- `dotnet build`, with 0 warnings;
- `dotnet test -- --minimum-expected-tests 1`, passing.

## 1. Shared contracts (Core)

- [x] 1.1 Move the stage id to the base record (design D1).
  - Make it `closed record class Stage(StageId Id)`, with `Noise(StageId Id, NoiseParameters Parameters) : Stage(Id)`.
  - Update `Pipeline.Validate` and any test that builds a `Noise`.
  - Verify that the existing validation and generation tests pass unchanged in what they assert, and that a test reads `Id` through a `Stage`-typed reference.
- [x] 1.2 Add `PixelBuffer.CopyTo(Span<Rgba32>)` and `Frame.CopyTo(Span<Rgba32>)` (design D6). Both copy the pixels in row-major order and throw `ArgumentException` when the span is shorter than width × height. A longer span is allowed, and only its prefix is written. Verify with tests in `Tilegen.Core.Tests/Imaging/`:
  - the pixels arrive in row-major order;
  - a short span throws;
  - a frame's copy equals its `ToBuffer()`.
- [x] 1.3 Add `RunSeed.Next()`: `unchecked(Value + 1)`, wrapping (design D8). Verify with tests for an ordinary value, for `-1` to `0`, and for `int.MaxValue` to `int.MinValue`.

## 2. Frame playback (Core)

- [x] 2.1 Add the playback types under `Tilegen.Core.Replay` (design D4; tests go in `Tilegen.Core.Tests/Replay/`, not a `Playback` folder, so the type name doesn't clash with a namespace):
  - the closed `Playback` with `FollowingLatest`, `Paused(int Index)` and `Playing(int Index, TimeSpan Carry)`;
  - `Playback.FrameInterval` (250 ms);
  - the `PlaybackCommand` enum;
  - the closed `ShownFrame` with `NoFrame` and `FrameAt(int Index)`.

  `Paused` and `Playing` throw on a negative index. `Playing` throws on a carry outside [0, FrameInterval). Verify that a switch expression over each closed type compiles with no discard arm, and that the constructor checks throw.
- [x] 2.2 Implement `Shown`, `Apply` and `Advance` as pure static functions (design D4's table and rules). Throw when a passed-in index is at or past `frameCount`, checked before anything else. Verify with:
  - one test per scenario in `specs/frame-playback/spec.md`;
  - one test per cell of D4's transition table, including the empty-timeline row;
  - names by behaviour (`testing R18`).
- [x] 2.3 Add CsCheck properties. Each generated case reaches its playback through a sequence of commands and advances from `FollowingLatest`, over a frame count that only grows (0 to 50), with generated commands and elapsed times, because that is how playback is reached in a run (design D4, precedence). Check that:
  - the shown frame is `NoFrame` exactly when the count is 0, and is otherwise in range;
  - splitting an elapsed total into any number of updates gives the same playback as one update;
  - any command on an empty timeline gives `FollowingLatest`;
  - `Advance` with zero elapsed time changes nothing.

  Verify that the properties pass.
- [x] 2.4 Create `docs/design/timeline-and-playback.md`, following `docs/design/README.md`. Write its playback half: the modes, the transition table, the interval and the carry, following the latest frame, and where it lives and its tests. Leave headed placeholders for the run and the timeline, which group 3 fills in. Verify by reading it against 2.1 and 2.2 as built.

## 3. Generation runs and the timeline (Engine)

- [ ] 3.1 Add the closed `RunState` (`Running`, `Completed`, `Stopped`; design D2), `TimelineEntry(StageId Stage, int Step, Frame Frame)` and `Timeline`. `Timeline` is read-only to callers, with an internal append (design D3). Verify that the types compile, and that `Timeline`'s public surface has no way to add, remove or replace an entry.
- [ ] 3.2 Implement `GenerationRun.Start`, `Advance` and `Stop` (design D2):
  - one `Random` per stage, built from `StageSeed.Derive(seed, stage.Id)` when the stage starts;
  - one frame per advance, copied with `ToBuffer()` and wrapped in a new `Frame`;
  - completing on the advance that finds no frame left;
  - disposing the enumerator on stop and on completion.

  Verify with one Engine test per scenario in `specs/generation-run/spec.md`, comparing pixels with `Pixels.Snapshots` and `Pixels.Difference` against `Generation.Run` called directly. Tests build pipelines through `Pipeline.Validate`, because `ValidatedPipeline`'s constructor is internal. Don't widen `InternalsVisibleTo`. Each `RunState` case is produced by at least one test.
- [ ] 3.3 Delete `EngineMarker`:
  - point `tests/Tilegen.ArchitectureTests/Layers.cs`, `ScriptingMarker.Inner`, `ExportMarker.Inner` and `SmokeGame`'s label at `GenerationRun` (`SmokeGame` survives until 5.1);
  - delete `tests/Tilegen.Engine.Tests/PlaceholderTests.cs`.

  Verify that the architecture tests pass, anti-vacuity checks included, and that `grep -rn EngineMarker src tests` is empty.
- [ ] 3.4 Fill in the run and timeline sections of `docs/design/timeline-and-playback.md`:
  - stage order, per-stage randomness, one frame per advance, and completing and stopping;
  - what is kept and its memory cost, and read-only entries;
  - the limits: the UI thread, an unbounded timeline, one step per update.

  Verify by reading it against 3.2 as built.
- [ ] 3.5 Update the Enforcement section of `docs/architecture/railway.md`: `RunState` is the second outcome type, and each of its cases is produced by an Engine test. Check the Enforcement sections of `testing.md` and `illegal-states.md`, and the README's "Enforcement status", for text this change makes stale, and fix it. Verify by reading each against the code.

## 4. The preview state (Engine)

- [ ] 4.1 Implement `Preview` (design D5): `Run`, `Playback`, `Shown`, `Update(TimeSpan)`, `Apply(PlaybackCommand)`, `Restart()` and `NextSeed()`. Verify with Engine tests:
  - `Update` advances the run one step and then playback;
  - `Update` on a completed run only advances playback;
  - `Restart` keeps the seed, gives an empty timeline, and returns to `FollowingLatest`;
  - `NextSeed` uses `RunSeed.Next`;
  - `Apply` gives the same playback as `Apply` in Core for the same count.
- [ ] 4.2 Add `Preview` to the "where it lives" section of `docs/design/timeline-and-playback.md`. Verify by reading it.

## 5. The preview window (Desktop)

- [ ] 5.1 Replace `SmokeGame` with `TilegenGame`, and update `Program` (design D7).
  - Keep the HiDef profile and the font setup.
  - Allow resizing.
  - Add `BuiltInPipeline` with the description and starting seed from D7.
  - Validate the description in `LoadContent`, and keep the outcome.
  - Keep a commented `typeof(ScriptingMarker)` and `typeof(ExportMarker)` reference, naming the changes that remove each.

  Verify that the app launches with `dotnet run --project src/Tilegen.Desktop`, and that the architecture tests still pass, including Desktop's required dependencies on both adapters.
- [ ] 5.2 Add the finding text: one Desktop function with one arm per `Finding` case and no discard arm (`railway R5`). Each line names the stage (index and given id) and what is wrong, with the range or the smallest valid cell size where the finding carries one. Show the lines in place of the frame when the outcome is `Invalid`. Verify by hand: temporarily give the built-in description an empty id and a width of −1, launch, see both lines, then revert the edit.
- [ ] 5.3 Draw the shown frame (design D6):
  - upload with `CopyTo` into cached `Rgba32[]` and `Color[]` arrays, only when the (run, index) key changes;
  - draw with `SamplerState.PointClamp` and `BlendState.NonPremultiplied`;
  - scale by the largest whole factor of 1 or more that fits, centred in the area beside the controls.

  Switch over `ShownFrame` with no discard arm. Verify by hand that the frames are sharp and refit in whole-number steps on resize.
- [ ] 5.4 Add the control column with Apos.Gui, rebuilt each frame from `Preview` (design D7). Confirm the component names against Apos.Gui 3.1 before writing.
  - Labels: stage and step, timeline position, seed, run state, playback mode.
  - Buttons: play or pause (labelled from the `Playback` case), step back, step forward, first, latest, restart and next seed.

  Call `Preview.Update(gameTime.ElapsedGameTime)` once per `Update`. Verify by hand against each scenario in `specs/preview-window/spec.md`, and record the checks for the PR (`testing R2`).

## 6. Integration

- [ ] 6.1 Run `grep -rn "EngineMarker\|SmokeGame" src tests docs .claude CLAUDE.md` and bring each remaining hit up to date. Verify that the grep is empty apart from the archived OpenSpec changes.
- [ ] 6.2 Run `openspec validate engine-preview --strict`, the full build and `dotnet test -- --minimum-expected-tests 1`. Verify that all three pass.

## Workflow follow-up

- Run `openspec validate engine-preview --strict` before opening the PR.
- The PR lists the manual checks from 5.2 to 5.4, and says that no golden was added or changed.
- Archive inside the PR before it merges, and check `docs/design/timeline-and-playback.md` against the code as built.
