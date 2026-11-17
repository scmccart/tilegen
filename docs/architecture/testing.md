# Testing architecture

How tilegen's tests are organised so they prove behaviour, stay fast and parallel, and fail for real reasons only. It governs everything under `tests/`. Code review enforces the **Rules** section; reviewers cite rules by number (`testing R6`).

## Why

A procedural technique has no reference to compare against: "correct" means "the owner looked at it and accepted it". That makes goldens the main proof, and a golden is worth exactly as much as the approval behind it. An agent that re-records one to turn a test green has deleted the proof, not passed it. The rest of this doc keeps the cheap layers cheap, so that the manual checks, which are the only ones for the UI, stay few.

## The idea

Each kind of test answers one question and has its own place.

| Layer | Answers | Where |
| --- | --- | --- |
| Value tests on Core | Does this function compute the right thing? | `tests/Tilegen.Core.Tests`, no fakes |
| Goldens | Does this technique still produce the approved image? | Core tests, data beside them |
| Property tests | Does an invariant hold for all inputs? | Core tests |
| Engine tests | Does the engine step, record and reload correctly? | `tests/Tilegen.Engine.Tests`, with fake ports once ports exist (none do yet, so these tests use real Core values) |
| Adapter integration tests | Does a real definition compile? Does a written tileset parse back? | One test project per adapter, against real Roslyn and temp directories |
| Architecture tests | Do references still point the right way? | `tests/Tilegen.ArchitectureTests` |
| Manual checks | Does the UI behave? Does Tiled's terrain brush paint correct transitions? | By hand, recorded in the PR |

There are no end-to-end or GUI tests and no performance suite. That is the owner's decision for now, not an oversight.

## How it applies here

- **Framework:** xUnit v3.
- **Architecture tests** use NetArchTest.Rules.
- **Property tests** use CsCheck, pinned in `Directory.Packages.props` and used by `Tilegen.Core.Tests`.
- **A shared test kit** owns the fakes, golden loading and exact pixel comparison, so no test project writes its own. It is `tests/Tilegen.TestKit`. It holds the golden comparer, its PNG helpers, the provenance reader and exact pixel comparison of frames (`Pixels`), and uses StbImageSharp and StbImageWriteSharp.

## Rules

- **R1.** A test MUST sit in the layer that answers its question. Core is tested by calling it with values, with no fakes and no engine. If the engine seems to need many unit tests, it holds decisions that belong in Core.
- **R2.** There are no end-to-end or GUI tests. The UI, and painting with an exported tileset in Tiled, are manual checks, and a PR that touches either MUST say which manual checks were done.
- **R3.** Every technique MUST have at least one golden: a fixed description, parameters and seed whose output must equal a stored image exactly.
- **R4.** Goldens are stored as PNG and MUST be compared as decoded pixels, never as file bytes. PNG is lossless, so a golden can be opened and looked at; its compressed bytes are not stable.
- **R5.** Every golden MUST record its provenance beside it: the description or parameters and the seed that produced it, the .NET version, and the change in which it was approved.
- **R6.** The owner approves every golden by eye, and a golden MUST NOT be recorded or re-recorded without that approval. When a golden test fails, stop, establish whether the change in output was intended, and show the before and after images. Never re-record to make a test pass.
- **R7.** A PR that re-records a golden MUST say so and give the reason the output changed. A .NET upgrade is a legitimate reason (`functional-core R8`); it still needs the owner's look.
- **R8.** Fixtures and goldens MUST come from tilegen's own output or be made for the test. No third-party art or tilesets: the repository is public.
- **R9.** Invariants SHOULD be property tests: the same seed gives the same pixels; a technique leaves its inputs unchanged; adjacent transition tiles match along their shared edge; a Wang set covers every combination it claims.
- **R10.** Export MUST be proven structurally: a test parses the written Tiled file and checks tile count, grid settings and Wang-set coverage. A snapshot of the text alone is not enough.
- **R11.** The definition loader MUST be proven by integration tests that compile a small real definition and check the description it returns, and that produce each failure case.
- **R12.** Tests MUST pass in parallel: no sleeps, wall clock, network or shared mutable statics. Each test that touches disk uses its own temporary directory and removes it. To wait for a condition, such as a file watcher noticing a change, poll for the condition within a limit; don't sleep for a guess.
- **R13.** Fix every flake at its cause. No whole-test retries, no skips, no longer waits on passing runs.
- **R14.** Prefer hand-written fakes to mocks. Use a mock only when the test is about a specific interaction.
- **R15.** Assert an expected failure as its outcome case. Never assert a throw for one (`railway R1`).
- **R16.** Integration tests SHOULD carry the trait `Category=Integration`, so a fast run can leave them out with `dotnet test --filter-not-trait "Category=Integration"`.
- **R17.** Architecture tests MUST assert the required references as well as the forbidden ones, so a rule can't pass because a type was renamed away.
- **R18.** Name tests by behaviour, such as `Erode_leaves_its_input_unchanged`, and shape them as arrange, act, assert.

## Anti-patterns

- **Re-recording a golden because the test failed.** Instead: stop and show the owner the difference (R6).
- **Comparing PNG files byte for byte.** Instead: decode and compare pixels (R4).
- **A downloaded tileset as a fixture.** Instead: generate one or draw a minimal one for the test (R8).
- **Testing a technique by running the app or the engine.** Instead: enumerate it in a Core test (R1).
- **`Thread.Sleep` before checking the file watcher fired.** Instead: poll for the condition (R12).
- **A snapshot of the `.tsj` text as the only export test.** Instead: parse it and check the structure (R10).
- **A CI retry for a failing test.** Instead: find the cause (R13).

## Enforcement

- **A banned-API list for test projects** is in place: `tests/BannedSymbols.txt`, applied to every project under `tests/`, with RS0030 as an error. It bans `Thread.Sleep`, every overload of `Task.Delay`, and `DateTime.Now` and `UtcNow`. Covers R12 in part: a shared static or network call is still a review finding.
- **The test kit's golden comparer** is built (`Golden` in `tests/Tilegen.TestKit`). It decodes the golden before comparing, so it compares pixels and never bytes. On anything but a match it writes the actual image, and the expected one on a size or pixel mismatch, to `golden-results/<technique>/` in the test output for inspection (the explicit-directory overloads write where they are told). It has no record mode: nothing writes into a goldens directory. Covers R4 and supports R5 (it reads the provenance) and R6. Its own tests are in `tests/Tilegen.Core.Tests/TestKit/`.
- **Property tests in place** (R9): each technique has a determinism property, in `tests/Tilegen.Core.Tests/Techniques/ValueNoiseProperties.cs`, `ThresholdProperties.cs` and `CutTileProperties.cs`. Threshold and the cut tile, the techniques that take an input, also have the inputs-unchanged property, in `ThresholdProperties.cs` and `CutTileProperties.cs`, and the Engine scenario `The_sources_entries_survive_the_reader` checks that a run leaves a source's timeline entries unchanged. That each new technique gets its properties is review.
- **CI** (`.github/workflows/build.yml`) builds and runs every test project on Windows on each push to main and each pull request, and fails a test project that runs no tests.
- **Review alone:** R1 to R3, R5 to R11, R13 to R16 and R18, and R17 in part. The architecture tests assert the required references, and review checks that each new rule does too. R6 depends on the owner: nothing mechanical stops a re-record.

## PR checklist

- [ ] Each new test sits in the right layer; Core is tested without fakes or the engine.
- [ ] A new or changed technique has a golden with its provenance, approved by the owner.
- [ ] Any re-recorded golden is named in the PR with the reason, and was approved.
- [ ] No third-party art in fixtures.
- [ ] No sleep, wall clock, network or shared static; temporary directories are unique and cleaned up.
- [ ] Expected failures are asserted as outcome cases.
- [ ] The manual checks done are listed, when the UI or the export changed.

## References

- Architecture plugin skill: `testing-architecture`.
- `functional-core.md` R8 for what determinism does and doesn't promise.
