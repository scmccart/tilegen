# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

tilegen is a personal desktop tool and testbench for procedurally generating game tilesets, starting with isometric tiles that have terrain transitions. It is C# 15 on .NET 11, with a MonoGame 3.8 (DesktopGL) window, Apos.Gui 3 for the UI, and xUnit v3 for tests. A pipeline of procedural techniques is described in a C# definition file, which the app compiles with Roslyn and hot-reloads. The output is a PNG atlas plus a Tiled tileset (`.tsj`/`.tsx`). Its author is its only user, and only Windows is supported and tested for now.

## Build and test

The solution is `tilegen.slnx`. Build everything from the repo root with `dotnet build`; warnings are errors, so a clean build has none. Run the app with `dotnet run --project src/Tilegen.Desktop`; it opens the window, so it takes focus.

- **Test:** `dotnet test -- --minimum-expected-tests 1` runs every test project and fails any project that runs no tests. Plain `dotnet test` reports a project that ran nothing as "Zero tests ran" but still exits 0. Keep the `--`: without it the option is silently ignored. `dotnet test --project tests/<Project>` runs one, for example `dotnet test --project tests/Tilegen.Core.Tests`. To leave out integration tests, run `dotnet test --filter-not-trait "Category=Integration"` (`testing R16`).
- **Tests run on Microsoft.Testing.Platform,** set in `global.json`. `dotnet test` therefore takes `--project` or `--solution` rather than a positional path, and xUnit's filter options (`--filter-trait`, `--filter-not-trait`, `--filter-class` and others) go straight on the command line.

- **The target is .NET 11, not .NET 10.** The architecture relies on C# 15 `closed` hierarchies and `union` types, so the compiler reports every switch that misses a case. Don't lower the target to make a package or tool happy; raise it as a design issue.
- **Package versions are pinned in `Directory.Packages.props`** (central package management; project files carry no versions). MonoGame 3.8.6 and Apos.Gui 3.1 were current when the stack was chosen (November 2026).
- **Apos.Gui needs the HiDef graphics profile,** because its Apos.Shapes dependency does. Its shaders ship precompiled inside the assembly, so there is no shader build step.
- **Every text file is LF, on every platform** (`.gitattributes`; batch files are the one CRLF exception). The scaffold's `.editorconfig` must set `end_of_line = lf` so editors and `dotnet format` agree with it.
- `scripts/lint-commits` checks commit messages; see Commit messages below.

## Conventions

- **Techniques live only in the app; a definition only configures and connects them.** Adding a technique is an app feature. A pipeline description is pure data, with no delegates and no types defined by a definition: that keeps the boundary checkable by the compiler and lets an old definition assembly unload after a reload.
- **Every technique is stepwise.** It is an iterator that yields a frame per iteration, because the app replays generation step by step, inside a stage as well as between stages.
- **Generation is deterministic: the same description and seed give the same pixels on the same .NET version and platform.** Goldens and "rerun only the stages that changed" depend on it. Don't promise more: floating-point maths functions and a seeded `System.Random` can both differ across runtimes.
- **Randomness comes only from the `System.Random` the engine passes in, one per stage.** Never `Random.Shared` or an unseeded `new Random()`. Don't write an RNG: using the framework's was the owner's decision.
- **Generation runs on the CPU over plain pixel buffers.** That keeps tests and export free of a window and a GPU. A technique that runs on the GPU, or a custom shader in tilegen's own code, needs a deliberate decision first.
- **Output is engine-neutral: a PNG atlas on a uniform rectangular grid plus a Tiled tileset, with transitions as Wang sets.** Tiled is the reference consumer. Don't add engine-native exporters (Godot `.tres`, Unity palettes); the likely consumers (Godot, MonoGame, Bevy, Flame) all load Tiled files.
- **Implement the Tiled format from its documentation and never copy code from Tiled's source.** Tiled is GPL; tilegen is MIT.
- **Implement algorithms from papers, articles and documentation, and cite the source in the technique's topic doc.** Copy code only from a source whose licence is compatible with MIT, with attribution in that doc.
- **Commit no third-party art or tilesets, including as test fixtures or goldens.** The repository is public. Goldens come only from tilegen's own output. The UI font (Fira Sans, SIL Open Font License 1.1) is the one third-party asset; its licence sits beside it in `src/Tilegen.Desktop/Fonts/`, and it is not art for tiles.
- **The owner approves every golden by eye. Never re-record a golden to make a failing test pass.** When a golden test fails, stop, work out whether the change in output was intended, and show the before and after images. A PR that re-records a golden says why the output changed.
- **Tests are unit and integration tests; there are no end-to-end or GUI tests.** The UI, and painting with the exported tileset's terrain brush in Tiled, are checked by hand. Say in the PR which manual checks were done.
- **Only Windows has to work, but keep Linux cheap.** Use MonoGame's DesktopGL flavour, and don't reach for a Windows-only API where a portable one costs nothing.
- **tilegen is not** a map or level editor, a pixel-art or paint tool, a runtime library for games to embed, an ML or diffusion image generator, or a product for other people. Don't add packaging, onboarding or polish aimed at other users.

## Architecture

The normative docs are in `docs/architecture/`; start at its `README.md`. Code review enforces each doc's Rules section and cites rule numbers. Read the relevant doc's Rules before a non-trivial change.

```
Tilegen.Desktop ─▶ Tilegen.Scripting.CSharp, Tilegen.Export ─▶ Tilegen.Engine ─▶ Tilegen.Core
                   references point only rightward
```

- **Core** is pure and references the .NET base library only: techniques, the pipeline model, isometric geometry, the Wang-set and tileset models.
- **Engine** holds the ports and drives runs: stepping techniques, keeping the frame timeline, reloading.
- **Scripting.CSharp** and **Export** are adapters: Roslyn loading and file watching; PNG encoding, the Tiled codec and file writing.
- **Desktop** is MonoGame and Apos.Gui. It draws, gathers input and wires everything together.

The rules agents break most:

- No IO, clock, statics or framework types (`Texture2D`, `Color`, `Point`) in Core.
- Every decision is a Core function. The Engine and Desktop dispatch on what Core returned; they don't decide.
- No interface exists for logic. Ports are for platform things only: compiling definitions, writing files.
- Expected failures are closed outcome types, switched over with no discard arm. Exceptions are for bugs.

Core, Engine, Scripting.CSharp and Export exist under `src/` (Desktop is still to come), and all the names are final. Their references follow the dependency rule, and checks that enforce it are being put in place; each architecture doc's Enforcement section and `docs/architecture/README.md` say which are in place.

## How work happens

- **Behaviour changes go through OpenSpec** (`/openspec-propose`): a new technique, or a change to the definition API, the export format or the app. Docs, CI, dependency bumps and pure refactors don't need a proposal.
- **One PR per change, squash-merged.** The PR carries the OpenSpec artifacts, the implementation, the topic doc updates and the archive step, so `main` never holds an unarchived change.
- **Subagents implement; the main session orchestrates.** Pick each subagent's model and effort with the `route` skill. The main session briefs them, checks their work against the architecture rules, keeps the change on track and handles escalations.
- **Topic design docs live in `docs/design/<topic>.md` and are kept current like specs.** Each describes how one algorithm or mechanism works. A change's `design.md` names the topic docs it creates or updates, and each is updated in the same task group as the code it describes. See `docs/design/README.md`.
- OpenSpec-only commits (propose, check-off, archive) are `docs(openspec): …`.

## Direction

- **Phase one is done when:** a pipeline of techniques is described in a C# definition and hot-reloaded on save; it generates an isometric tileset with transitions between at least two terrains from parameters and a seed; the app shows a live preview and a replayable step-by-step animation of the generation; and the exported PNG atlas and Tiled tileset open in Tiled, where the terrain brush paints correct transitions.
- **Deferred:** tiles with height (cliffs, walls), animation, other projections, engine-specific exporters, a visual pipeline editor, a codegen library to cut definition boilerplate, other scripting languages, Linux testing, and an integration that lets an agent operate tilegen.
- **Script integration is a seam, not a plugin system.** The engine consumes a pipeline description and doesn't know what produced it. Keep that seam; don't design a second language front-end before one is wanted.
- **After phase one: not decided yet,** on purpose. Don't plan past it.

## Commit messages

Commits follow Conventional Commits, checked by commitlint (`commitlint.config.mjs`):
- Subject is `type(scope)!: subject`. Types: `feat`, `fix`, `docs`, `test`, `ci`, `build`, `refactor`, `perf`, `style`, `chore`, `revert`.
- Scopes are lower-case and name the area touched. `deps` and `openspec` are the ones in use so far; add the others here as the project grows areas.
- The subject starts lowercase, except for an acronym (`docs: README, …`). Headers are at most 100 characters.
- `!` marks a breaking change (a changed key, config, API or default).
- Merges keep `Merge …`, which the linter skips.
- `git config core.hooksPath scripts/hooks` (once per clone) runs `scripts/lint-commits --edit` as a `commit-msg` hook. `scripts/lint-commits` alone checks the commits not on `origin/main`; `scripts/lint-commits BASE` checks `BASE..HEAD`; `scripts/lint-commits --title TITLE` checks a PR title.
- PRs are squash-merged, so the PR title becomes the commit on `main` with ` (#NN)` appended. Titles follow the same rules, and are at most 93 characters to leave room for the suffix. The squash commit's body is empty (the repository's squash default is the PR title alone), so mark a breaking change in the title with `!`.
- `.github/workflows/commits.yml` checks every pushed and PR commit; `.github/workflows/pr-title.yml` checks PR titles (a required check), with the script and rules taken from the base commit.
- It runs commitlint through `npx` (Node ≥ 22.12), with `COMMITLINT_VERSION` pinned in `scripts/lint-commits` and moved by hand. To vet a bump, run `scripts/lint-commits $(git rev-list --max-parents=0 HEAD)` (the root commit) over the whole history.
