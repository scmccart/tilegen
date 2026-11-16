# Tasks

Unless a task says otherwise, every task is verified by:

- `dotnet build`, with 0 warnings;
- `dotnet test -- --minimum-expected-tests 1`, passing.

Some tasks in groups 1 and 3 touch Engine or Desktop. Those edits are the mechanical ones needed to keep the build green after a Core contract changes; the work itself is in Core.

## 1. Shared contracts

- [x] 1.1 Move the id to the base `StageDescription` (design D2): `closed record class StageDescription(string Id)`, with `NoiseDescription(string Id, …) : StageDescription(Id)`. Update `Pipeline.Validate` and the tests that build descriptions. Verify that the validation tests pass unchanged in what they assert, and that a test reads `Id` through a `StageDescription`-typed reference.
- [x] 1.2 Add `Image` under `Tilegen.Core.Imaging` (design D4), with `Frame.ToImage()` and `PixelBuffer.ToImage()`.
  - `Image` has `Width`, `Height`, a read-only indexer, `CopyTo(Span<Rgba32>)` and `ToBuffer()`.
  - It is backed by an `ImmutableArray<Rgba32>` and has no member that exposes it writably.

  Verify with tests in `tests/Tilegen.Core.Tests/Imaging/`:
  - a write to the source buffer after `ToImage()` doesn't change the image;
  - `CopyTo` is row-major and throws on a short span;
  - `ToBuffer` gives an independent copy;
  - an out-of-range index throws.
- [x] 1.3 Ban the escape hatches (design D4).
  - First confirm that `grep -rn "MemoryMarshal\|ImmutableCollectionsMarshal\|Unsafe\." src/Tilegen.Core` finds nothing.
  - Then add `T:System.Runtime.InteropServices.ImmutableCollectionsMarshal`, `T:System.Runtime.InteropServices.MemoryMarshal` and `T:System.Runtime.CompilerServices.Unsafe` to `src/Tilegen.Core/BannedSymbols.txt`, with messages citing `functional-core R4`.
  - Update the banned-list paragraph in the Enforcement section of `docs/architecture/functional-core.md`.

  Verify that each entry gives RS0030 by adding a use of each to a Core file temporarily, building, and reverting.
- [x] 1.4 Add `StageOutputs` (`Empty`, `With`, and an indexer that throws on a missing id) and change `Generation.Run` to `Run(Stage, StageOutputs, Random)` (design D5). Keep `GenerationRun` and its tests compiling:
  - `TimelineEntry` holds `Image Image` in place of `Frame Frame`, and the run keeps `frame.ToImage()`;
  - the run adds each finished stage's last entry's image to its outputs, and passes the outputs when the next stage starts. A stage that ends with no frame throws `InvalidOperationException`;
  - `FrameTexture` uploads with `Image.CopyTo`;
  - `tests/Tilegen.Engine.Tests` and `GenerationTests` move from `.Frame` to `.Image`. The TestKit's `Pixels` helpers gain `Image` overloads if they need them.

  Verify that every existing Engine scenario passes unchanged in what it asserts, and that a Core test shows `StageOutputs.With` leaves the original unchanged and the indexer returns what was added.
- [x] 1.5 Add `StageHandle` (internal constructor; `Index`, `Id`) and `PipelineBuilder` (`Add`, `Build`), with the comment `functional-core R11` asks for (design D1). Verify with Core tests:
  - `Add` returns handles at positions 0, 1 and 2, holding the ids given, with `""` for a null id;
  - `Build` returns the stages in order, and a later `Add` doesn't change a description already built.

  Task 3.3 covers the scenario in which a threshold reads a stage through its handle.

## 2. The threshold technique (Core)

- [x] 2.1 Add `ThresholdLevel` (0 to 255, with `Min`, `Max`, `IsInRange`, an internal constructor and `Create`) and `ThresholdParameters(StageId Source, ThresholdLevel Level)` (internal constructor, plus `Create`) under `Tilegen.Core.Techniques` (design D3). Verify with wrapper tests like `NoiseWrapperTests`: the bounds and `Create` throwing out of range.
- [x] 2.2 Implement `TerrainThreshold.Run(Image source, ThresholdParameters parameters, Random random)` (design D6). Arguments are checked at the call, and the rest runs in the first step. Its one step allocates a working buffer of the source's size, writes opaque white where ⌊(R + G + B) / 3⌋ ≥ level and opaque black elsewhere, and yields. Expose the two colours as named constants. Verify with `tests/Tilegen.Core.Tests/Techniques/ThresholdTests.cs`: one test per scenario in `specs/threshold/spec.md`, apart from the validation and golden scenarios, which are in 3.3 and 2.4. That includes no draws from the random: the random's next value equals a fresh same-seed random's first.
- [x] 2.3 Add `ThresholdProperties.cs`: CsCheck properties over generated images up to 32 × 32 with random pixels, and levels from 0 to 255 (design D9):
  - determinism;
  - every pixel is opaque black or white, and white exactly where the brightness is at or above the level;
  - **inputs unchanged:** the input equals its pre-run `ToBuffer()` snapshot after every yielded frame.

  Update the comment in `ValueNoiseProperties.cs` that says no technique has an input yet. Verify that the properties pass.
- [x] 2.4 Add the threshold golden test, `ThresholdGoldenTests.cs`.
  - It decodes `Goldens/value-noise/basic.png` into an `Image`, reads its level from `Goldens/threshold/basic.golden.json`, runs the technique, and compares the frame with `Golden`.
  - Write the provenance described in design D9.
  - Run the test once. It reports `Missing` and writes `golden-results/threshold/basic.actual.png`.
  - **Stop and show the owner** that image beside its input. Copy it to `tests/Tilegen.Core.Tests/Goldens/threshold/basic.png` by hand only after the owner approves it (`testing R6`).

  Verify that the golden test passes after approval.
- [x] 2.5 Create `docs/design/threshold.md`, following `docs/design/README.md`: what it does, the rule, one frame, its parameters, sources (none; a plain binary threshold), decisions and limits, where it lives, and the golden and its provenance. Verify by reading it against 2.2 to 2.4 as built.

## 3. Threshold stages and input validation (Core)

- [x] 3.1 Add the `Threshold(StageId Id, ThresholdParameters Parameters)` stage and its arm in `Generation.Run`, which reads `outputs[parameters.Source]` (design D3, D5). Verify that a `GenerationTests` case, building the stage with `ThresholdParameters.Create`, gives the same frames through `Generation.Run` as through `TerrainThreshold.Run` on the same image.
- [x] 3.2 Add `ThresholdDescription(string Id, StageHandle Source, int Level)` together with its validation, because `Pipeline.Validate`'s switch has no discard arm and the build breaks until both exist. Add `Parameter.Level`, the `Input` enum and the `MissingInput`, `UnknownInput`, `SelfInput` and `LaterInput` findings. Implement `CheckInput` and `ValidateThreshold` (design D3):
  - checks in the order id, source, level;
  - a stage is built only when it added no finding and its source resolved.

  Add Desktop's `FindingText` arms, the `Level` parameter name and the `Input` names (design D8), so the exhaustive switches compile. Verify that the closed-type test in `ClosedTypeTests.cs` switches over every `Finding` case with no discard arm.
- [x] 3.3 Write the validation tests in `PipelineValidationTests.cs`: one test per scenario in `specs/pipeline-validation/spec.md` and the two validation scenarios in `specs/threshold/spec.md`. Each new finding is produced and asserted as its case (`testing R15`). Later and self references use handles from a second builder (design D9). Extend `PipelineValidationProperties.cs` so that its generated descriptions include threshold stages whose sources are earlier stages, and check that its determinism property still holds. Verify that the tests pass.
- [x] 3.4 Create `docs/design/stage-inputs.md`. It covers the builder and handles, how a handle is resolved and in what order, why a source's findings aren't repeated, `Image` and the banned escape hatches, the inputs-unchanged checks, the limits (no kinds, no size checks, no cycles possible), and where it lives. Leave a headed placeholder for the engine half, which 4.2 fills. Verify by reading it against 1.2 to 3.3 as built.

## 4. Passing inputs in a run (Engine)

- [x] 4.1 Add one Engine test per scenario in `specs/generation-run/spec.md` to `GenerationRunScenarioTests.cs` (design D9). `TestPipelines` builds them through `PipelineBuilder` and `Pipeline.Validate`. The source's entries are snapshotted between advances, before the threshold's first step. What a stage was passed is observed through the threshold's frame: the stage set is closed, so no stand-in technique can record its inputs. The no-frame throw has no test, for the same reason (design D5). Verify that the tests pass.
- [x] 4.2 Fill in the engine half of `docs/design/stage-inputs.md`, and update `docs/design/timeline-and-playback.md` (entries hold an `Image`, the run keeps outputs, "run alone" now includes inputs, a stage with no frame throws). Recompute the figures that describe the built-in run for its 6 frames: "10 frames", "640 KiB", "2.5 seconds" and "2.75". Verify by reading each against 1.4 and 4.1 as built.
- [x] 4.3 Update the Enforcement sections of `docs/architecture/functional-core.md` and `testing.md`, and the README's "Enforcement status". The inputs-unchanged property is in place in `ThresholdProperties.cs`, and the Engine scenario covers the timeline. Fix any text this change makes stale in `illegal-states.md`'s Enforcement section and in `.claude/rules/*.md`. Verify by reading each against the code.

## 5. The built-in pipeline (Desktop)

- [x] 5.1 Change `BuiltInPipeline` to use `PipelineBuilder` with the noise and threshold stages from design D7, and update its comment. Verify:
  - launch with `TILEGEN_SCREENSHOT` set and `TILEGEN_SCREENSHOT_AT` past the run's end (about 2 s for 6 frames at 250 ms), and check that the screenshot shows a black-and-white map;
  - take a second screenshot mid-run, about 1 s in, showing noise;
  - send both PNGs to the owner.
- [x] 5.2 Check the invalid-description path by hand. Temporarily point the threshold's source at a handle from a second builder, and check by screenshot that the window lists an unknown-input finding as text. Revert. Verify that `git diff src/Tilegen.Desktop/BuiltInPipeline.cs` shows only 5.1's change.
- [x] 5.3 Update `docs/design/value-noise.md`: threshold now reads its output, and its "Where it lives" note points at threshold's inputs-unchanged property. Verify by reading it.

## 6. Integration check

- [x] 6.1 Run `dotnet build`, `dotnet test -- --minimum-expected-tests 1` and `openspec validate stage-inputs --strict`. Check that each topic doc named in design.md's "Topic docs" section matches the code, and that `docs/roadmap.md` row 1 reads "in progress (openspec: stage-inputs)". Verify that all three commands pass.

## Workflow follow-up

- Open the PR, listing the manual checks from 5.1 and 5.2 (`testing R2`) and the golden approval from 2.4.
- Archive the change inside the PR before it merges. Set roadmap row 1 to "done" and add it to the Done table with its archive folder.
