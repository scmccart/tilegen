# Tasks

Each bite check is a scratch edit, a run whose output shows the expected failure, and a revert that leaves the build clean. Record each one (violation, command, the diagnostic or failing test) for the PR description's bite table (design.md, "Showing each check bite").

## 1. Solution, shared build settings and the layer projects

- [x] 1.1 Add `global.json` (SDK 11.0.100, `rollForward: latestFeature`, MTP test runner), root `Directory.Build.props`, `Directory.Packages.props` (central package management, no versions yet beyond what this group uses) and `.gitignore` (bin, obj, .vs, *.user, TestResults, *.binlog), as in design.md "Build settings". Verify `dotnet --version` in the repo prints an 11.0.x SDK.
- [x] 1.2 Create `tilegen.slnx` with `/src` and `/tests` solution folders, and `src/Tilegen.Core`, `Tilegen.Engine`, `Tilegen.Scripting.CSharp` and `Tilegen.Export`, each with only its marker type and the project references in design.md "Layout and names". Core removes the `System.IO` and `System.Threading.Tasks` implicit usings. Verify that `dotnet build` succeeds with 0 warnings, and that `git ls-files --eol` shows every new text file as `i/lf`.
- [x] 1.3 Update `CLAUDE.md`: in "Build and test", replace "There is no solution yet…" with the `dotnet build` command as run in 1.2, and say package versions are pinned in `Directory.Packages.props`. In "Architecture", replace "These projects are planned names; none exists yet…" with the fact that they exist and are checked (detail lands with groups 2 to 6). In `docs/architecture/README.md`, replace the "planned names" paragraph with "the names are final". Verify with `grep -n "no solution yet\|planned names" CLAUDE.md docs/architecture/README.md` returning nothing.

## 2. Compiler checks

- [x] 2.1 Add `.editorconfig` as in design.md "`.editorconfig`": `end_of_line = lf`; CS8509 and RS0030 as errors; CS8524 `none`, with a comment giving the reason. Verify `dotnet build` stays clean.
- [x] 2.2 Bite check: in Core, add switch expressions that miss a case of a `closed` record hierarchy, of a `union` and of a named enum member, and show 3 CS8509 errors. Add a complete enum switch expression and show it compiles. Rerun the missing-case violations with `-p:TreatWarningsAsErrors=false` and show they are still errors. Revert, and verify the build is clean.
- [x] 2.3 Bite check: a possible null return (CS8603) and an unused local (CS0168) each fail the build. Revert, and verify the build is clean.
- [x] 2.4 Update the Enforcement sections of `docs/architecture/illegal-states.md` and `docs/architecture/railway.md`: CS8509 is an error and covers switch *expressions* over closed types, unions and enums; switch *statements* are not checked by the compiler (IDE0010 also flags complete switches and wants a `default`) and stay with review; CS8524 is silenced, and why. Verify neither section still says "Planned".

## 3. Core's banned APIs

- [x] 3.1 Add the BannedApiAnalyzers package (`PrivateAssets="all"`) and `src/Tilegen.Core/BannedSymbols.txt` with exactly the `functional-core.md` list, including both `Random` constructors. Each entry's message names its rule. Verify `dotnet build` stays clean.
- [x] 3.2 Bite check: one scratch file in Core that uses every entry. Show one RS0030 per entry, and count the entries against the errors; a missing error means a misspelt entry. Rerun with `-p:TreatWarningsAsErrors=false` and show RS0030 is still an error. Revert, and verify the build is clean.
- [x] 3.3 Update `functional-core.md` Enforcement and `.claude/rules/core.md` "Enforced by" for the banned list. Say that RS0030 does not see a banned type used only in a signature (a `CancellationToken` parameter, a `Task` return), so that part of R1 and R10 stays with review. Verify the banned-list bullet no longer says "Planned".

## 4. Desktop smoke window

- [x] 4.1 Create `src/Tilegen.Desktop` (WinExe, references Engine, Scripting.CSharp and Export, plus MonoGame.Framework.DesktopGL 3.8.6.1 and Apos.Gui 3.1.0) with an explicit `Program` class in `Tilegen.Desktop` and `SmokeGame` as in design.md "Desktop smoke window". The label text names the wired layer assemblies through their markers. Add `Fonts/FiraSans-Regular.ttf` and `Fonts/OFL.txt`, both copied to output. Verify that `dotnet build` is clean, that the font and licence are in `bin/`, and that `git check-attr binary` on the TTF reports binary.
- [x] 4.2 With the owner's go-ahead, run `dotnet run --project src/Tilegen.Desktop`. The owner confirms that the window opens and the label renders.
- [x] 4.3 Update the docs:
  - `CLAUDE.md`: the run command in "Build and test", and the font sentence in Conventions (the UI font is the one third-party asset, under OFL, with its licence beside it).
  - `ports-and-adapters.md`'s table: Desktop's packages, and that Apos.Gui brings MonoGame.Extended, Apos.Shapes and Apos.Input.
  - `.claude/rules/desktop.md`: "Enforced by" is left for group 6.

  Verify the run command in `CLAUDE.md` is the one used in 4.2.

## 5. Test projects and the test banned list

- [x] 5.1 Create `tests/Directory.Build.props` (imports the root props; adds BannedApiAnalyzers and `tests/BannedSymbols.txt` with `Thread.Sleep`, `Task.Delay`, and `DateTime.Now` and `UtcNow`) and `tests/Tilegen.TestKit` (class library, references Engine, no types). Add `tests/Tilegen.Core.Tests`, `Tilegen.Engine.Tests`, `Tilegen.Scripting.CSharp.Tests` and `Tilegen.Export.Tests`, each Exe on xunit.v3 4.0.3, referencing its layer and TestKit, and each with one placeholder test as described in design.md "Test projects and MTP". Add all of them to `tilegen.slnx`. Verify that `dotnet build` is clean and `dotnet test` passes with 4 tests.
- [x] 5.2 Verify the test commands: run `dotnet test --project tests/Tilegen.Core.Tests` and an xUnit trait filter that leaves out integration tests (check xUnit's MTP option names first). Record the exact commands that ran.
- [x] 5.3 Bite check: a test using every entry in the test banned list shows one RS0030 per entry. Also show that emptying a test project makes `dotnet test` fail with exit code 8. Revert, and verify `dotnet test` passes.
- [x] 5.4 Update the docs:
  - `CLAUDE.md` "Build and test": the commands from 5.1 and 5.2, as run.
  - `testing.md`: in "How it applies here", NetArchTest is pinned, CsCheck is still to be pinned by the first change that writes a property test, and the test kit exists but is empty. In Enforcement, the test banned list is in place, and the golden comparer is deferred to the first technique change (owner decision, 2026-11-14).
  - `.claude/rules/tests.md` "Enforced by".

  Verify no Enforcement line in `testing.md` says "Planned" without a reason.

## 6. Architecture tests

- [ ] 6.1 Create `tests/Tilegen.ArchitectureTests` (xunit.v3, NetArchTest.Rules 1.3.2, references all five src projects) with the forbidden-dependency, required-dependency and anti-vacuity tests from design.md "Architecture tests". Verify `dotnet test --project tests/Tilegen.ArchitectureTests` passes.
- [ ] 6.2 Add the declared-references tests over each project's `obj/project.assets.json`: exact project references for src projects and TestKit, and a package allowlist matching the `ports-and-adapters.md` table. Verify they pass, and that each assertion names the project and the offending reference in its failure message.
- [ ] 6.3 Bite check, one at a time, reverting each:
  - Core uses `System.IO.Path`.
  - Scripting.CSharp references Export and uses its marker.
  - Engine's marker stops using Core.
  - A Core type is in namespace `Elsewhere`.
  - Core gains an unused package reference.
  - Engine gains an unused reference to Apos.Gui.

  Each fails the named test. Verify `dotnet test` passes after the last revert.
- [ ] 6.4 Update the docs:
  - Enforcement in `functional-core.md` (architecture tests, project references), `ports-and-adapters.md` (architecture tests and declared references; the table is mirrored by the allowlist, so a new package updates both) and `illegal-states.md` (R14 is structural).
  - `docs/architecture/README.md` "Enforcement status": what is in place, the NetArchTest choice and why not ArchUnitNET or reflection, and what is deferred or not possible.
  - "Enforced by" in `.claude/rules/core.md`, `engine.md`, `adapters.md` and `desktop.md`.

  Verify `grep -rn "Planned\|nothing yet\|scaffold change" CLAUDE.md docs/architecture .claude/rules` shows only statements that are still true, each with its reason.

## 7. CI and Dependabot

- [ ] 7.1 Add `.github/workflows/build.yml` as in design.md "CI and Dependabot". First confirm the current Windows runner label and the `actions/setup-dotnet` major version. Push the branch and verify the job is green.
- [ ] 7.2 Bite check: push a commit with a failing architecture test (subject passes `scripts/lint-commits`) and show the job red, then push its revert and show it green.
- [ ] 7.3 Add the `nuget` entry to `.github/dependabot.yml` with `prefix: chore` and `include: scope`, and update its header comment. Verify the YAML parses, and that the schedule and commit-message block match the existing entry.
- [ ] 7.4 Update `testing.md` Enforcement (CI runs every test project on each push and pull request) and `CLAUDE.md` (the build workflow, beside the existing commit workflows). Verify both name `.github/workflows/build.yml`.

## 8. Integration check

- [ ] 8.1 From a clean clone of the branch (`core.symlinks` on), run `dotnet build` and `dotnet test`. Verify 0 warnings, every test project passing, and every `.claude/skills/openspec-*` entry a symlink.
- [ ] 8.2 Read every Enforcement section, every "Enforced by" line and `CLAUDE.md` against what was built. Verify each rule is listed as enforced (by what), as reviewed, or as deferred with its reason, and that nothing still describes the scaffold as future work.

## Workflow follow-up

- Archive the change inside the PR (`docs(openspec): archive scaffold-solution`), so `main` never holds an unarchived change.
- Open one PR titled `build: scaffold the solution and enforce the layer rules` (check it with `scripts/lint-commits --title`). Its description carries the bite table, the manual check done (the smoke window), and the rules now enforced versus reviewed. Squash-merge it after CI is green and the owner approves.
