# Design

## Context

There is no code yet (see proposal.md, "Why"). The constraints come from `CLAUDE.md` and `docs/architecture/`: .NET 11 and C# 15, the dependency rule, and the Enforcement section of each architecture doc. Before this design was written, a throwaway spike outside the repository tested each unknown on this machine (Windows 11, SDK 11.0.100) on 2026-11-14. Its findings are recorded under "Spike findings" below, because implementers see only this document and their brief.

Owner decisions taken while designing (2026-11-14): `.slnx`; central package management; the planned project names unchanged; CS8524 silenced; the golden comparer deferred to the first technique change.

## Goals / Non-Goals

**Goals:**

- Every check the docs promise is either in place and shown failing on a violation, or recorded in the docs as deferred or not possible, with the reason.
- Every check is independent of the others. Each fails by itself, so none relies on warnings as errors or on another check to bite.
- A later feature adds code, packages and tests only: no project, test project, analyzer setting or CI job.

**Non-Goals:**

- A custom Roslyn analyzer, for example one that flags switch statements over closed types. That is more machinery than the scaffold needs. Review covers it for now.
- Lock files (`RestorePackagesWithLockFile`), NuGet caching in CI, a Release build in CI, and code coverage.
- Making the new CI job a required check. That is a repository setting the owner changes after merge.

## Layer mapping

| Layer | What this change puts there |
| --- | --- |
| Core | A marker type and the banned-API list. No logic, no outcome types. |
| Engine | A marker type that uses Core's, so the Engine → Core dependency exists in IL. No ports. |
| Adapters | One marker type each, using Engine's. No Roslyn, no PNG package. |
| Desktop | The composition root and the smoke window: a MonoGame `Game` with HiDef and one Apos.Gui label. The label text names the layer assemblies it was wired with, so Desktop's references to both adapters are real uses. It contains no decisions. |

No outcome types are introduced, because no operation in this change can fail in an expected way.

## Decisions

### Layout and names

```
tilegen.slnx                    solution folders /src and /tests
global.json                     SDK 11.0.100, rollForward latestFeature; test runner Microsoft.Testing.Platform
Directory.Build.props           net11.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
Directory.Packages.props        every version, ManagePackageVersionsCentrally
.editorconfig                   end_of_line = lf; CS8509, RS0030 error; CS8524 none
.gitignore
src/Tilegen.Core/               CoreMarker.cs, BannedSymbols.txt
src/Tilegen.Engine/             EngineMarker.cs
src/Tilegen.Scripting.CSharp/   ScriptingMarker.cs
src/Tilegen.Export/             ExportMarker.cs
src/Tilegen.Desktop/            Program.cs, SmokeGame.cs, Fonts/FiraSans-Regular.ttf, Fonts/OFL.txt
tests/Directory.Build.props     imports the root props; adds the test banned list
tests/BannedSymbols.txt
tests/Tilegen.TestKit/          class library, empty (no types yet)
tests/Tilegen.Core.Tests/
tests/Tilegen.Engine.Tests/
tests/Tilegen.Scripting.CSharp.Tests/
tests/Tilegen.Export.Tests/
tests/Tilegen.ArchitectureTests/
.github/workflows/build.yml
```

- Each project's root namespace is its assembly name. The architecture tests match dependencies by namespace, so a test asserts this mapping (see Architecture tests).
- Desktop uses an explicit `Program` class in `Tilegen.Desktop`, not top-level statements, which would put `Program` in the global namespace.
- Project references, with nothing else declared:
  - Engine → Core.
  - Scripting.CSharp and Export → Engine, and get Core transitively. The ports table allows a direct reference to Core, but nothing needs one.
  - Desktop → Scripting.CSharp, Export and Engine.
  - Tilegen.TestKit → Engine, because it will hold fakes of Engine's ports and golden helpers over Core values.
  - Each `*.Tests` project → its layer and TestKit. ArchitectureTests → all five src projects.
- Markers are `public static class <Layer>Marker` with a doc comment saying they exist so the layer's assembly and its dependency are real, and may be deleted once real types do that job. Engine's marker exposes `typeof(CoreMarker)`; each adapter's exposes `typeof(EngineMarker)`. The required-dependency tests ask whether *any* type in the assembly depends on the inner namespace, so they keep passing after a marker is deleted, as long as real code makes the dependency.

### Build settings

- **`global.json`:** SDK `11.0.100` with `rollForward: latestFeature`, so any 11.0.x SDK builds the code and 10.x never does. `"test": { "runner": "Microsoft.Testing.Platform" }` puts `dotnet test` in MTP mode. This is the documented mechanism for the .NET 10 SDK and later; the .NET 11 `DOTNET_TEST_RUNNER` variable is not used.
- **Root `Directory.Build.props`:** `TargetFramework net11.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`. `LangVersion` is not set, because net11.0 defaults to C# 15.
- **Core opts out of two implicit usings:** `System.IO` and `System.Threading.Tasks`, which hold the banned `File`, `Directory` and `Task`. This is a convenience so those names aren't in scope by default. The banned list and the architecture tests are the checks.
- **Central package management (owner decision):** `Directory.Packages.props` holds every `PackageVersion`. Transitive pinning stays off, because nothing needs it.
- **Pinned versions** (NuGet, 2026-11-14):

  | Package | Version | Used by |
  | --- | --- | --- |
  | MonoGame.Framework.DesktopGL | 3.8.6.1 | Desktop |
  | Apos.Gui | 3.1.0 | Desktop (brings Apos.Shapes 0.9.3, Apos.Input 3.2.0, MonoGame.Extended 5.3.1 transitively) |
  | Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | Core and every project under `tests/`, with `PrivateAssets="all"` |
  | xunit.v3 | 4.0.3 | test projects. Package 4.x is still the xUnit v3 framework, and defaults to MTP v2. |
  | NetArchTest.Rules | 1.3.2 | ArchitectureTests |

  Not added, because nothing uses them yet: `MonoGame.Content.Builder.Task` (the font is copied, not built), CsCheck, Roslyn, a PNG encoder, `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` (VSTest only).

### `.editorconfig`

`root = true`. `[*]` sets `end_of_line = lf`, `charset = utf-8`, `insert_final_newline = true` and the usual indentation (4 for `.cs`, 2 for XML, JSON and YAML). `[*.cs]` sets:

- `dotnet_diagnostic.CS8509.severity = error`: a switch *expression* that misses a case of a `closed` type, a `union` or a named enum member.
- `dotnet_diagnostic.RS0030.severity = error`: a banned API.
- `dotnet_diagnostic.CS8524.severity = none` (owner decision), with a comment. With it on, a switch expression that handles every *named* enum member still fails, and only a discard arm fixes it, which is the arm `illegal-states R5` forbids. With it off, a missing named member still fails with CS8509.

`end_of_line = lf` is a setting, not a check: it makes editors and `dotnet format` agree with `.gitattributes`, which is what normalises line endings.

### Banned APIs

- **`src/Tilegen.Core/BannedSymbols.txt`** is exactly the `functional-core.md` list. Each entry carries the rule it enforces as its message, for example `P:System.DateTime.Now;No clock in Core (functional-core R1)`. `Random` has two constructors, and both are listed.
- **`tests/BannedSymbols.txt`**, applied to every project under `tests/` through `tests/Directory.Build.props`, covers `testing R12`: `Thread.Sleep`, `Task.Delay`, and `DateTime.Now` and `UtcNow`. `UtcNow` is added because R12 bans the wall clock, not one property. Overloads are listed one by one, unless the bite check shows that the no-parameter form bans them all.
- **A misspelt entry silently bans nothing,** with no diagnostic (spike finding). So the bite check exercises every entry, and the PR records one RS0030 per entry.
- **RS0030 does not fire on a banned type that appears only in a signature** (spike: a `CancellationToken` parameter and an `async Task<int>` return were not flagged). That gap is left to review: a `CancellationToken` or `Task` in a Core signature is a review finding under `functional-core R10`, and the docs say so.

### Architecture tests (NetArchTest.Rules 1.3.2)

**Library choice:** NetArchTest, over ArchUnitNET 0.13.5 and a bare reflection check.

- ArchUnitNET passed vacuously on a `System.IO` dependency in the spike, until CoreLib was loaded into its architecture as well. That is exactly the failure `testing R17` guards against.
- A reflection check over `GetReferencedAssemblies()` sees only assembly names (`System.Runtime`), so it can't express "no `System.IO` namespace".
- NetArchTest caught both the `System.IO` use and a `CancellationToken` parameter unaided, and reads net11.0 assemblies with Mono.Cecil 0.11.3.
- Its last release is 2021 (MIT). If it stops reading a future runtime's assemblies, the fallback is its maintained fork, NetArchTest.eNhancedEdition, with the same API.

**Forbidden dependencies, by namespace prefix:**

| Assembly | Must not depend on |
| --- | --- |
| Core | `Tilegen.Engine`, `Tilegen.Scripting`, `Tilegen.Export`, `Tilegen.Desktop`, `Microsoft.Xna.Framework`, `MonoGame`, `Apos`, `Microsoft.CodeAnalysis`, `System.IO` |
| Engine | `Tilegen.Scripting`, `Tilegen.Export`, `Tilegen.Desktop`, `Microsoft.Xna.Framework`, `MonoGame`, `Apos`, `Microsoft.CodeAnalysis` |
| Scripting.CSharp | `Tilegen.Export`, `Tilegen.Desktop`, `Microsoft.Xna.Framework`, `MonoGame`, `Apos` |
| Export | `Tilegen.Scripting`, `Tilegen.Desktop`, `Microsoft.Xna.Framework`, `MonoGame`, `Apos`, `Microsoft.CodeAnalysis` |

- The namespaces are the ones the docs list, with one addition: `MonoGame` is listed separately because Apos.Gui brings `MonoGame.Extended`, which is not under `Microsoft.Xna.Framework`.
- Engine's "imaging namespace" rule has nothing to name until Export picks a PNG package. Until then the declared-references test keeps any package out of Engine.

**Required dependencies** (`testing R17`):

- Engine depends on `Tilegen.Core`.
- Scripting.CSharp and Export each depend on `Tilegen.Engine`.
- Desktop depends on `Tilegen.Engine`, `Tilegen.Scripting.CSharp`, `Tilegen.Export`, `Microsoft.Xna.Framework` and `Apos`.

**Anti-vacuity:**

- Every assembly under test is obtained by `typeof(XMarker).Assembly`, so a rename breaks the build rather than emptying the test.
- One test asserts that each assembly has at least one type, and that every type in it lives under the assembly's root namespace. Compiler-generated types are excluded.

**Declared references:**

- IL-based checks see only references the code uses (spike finding), so an unused forbidden `PackageReference` or `ProjectReference` passes them. A second test class reads each project's `obj/project.assets.json`, which NuGet writes on restore with `Directory.Build.props` already applied. It asserts:
  - project references: the exact set listed under "Layout and names", for every src project and TestKit;
  - packages: a subset of an allowlist. Core may have only BannedApiAnalyzers, private. Engine has none. Scripting.CSharp may have `Microsoft.CodeAnalysis.CSharp` and Export none yet, both from the ports-and-adapters table. Desktop may have MonoGame.Framework.DesktopGL and Apos.Gui.
- The allowlist is the ports-and-adapters table in code. A new package updates both in the same change, and the table says so.
- The repository root is found by walking up from `AppContext.BaseDirectory` to `tilegen.slnx`.

`illegal-states R14` (the export wire types stay out of Core and Engine) is then structural: neither may depend on `Tilegen.Export` at all.

### Test projects and MTP

- **Each test project** is `OutputType Exe` and references `xunit.v3`.
- **A test project with no tests fails `dotnet test` with exit code 8** (spike finding). Each layer test project therefore holds one placeholder test, named for what it checks, for example `Core_tests_run_against_Tilegen_Core`. It asserts that the layer's assembly is the one referenced, and a comment says the first real test replaces it.
  - Rejected alternative: `--ignore-exit-code 8`. A project that lost all its tests would then pass silently, which is a disabled check.
- **The commands written into `CLAUDE.md`** must have been run in this change:
  - `dotnet build`
  - `dotnet test`
  - `dotnet test --project tests/<Project>`
  - `dotnet run --project src/Tilegen.Desktop`
  - an xUnit trait filter for `testing R16`, recorded only if it runs.

### Desktop smoke window

- **The window:** `SmokeGame : Game` sets `GraphicsProfile.HiDef` in its constructor, loads the font through `TitleContainer.OpenStream("Fonts/FiraSans-Regular.ttf")` into `ShapeFont`, calls `GuiHelper.Setup`, and puts one `Label` inside `MenuPanel.Push/Pop` in `Update`. The pattern is from the Apos.Gui getting-started guide; the spike ran it.
- **The font:** Fira Sans Regular, SIL OFL 1.1 with no Reserved Font Name, from the google/fonts repository. OFL allows bundling with MIT software if the licence travels with the font, so `Fonts/OFL.txt` is committed beside it and copied to the output.
  - `CLAUDE.md`'s "no third-party art" rule gains a sentence: the UI font is the one third-party asset, and it is under OFL.
- **The check:** the window is opened by hand, with the owner's go-ahead because it takes focus, and the owner confirms it.

### CI and Dependabot

- **`.github/workflows/build.yml`:**
  - Triggers: `push` to main, `pull_request`, `workflow_dispatch`.
  - `permissions: contents: read`, and the same concurrency block as `commits.yml`.
  - One job, `build`, on `windows-2025`. Its steps are `actions/checkout@v7`, `actions/setup-dotnet@v6` with `global-json-file: global.json`, `dotnet build`, then `dotnet test --no-build`.
  - The implementer confirms the runner label and setup-dotnet's current major version when writing the file.
- **`dependabot.yml`:** a `nuget` entry for `/` with the same `monthly` schedule and `commit-message: { prefix: chore, include: scope }`, giving `chore(deps): bump …`. The header comment is updated to cover both ecosystems.

### Showing each check bite

Each check gets a deliberate violation in a scratch edit, a build or test run whose output shows the expected diagnostic or test failure, and a revert that brings the run back to clean. The table goes in the PR description.

| Check | Violation |
| --- | --- |
| RS0030, Core | One file using every entry in Core's list; expect one RS0030 per entry |
| RS0030, tests | One test using every entry in the test list |
| CS8509 | A switch expression missing a case of a `closed` record hierarchy; of a `union`; of a named enum member. Also show a complete enum switch compiles (CS8524 off). |
| The severities alone | The CS8509 and RS0030 violations again with `-p:TreatWarningsAsErrors=false`: still errors |
| Nullable | A possible null return, CS8603 |
| `TreatWarningsAsErrors` | An ordinary warning such as CS0168 becomes an error |
| Forbidden dependency | Core uses `System.IO.Path`; Scripting.CSharp references Export and uses its marker |
| Required dependency | Engine's marker stops using Core |
| Namespace mapping | A type in Core declared in namespace `Elsewhere` |
| Declared references | Core gains an unused package reference; Engine gains an unused reference to Apos.Gui |
| CI | A pushed commit with a failing architecture test turns the job red; a revert commit turns it green. Both disappear in the squash. |

The dependency rule's cycle cases (Core → Engine) need no demonstration: MSBuild refuses a circular reference before any check runs.

## Spike findings

From the 2026-11-14 spike. These contradict or sharpen the docs, and the doc updates must reflect them.

1. **MonoGame 3.8.6.1 with Apos.Gui 3.1.0 restores, builds with zero warnings, and opens a HiDef window with a label on net11.0.** The owner saw it render.
2. **CS8509 covers switch expressions only.** A switch *statement* over a `closed` type or a `union` that misses a case gets no diagnostic. IDE0010 can't fill the gap: it also fires on complete switch statements and on `int` and `string` switches, and wants a `default`. `illegal-states R5` and `railway R2` are therefore compiler-checked for switch expressions and reviewed for statements, and the docs say so.
3. **CS8524** makes complete enum switch expressions fail. It is silenced, per the decision above.
4. **RS0030 misses banned types that appear only in signatures,** and **a misspelt entry bans nothing.** See "Banned APIs".
5. **The IL-based checks see only used references,** and **ArchUnitNET can pass vacuously.** See "Architecture tests".
6. **MTP exits 8 when no test runs.** See "Test projects and MTP".
7. **`dotnet test` and `dotnet test --solution <file>.slnx` both work** in MTP mode on SDK 11.0.100.

## Risks / Trade-offs

- [NuGet audit runs under warnings as errors, so a new advisory against a pinned package can turn the build red with no code change] → That is the desired signal. Bump the package, or suppress the one advisory with a dated comment, never `NuGetAudit=false`.
- [NetArchTest is unmaintained since 2021] → Its bite checks would show a regression. NetArchTest.eNhancedEdition is a drop-in fallback.
- [Reading `obj/project.assets.json` couples a test to NuGet's file layout and the default output paths] → It is a documented format. If the repository adopts artifacts output, the test's path helper changes in that change.
- [Banning `Task.Delay` in tests means the file-watcher polling helper `testing R12` asks for will need some wait primitive] → The Scripting.CSharp change designs that helper; any suppression it needs goes on one line, with a reason.
- [Placeholder tests are scaffolding in the test suite] → Each says so in its name and comment. The first real test in each project replaces it.
- [Silencing CS8524 lets an out-of-range enum value throw `SwitchExpressionException` at run time] → That is a bug, and the rules say bugs throw.

## Migration Plan

None. This is new code with nothing to migrate. Rollback is reverting the squash commit.

## Topic docs

None. This change adds no algorithm or mechanism of the kind `docs/design/` describes. The architecture docs and rule files are updated instead, as the proposal lists.
