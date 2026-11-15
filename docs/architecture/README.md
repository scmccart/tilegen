# tilegen architecture

These are the normative architecture docs for tilegen, a desktop testbench that procedurally generates game tilesets. "Normative" means code review enforces each doc's **Rules** section, and reviewers cite rule numbers from these files (`functional-core R3`).

The docs were adopted before any code existed. The project names below are final; type names in the illustrative code are not.

## Dependency rule

```
Tilegen.Desktop ─▶ Tilegen.Scripting.CSharp, Tilegen.Export ─▶ Tilegen.Engine ─▶ Tilegen.Core
                   references point only rightward
```

- **Core** (`src/Tilegen.Core`): pure. Techniques, the pipeline description and its validation, isometric geometry, the Wang-set and tileset models, and every decision. It does no IO, reads no clock, and uses no framework types. It references the .NET base library only.
- **Engine** (`src/Tilegen.Engine`): the shell's orchestration. It declares the ports, steps techniques, keeps the frame timeline, and reloads definitions. It references Core, and no UI, graphics, compiler or imaging package.
- **Adapters** (`src/Tilegen.Scripting.CSharp`, `src/Tilegen.Export`): one project per outside technology. Scripting.CSharp compiles definitions with Roslyn and watches their files. Export encodes PNG, writes the Tiled tileset and writes files. Each references Engine and its own technology.
- **Desktop** (`src/Tilegen.Desktop`): MonoGame and Apos.Gui. It draws, gathers input, and is the composition root.
- **A definition** references Core and nothing else of tilegen's.

## Read by layer

| Touching | Read first |
| --- | --- |
| anything | this page, then `functional-core.md` |
| `src/Tilegen.Core/**` | `functional-core.md`, `illegal-states.md`, `railway.md` |
| `src/Tilegen.Engine/**` | `functional-core.md`, `ports-and-adapters.md`, `railway.md` |
| `src/Tilegen.Scripting.*/**`, `src/Tilegen.Export/**` | `ports-and-adapters.md`, `railway.md` |
| `src/Tilegen.Desktop/**` | `ports-and-adapters.md`, `functional-core.md` |
| `tests/**` | `testing.md` |
| a technique | its topic doc in `docs/design/` as well |

## The docs

| Doc | One line |
| --- | --- |
| [functional-core.md](functional-core.md) | The spine: Core decides and is pure; the shell does effects. Techniques as iterators, determinism, randomness. |
| [ports-and-adapters.md](ports-and-adapters.md) | Where the boundaries to Roslyn, the filesystem and MonoGame go, and what may cross them. |
| [illegal-states.md](illegal-states.md) | The stage set as a closed type, validated pipelines, typed units and IDs. |
| [railway.md](railway.md) | Expected failures as closed outcome types: compile errors, validation findings, export failures, stopping a run. |
| [testing.md](testing.md) | Which kinds of test exist, goldens and who approves them, what is checked by hand. |

Not adopted: an entity component system (no entity simulation), a process architecture (one process), and legacy-port rules (nothing is being ported).

## Shared vocabulary

- **Core / shell:** Core is `Tilegen.Core`. The shell is everything else: Engine, the adapters and Desktop.
- **Technique:** an algorithm in Core that produces or transforms pixels, written as an iterator that yields a frame per step.
- **Stage:** one use of a technique in a pipeline, with its parameters and inputs.
- **Definition:** a C# source file the user writes. It runs once per reload and returns a pipeline description.
- **Pipeline description:** the pure-data value a definition returns: which stages, how they connect, with what parameters.
- **Validated pipeline:** a description that passed validation. It is the only thing the engine will run.
- **Frame:** the state of a stage's output at one step. The timeline is the frames the engine kept.
- **Port / adapter:** a port is an interface in Engine for something that is platform. An adapter implements it against a real technology; a fake implements it in memory for tests.
- **Outcome:** a closed hierarchy or union listing every way an operation can end.
- **Topic doc:** a file in `docs/design/` describing how one algorithm or mechanism works, kept current with the code.

## Enforcement status

These checks are in place. Each doc's Enforcement section says which of its rules they cover.

- **One project per layer,** with references that follow the dependency rule. The declared-reference test in `tests/Tilegen.ArchitectureTests` checks them from each project's `obj/project.assets.json`, so it sees a reference the code doesn't use yet. It mirrors the table in `ports-and-adapters.md`: every src project and the test kit has the project references the dependency rule requires and none the table doesn't allow, only the packages the table lists, and no framework reference beyond the base library. Raw DLL references don't appear in that file, so review catches them. The test kit isn't a layer in the table; its row follows `testing.md`.
- **Core's banned list:** `src/Tilegen.Core/BannedSymbols.txt` (Microsoft.CodeAnalysis.BannedApiAnalyzers), with RS0030 as an error.
- **The test banned list:** `tests/BannedSymbols.txt`, applied to every project under `tests/`.
- **The golden comparer:** `Golden` in `tests/Tilegen.TestKit`. It compares decoded pixels, writes the actual image (and the expected one on a mismatch) under `golden-results/<technique>/`, and has no record mode. Its tests are in `tests/Tilegen.Core.Tests/TestKit/`.
- **The compiler:** `.editorconfig` makes CS8509 (a switch expression that misses a case) an error and silences CS8524. Nullable is enabled, and warnings are errors.
- **The architecture tests:** `tests/Tilegen.ArchitectureTests`, on NetArchTest.Rules (the version is in `Directory.Packages.props`). They check each layer's forbidden namespaces and its required ones (`testing R17`), that every type sits under its assembly's root namespace, that each assembly shows at least one type to the rules, and that each forbidden row doesn't overlap the assembly's own namespace, so a rule that matches by namespace can't pass on nothing.
- **CI:** `.github/workflows/build.yml` runs the build and every test project on Windows, so each check above runs on each push to main and each pull request.

NetArchTest was chosen over two alternatives. ArchUnitNET 0.13.5 passed vacuously on a `System.IO` dependency unless CoreLib was loaded into its architecture too. A reflection check over `Assembly.GetReferencedAssemblies()` sees assembly names only, such as `System.Runtime`, so it can't express a namespace rule. NetArchTest has been unmaintained since 2021; if it stops reading a new runtime's assemblies, its maintained fork NetArchTest.eNhancedEdition, with the same API, is the fallback.

Not enforced:

- **Switch statements.** CS8509 covers switch expressions only, and IDE0010 also flags complete switches. Review covers them.
- **A banned type used only in a signature,** such as a `CancellationToken` parameter or a `Task` return. RS0030 doesn't see it; review does.
- **Checks with nothing to check yet:** the inputs-unchanged test waits for the first technique that takes an input. Value noise, the first technique, takes none; its determinism property is in place (`functional-core.md`, Enforcement). The required Roslyn and PNG dependencies, and Engine's imaging-namespace rule, wait for those packages.

Never leave a check disabled without recording it.

## Exceptions in force

| Doc rule | Exception | Approved in | Ends with |
| --- | --- | --- | --- |
| `functional-core R3` | A technique takes a `System.Random` constructed by the engine, instead of an RNG value it returns. | Owner decision, project-context interview, 2026-11-14 | Standing; not time-boxed |
| `functional-core R4` | A technique is an iterator that mutates its own working buffer between yields, instead of a pure step function over immutable state. | Owner decision, project-context interview, 2026-11-14 | Standing; not time-boxed |
