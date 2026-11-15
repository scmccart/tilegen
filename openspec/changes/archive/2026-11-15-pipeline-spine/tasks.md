# Tasks

Every task's verification includes `dotnet build` with 0 warnings and `dotnet test -- --minimum-expected-tests 1` passing, unless the task says otherwise.

## 1. Shared contracts (Core)

- [x] 1.1 Add `Rgba32`, `PixelBuffer` and `Frame` to Core (design D6). Width and height must be at least 1. Pixels are stored row-major. `Frame` is a read-only view with `ToBuffer()`. Verify with value tests in `Tilegen.Core.Tests`: construction limits, get and set, a copy independent of its source, and a `ToBuffer()` copy unaffected by later writes to the buffer.
- [x] 1.2 Add the typed wrappers `StageId`, `PixelSize`, `Octaves`, `CellSize`, `Persistence` and `NoiseParameters` (design D2). Each has an internal constructor, a public throwing factory, and its range as constants. `NoiseParameters` enforces cell size ≥ 2^(octaves − 1). Verify with tests at each edge of each range, through the throwing factories.
- [x] 1.3 Add the description and validated shapes (design D1, D3):
  - the closed `StageDescription` with `NoiseDescription`, and `PipelineDescription`;
  - the closed `Stage` with `Noise`, and `ValidatedPipeline` with no public constructor;
  - the closed `ValidationOutcome` (`Valid`, and `Invalid`, which refuses an empty list);
  - the closed `Finding` cases, `StageRef`, `Parameter` and `ParameterRange`.

  Verify that a switch expression over each closed type compiles with no discard arm, and that a test shows `Invalid` refuses an empty list.
- [x] 1.4 Delete `CoreMarker`:
  - point `tests/Tilegen.ArchitectureTests/Layers.cs` and `EngineMarker` at `PixelBuffer`;
  - replace Core.Tests' placeholder test with the tests above.

  Verify that the architecture tests pass, anti-vacuity checks included, and that `grep -rn CoreMarker src tests` is empty.

## 2. Pipeline validation (Core)

- [x] 2.1 Add CsCheck 5.0.1 to `Directory.Packages.props` and to `Tilegen.Core.Tests`. Confirm its licence and record it in design D8's table if it differs. Verify that the package restores and a trivial CsCheck property runs.
- [x] 2.2 Implement `Pipeline.Validate` (design D3): description order, the id before the parameters, the cross-field rule only when both of its values are in range, and exactly one finding for an empty pipeline. Verify with tests for every scenario in `specs/pipeline-validation/spec.md` and the range scenarios in `specs/value-noise/spec.md`. Each `Finding` case and each `ValidationOutcome` case must be produced by at least one test (`railway` Enforcement).
- [x] 2.3 Add validation property tests with CsCheck:
  - a generated in-range description always validates, keeping its ids and values;
  - one parameter pushed out of range gives exactly that `OutOfRange` finding;
  - validating twice gives equal results, and the description is unchanged.

  Verify that the properties pass.
- [x] 2.4 Update `docs/architecture/railway.md` Enforcement: there is now an outcome type (`ValidationOutcome`), and each of its cases and each `Finding` is produced by a test. Verify by reading it against 2.2's tests.

## 3. Stage seeds (Core)

- [x] 3.1 Confirm on .NET 11 whether `new Random(s)` and `new Random(-s)` give the same sequence, with a scratch check that isn't committed. Report the result. If they don't, drop the mask from design D4 and the topic doc, and say so.
- [x] 3.2 Implement `RunSeed`, `StageSeed` and `StageSeed.Derive` as FNV-1a 64 over the little-endian run seed plus the UTF-8 id, folded and masked (design D4). Verify with:
  - the hash core against FNV's published 64-bit FNV-1a test vectors (at least `""` and `"a"`);
  - a pinned table of at least five (run seed, id) → seed rows, including a negative run seed and a non-ASCII id;
  - each scenario in `specs/stage-seeding/spec.md`, including the culture scenario, restoring `CultureInfo.CurrentCulture` in a `finally`.
- [x] 3.3 Write `docs/design/stage-seeds.md` in the format of `docs/design/README.md` (design "Topic docs"). Its sources are the FNV specification and the test vectors. Verify that each claim in it matches 3.2's code and tests.

## 4. Golden comparer (test kit)

- [x] 4.1 Add StbImageSharp 2.30.16 and StbImageWriteSharp 1.16.7 to `Directory.Packages.props` and `Tilegen.TestKit`. Add both to TestKit's row in `DeclaredReferenceTests`, and update the row's comment. Verify that the architecture tests pass. Then remove one package from the allowlist in a scratch edit, show `Project_declares_only_allowed_packages(Tilegen.TestKit)` failing, and revert.
- [x] 4.2 Implement the golden comparer and provenance reader (design D7):
  - `GoldenComparison` with `Matches`, `Missing`, `SizeDiffers` and `PixelsDiffer`;
  - `Golden.Compare` and `Golden.AssertMatches`;
  - actual and expected images written under `golden-results/` on anything but a match;
  - no record mode.

  Verify with tests in `Tilegen.Core.Tests` for each `GoldenComparison` case and a PNG encode-then-decode round trip. Each test uses its own temporary directory and removes it.
- [x] 4.3 Update the docs that describe the comparer and the test kit:
  - `docs/architecture/testing.md`: How it applies (the test kit holds the comparer; CsCheck is pinned) and Enforcement (the comparer is built; it covers R4 and supports R6);
  - `docs/architecture/ports-and-adapters.md`, the note under the table: the test kit is also allowed the two Stb packages;
  - `docs/architecture/README.md`, Enforcement status;
  - `.claude/rules/tests.md`, Enforced by.

  Verify with `grep -rn "not built yet\|deferred to the first technique\|arrives with the first technique" CLAUDE.md docs .claude`, reporting each remaining hit and whether it is still true.

## 5. Value noise (Core)

- [x] 5.1 Implement `ValueNoise.Run` (design D5) and `Generation.Run`, a switch expression from `Stage` to its technique with no discard arm. Verify with tests for each scenario in `specs/value-noise/spec.md` except the golden: frame count and size, the first frame equal to a 1-octave run, opaque grey, same seed same pixels, different seeds differ, and a kept copy unaffected by later steps.
- [x] 5.2 Add CsCheck properties over generated valid noise parameters, kept small (sides up to 64): determinism, frame count equal to octaves, and every pixel opaque grey. Value noise has no input, so the inputs-unchanged property has nothing to check. Say so in the test file, and leave it for the first technique with an input. Verify that the properties pass.
- [x] 5.3 Add the golden test, reading `tests/Tilegen.Core.Tests/Goldens/value-noise/basic.golden.json` with `approvedIn: "pipeline-spine"`. The suggested stage is 64 × 64, 4 octaves, cell size 16, persistence 0.5, with a fixed stage seed. Run it while no PNG exists, so it fails as `Missing` and writes `basic.actual.png`. **Stop here.** The main session shows the image to the owner. Only after the owner approves it by eye, copy it to `Goldens/value-noise/basic.png`. Verify that the golden test then passes and the provenance file names the .NET version.
- [x] 5.4 Write `docs/design/value-noise.md` in the format of `docs/design/README.md`, with the sources of design D5 checked against their texts. Update `docs/architecture/functional-core.md` Enforcement: the determinism property now exists for value noise, and inputs-unchanged waits for the first technique with an input. Verify that each claim in the topic doc matches 5.1 to 5.3.

## 6. Integration

- [x] 6.1 Run `dotnet build` (0 warnings) and `dotnet test -- --minimum-expected-tests 1`. Report the counts per project, and confirm that CI is green on the pushed branch.
- [x] 6.2 Check the PR against the checklists in `functional-core.md`, `illegal-states.md`, `railway.md` and `testing.md`, and record each item's result in the PR description.

## Workflow follow-up

- Run `openspec validate pipeline-spine --strict` before opening the PR.
- Archive the change inside its PR before it merges, checking `docs/design/value-noise.md` and `docs/design/stage-seeds.md` against the code as built.
- The PR names the golden added, its approval, and the manual check of looking at it.
