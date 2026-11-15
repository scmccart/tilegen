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

Nothing is enforced by the build yet, because no project exists. The scaffold change must put these in place, and each doc's Enforcement section says which of its rules they cover:

- One project per layer, with project references that follow the dependency rule.
- `BannedSymbols.txt` in Core (Microsoft.CodeAnalysis.BannedApiAnalyzers), with RS0030 as an error.
- An architecture test project asserting the forbidden references, and the required ones so a rule can't pass vacuously.
- `.editorconfig` with CS8509 (non-exhaustive switch) as an error, nullable enabled, and `TreatWarningsAsErrors`.

If an analyzer or the architecture-test library lags behind .NET 11, use a reflection check over `Assembly.GetReferencedAssemblies()` and say so here. Never leave a check disabled without recording it.

## Exceptions in force

| Doc rule | Exception | Approved in | Ends with |
| --- | --- | --- | --- |
| `functional-core R3` | A technique takes a `System.Random` constructed by the engine, instead of an RNG value it returns. | Owner decision, project-context interview, 2026-11-14 | Standing; not time-boxed |
| `functional-core R4` | A technique is an iterator that mutates its own working buffer between yields, instead of a pure step function over immutable state. | Owner decision, project-context interview, 2026-11-14 | Standing; not time-boxed |
