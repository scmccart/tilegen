# Proposal

## Why

tilegen has normative architecture docs but no code, so none of their rules is enforced: a reference that points the wrong way, a clock read in Core or a switch that misses a case would wait for review. This change creates the solution and puts the build-time and test-time checks the docs promise in place before the first feature, so that feature adds code and nothing else.

## What Changes

- A `.slnx` solution at the root, with one project per layer under `src/`: `Tilegen.Core`, `Tilegen.Engine`, `Tilegen.Scripting.CSharp`, `Tilegen.Export` and `Tilegen.Desktop`. Project references follow the dependency rule exactly. The planned names are kept.
- Test projects on xUnit v3 under `tests/`: `Tilegen.Core.Tests`, `Tilegen.Engine.Tests`, `Tilegen.Scripting.CSharp.Tests`, `Tilegen.Export.Tests`, `Tilegen.ArchitectureTests`, and an empty `Tilegen.TestKit` for the first fake or golden helper. All run on Microsoft.Testing.Platform through `dotnet test`.
- Shared build settings: `global.json` pinning SDK 11.0.100 and the MTP test runner; `Directory.Build.props` with `net11.0`, nullable and `TreatWarningsAsErrors`; central package management in `Directory.Packages.props`, with every version pinned.
- `.editorconfig` with `end_of_line = lf`, CS8509 as an error, and CS8524 silenced so a complete enum switch needs no discard arm.
- `src/Tilegen.Core/BannedSymbols.txt` with the list from `functional-core.md`, and a banned list for test projects from `testing.md`, both with RS0030 as an error.
- Architecture tests (NetArchTest) that assert the forbidden and the required dependencies between layers and packages, plus a test over the project files that checks declared references, which IL-based checks can't see.
- A demonstration, recorded in the PR description, that each check fails on a deliberate violation, including a switch over a C# 15 `closed` type that misses a case.
- A smoke check in `Tilegen.Desktop`: MonoGame DesktopGL with Apos.Gui opening a HiDef window with one label, using a bundled OFL-licensed font.
- CI: a GitHub Actions job on Windows that builds and runs every test project. Dependabot gains a NuGet entry with the existing commit prefix conventions.
- A `.gitignore` for .NET build output.
- The docs brought up to date: real build and test commands in `CLAUDE.md`, and every "planned" or "nothing yet" enforcement statement in `CLAUDE.md`, `docs/architecture/` and `.claude/rules/` replaced with what is now true, including the checks that turned out impossible or are deferred, and why.

### Out of scope

- Any product behaviour: no technique, pipeline model, pixel buffer, definition loading, file watching, export, or UI beyond the smoke label.
- The golden comparer the docs promise. It needs a PNG decoder and a pixel type, which nothing has yet; it arrives with the first technique (owner decision, 2026-11-14). `Tilegen.TestKit` exists empty so that change adds no project.
- Packages nothing uses yet: CsCheck, Roslyn, a PNG encoder, the MonoGame content pipeline.
- Linux or macOS CI.
- A custom Roslyn analyzer. Switch *statements* over closed types are not checked by the compiler, and stay with review.

## Capabilities

### New Capabilities

None. This change adds no spec-level behaviour, so `.openspec.yaml` sets `skip_specs: true`.

### Modified Capabilities

None.

## Impact

- New: the solution, 5 source projects, 6 test projects, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, a font and its licence, `.github/workflows/build.yml`.
- Changed: `.github/dependabot.yml`, `CLAUDE.md`, `docs/architecture/*.md`, `.claude/rules/*.md`.
- Dependencies, all pinned: MonoGame.Framework.DesktopGL 3.8.6.1, Apos.Gui 3.1.0, Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0, xunit.v3 4.0.3, NetArchTest.Rules 1.3.2.
- Developers need the .NET 11 SDK (11.0.100 or a later feature band) and `core.symlinks` on.
