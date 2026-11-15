# Functional core, imperative shell

`Tilegen.Core` holds every decision and is pure: values go in, values come out. The shell (`Tilegen.Engine`, the adapters and `Tilegen.Desktop`) does every effect and decides as little as it can. This is the spine of the architecture; the other docs build on it. Code review enforces the **Rules** section; reviewers cite rules by number (`functional-core R3`).

## Why

tilegen is a testbench for procedural techniques, so almost all of its value is logic: the techniques, how stages connect, isometric geometry, how transitions become Wang sets. That logic has to be testable without a window, reproducible from a seed so goldens mean something, and replayable step by step. All three fail as soon as a technique reads a clock, a global RNG or a `Texture2D`.

## The idea

- **Core decides.** A technique, a validation, a layout or a draw order is a Core function of its arguments.
- **The shell gathers, calls and enacts.** The engine constructs what a technique needs, enumerates it, and hands the results to adapters and the UI.
- **Two standing exceptions, both the owner's decision.** A technique is an iterator, not a pure step function, and it takes a `System.Random` from the engine. R3 and R4 state exactly what is allowed.

Illustrative code; the names are not final.

```csharp
// Core: a technique. Everything it needs is a parameter, and its only randomness is the Random it was given.
public static IEnumerable<Frame> Run(ErodeParameters parameters, PixelBuffer input, Random random)
{
    var work = input.Copy();                 // created here and mutated only here; the input is never touched
    for (var i = 0; i < parameters.Iterations.Count; i++)
    {
        ErodeOnce(work, random);
        yield return work.AsFrame();         // valid until the next step; the engine copies what it keeps
    }
}

// Engine: gather, run, enact.
var random = new Random(StageSeed.Derive(runSeed, stage.Id).Value);   // Core derives the seed; the engine constructs
foreach (var frame in Erode.Run(parameters, input, random))
{
    timeline.Record(stage.Id, frame);
    if (stopRequested) break;                // stopping a run is the engine no longer enumerating (R10)
}
```

## How it applies here

| Concern | Where | Notes |
| --- | --- | --- |
| Techniques | Core | Iterators over pixel buffers (R4) |
| Pipeline description, validation | Core | Pure data and a pure function (`illegal-states R2`, `R3`) |
| Which stages rerun after a reload | Core | A pure comparison of two validated pipelines |
| Isometric geometry, atlas layout, Wang sets, the tileset model | Core | Values only |
| Draw order of the preview map, which frames a timeline keeps | Core | Returned as data; the shell applies it |
| Stepping, the timeline's storage, reloading, the playback clock | Engine | No decisions of its own |
| Compiling definitions, encoding PNG, writing files | Adapters | See `ports-and-adapters.md` |
| Drawing, input, the UI | Desktop | Presents what Core and Engine return |

## Rules

- **R1.** Core MUST NOT do IO, read the clock, read the environment, or touch static mutable state. These are hidden inputs and outputs, and they end determinism and window-free tests.
- **R2.** Every decision MUST be a Core function, and the Engine and Desktop MUST NOT branch except to dispatch on what Core returned or on what the user asked for. A decision is anything that changes generated pixels, what is exported, what is valid, or in what order things run or draw. Which panel is open is presentation, not a decision.
- **R3.** Time MUST be an argument, and randomness MUST come only from an argument.
  - Exception (owner decision, 2026-11-14: iterators are worth more here than a value-typed RNG, and tilegen should not implement its own generator): a technique takes a `System.Random` that the engine constructed, and draws from it. Standing; not time-boxed.
  - Core MUST NOT construct a `Random` or use `Random.Shared`. Only the engine constructs one, from a seed Core derived (R9).
- **R4.** A technique MUST be an iterator that yields a frame per step, so the app can replay generation inside a stage.
  - Exception (owner decision, 2026-11-14): this replaces the pure step function over immutable state. Standing; not time-boxed.
  - A technique owns its working buffer and MAY mutate it between yields. It MUST NOT mutate an input, which is another stage's output.
  - A yielded frame is valid only until the next step. The engine copies what it keeps; a technique never holds on to a frame it yielded and assumes it unchanged.
  - A technique MUST NOT do a long computation outside its steps. If the work can't be watched, it isn't stepwise.
  - A technique MUST be safe to abandon after any yield: it holds nothing that needs disposal.
- **R5.** Core MUST return results as data and never enact them. It returns pixels, a tileset model, a draw list or a list of stages to rerun; the shell writes, draws and runs.
- **R6.** Values cross the boundary, not interfaces. A Core function MUST NOT take an interface or a callback that reaches the outside. The `Random` of R3 is the one handle allowed in.
- **R7.** Core's inputs and outputs MUST be free of infrastructure types: no MonoGame, Apos, Roslyn or `System.IO` types. Core has its own colour, point and rectangle values; adapters and Desktop convert.
- **R8.** Generation MUST be deterministic: the same validated pipeline and run seed give the same pixels on the same .NET version and platform. Output MUST NOT depend on the enumeration order of an unordered collection, on hash codes, on thread scheduling, on the current culture, or on time. The promise does not extend across runtimes or platforms.
- **R9.** Each stage MUST get its own `Random`, seeded from the run seed and the stage's identity by a pure Core function. With one shared stream, editing an upstream stage shifts every stage after it, which breaks rerunning only what changed. The derivation MUST NOT use `HashCode.Combine` or `string.GetHashCode`: both are randomized per process, so the seed would differ on every launch.
- **R10.** Stopping a run is the engine no longer enumerating. No `CancellationToken` or deadline reaches Core.
- **R11.** Core types SHOULD be immutable values (records, readonly structs). The pixel buffer is the one mutable type by design; any other mutable class in Core needs a comment that justifies it.
- **R12.** A Core function MAY mutate data it created that does not escape: scratch spans, builders, pooled arrays returned before exit. Apart from a technique's working buffer (R4), it MUST NOT mutate an argument, a captured variable or a field.
- **R13.** Expected failures leave Core as values (`railway.md`). Exceptions are for bugs, on either side.
- **R14.** The boundary MUST be enforced by structure, not discipline: Core is its own project referencing the base library only, with a banned-API list.

## Anti-patterns

- **A technique that computes everything, then yields once.** Instead: yield after each iteration, pass, cell or sweep.
- **`Random.Shared`, `new Random()`, `DateTime.Now` or `Stopwatch` in Core.** Instead: take the stage's `Random` as a parameter; take time as a value.
- **One `Random` threaded through every stage.** Instead: a seed per stage from Core (R9).
- **Mutating the input buffer "to save a copy".** Instead: copy into the working buffer. The input is another stage's output and may be on the timeline.
- **An `if` in the engine or UI that changes what is generated.** Instead: a Core function returns the decision and the shell applies it.
- **A Core function taking `Texture2D`, `Color` from MonoGame, or a `Stream`.** Instead: Core's own value types, converted outside.
- **Iterating a `HashSet` or `Dictionary` to decide pixel output.** Instead: an ordered collection, or sort first (R8).
- **Testing a technique through the engine or the app.** Instead: enumerate it directly in a Core test.

## Enforcement

- **Project references:** `Tilegen.Core` has no project references and no runtime package references. Analyzer-only packages, such as the banned-API analyzer below, are allowed. Covers R7 and R14. Checked by the declared-reference test in `tests/Tilegen.ArchitectureTests`, which allows Core no project reference and only the banned-API analyzer as a private package.
- **`src/Tilegen.Core/BannedSymbols.txt`,** with RS0030 as an error in `.editorconfig`. In place. Covers R3 and R10, and the clock, files and directories of R1: both `System.Random` constructors and `Random.Shared`, `DateTime.Now`/`UtcNow`, `DateTimeOffset.Now`/`UtcNow`, `Environment.TickCount`/`TickCount64`, `System.Diagnostics.Stopwatch`, `System.IO.File`, `System.IO.Directory`, `System.Threading.Tasks.Task`, `System.Threading.CancellationToken`. Each entry's message names its rule.
  - RS0030 does not see a banned type that appears only in a signature, such as a `CancellationToken` parameter or a `Task` return. Using a member or constructor is caught; the signature alone is not. That part of R1 and R10 stays with review.
  - A misspelt entry bans nothing and gives no diagnostic. Whoever edits the list checks that each entry produces an RS0030 error.
- **Core's implicit usings:** the `System.IO` and `System.Threading.Tasks` global usings are removed from Core, so `File`, `Directory` and `Task` are not in scope by default. A convenience, not a check.
- **Architecture tests** in `tests/Tilegen.ArchitectureTests` (NetArchTest.Rules): Core depends on no `Tilegen.Engine`, `Tilegen.Scripting`, `Tilegen.Export` or `Tilegen.Desktop` namespace, and on no `Microsoft.Xna.Framework`, `MonoGame`, `Apos`, `Microsoft.CodeAnalysis` or `System.IO` namespace. Covers R7, and R6 in part: no outside type can reach Core, but an interface or callback that Core declares itself is review.
- **Tests:** a determinism property (two runs, same seed, same pixels) and an inputs-unchanged check per technique will cover R4 and R8 in part (`testing.md`). There is nothing to check yet, because there are no techniques; they arrive with the first technique.
- **Review alone:** R2, R5, R6 in part, R9, R11 to R13, the stepwise parts of R4, `CancellationToken` or `Task` in a signature (R1, R10), and the rest of R1: environment variables, the console and static mutable state.

## PR checklist

- [ ] New decision logic is in Core; Engine and Desktop changes only gather, call, enact and dispatch.
- [ ] Nothing in Core reads the clock, the environment, files or statics, or constructs a `Random`.
- [ ] Each new technique is an iterator, yields per step, leaves its inputs unchanged, and can be abandoned after any yield.
- [ ] Output doesn't depend on unordered enumeration, hash codes, threads, culture or time.
- [ ] Core signatures contain no interfaces, callbacks or infrastructure types.
- [ ] The banned-API list and the architecture tests still pass and haven't been loosened.

## References

- Architecture plugin skill: `functional-core-imperative-shell` (its R3 and R4 are the rules the two exceptions depart from).
- `docs/architecture/README.md`, "Exceptions in force".
