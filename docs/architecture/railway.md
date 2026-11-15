# Railway-oriented programming

An expected failure is a value in the return type, not an exception, a `null` or a `bool`. It governs every operation in tilegen that can fail for a reason the user should see: loading a definition, validating a pipeline, exporting a tileset. Code review enforces the **Rules** section; reviewers cite rules by number (`railway R2`).

## Why

tilegen's main loop is "edit a definition, save, look". Most saves in the middle of an edit are wrong in some ordinary way: a compile error, a stage wired to nothing, a parameter out of range. Those are the normal case, not the exceptional one, and the UI has to show all of them without the app falling over. If they travel as exceptions or strings, a new kind of failure compiles fine and shows up as a generic message or a crash.

## The idea

- **An outcome type lists every way an operation can end,** as a C# 15 `closed` hierarchy or a `union`.
- **Callers switch over every case.** Adding a case breaks each switch at compile time.
- **Errors are data until the edge.** Only Desktop turns one into text.

Illustrative code; the names are not final.

```csharp
public closed record class DefinitionOutcome;
public sealed record class Loaded(PipelineDescription Description) : DefinitionOutcome;
public sealed record class CompileFailed(ImmutableArray<SourceFinding> Findings) : DefinitionOutcome;
public sealed record class DefinitionThrew(string ExceptionType, string Message) : DefinitionOutcome;
public sealed record class SourceMissing(DefinitionLocation Location) : DefinitionOutcome;

// Desktop: the one place an outcome becomes text. No discard arm.
string Describe(DefinitionOutcome outcome) => outcome switch
{
    Loaded l          => $"{l.Description.Stages.Length} stages",
    CompileFailed c   => $"{c.Findings.Length} compile errors",
    DefinitionThrew t => $"the definition threw {t.ExceptionType}",
    SourceMissing m   => $"{m.Location} not found",
};
```

## How it applies here

| Workflow | Outcome | Where it is produced |
| --- | --- | --- |
| Loading a definition | Loaded, compile failed, the definition threw, source missing | `Tilegen.Scripting.CSharp`, through the engine's port |
| Validating a pipeline | A validated pipeline, or every finding | Core |
| Exporting a tileset | Written, or a named failure | `Tilegen.Export`, through the engine's port |
| A generation run | Completed or stopped | Engine |

## Rules

- **R1.** An expected failure MUST be in the return type. Don't throw for it, return `null` for it, or use `bool TryX(out T)`.
- **R2.** Each outcome set MUST be closed, as a `closed` record hierarchy with sealed cases or as a `union`, and switches over it MUST be exhaustive with no discard or default arm. Where a `union`'s default value can reach a switch, add a `null` arm that throws; never `_`.
- **R3.** One error type per workflow, not one global error type. Widen a step's error into the workflow's error at the joint.
- **R4.** Error cases MUST carry data that code can branch on. `string`, `int` codes and `Exception` are not error types. A compiler message may be carried as a field of a finding, beside its location and severity.
- **R5.** Text for a person MUST be rendered only in Desktop.
- **R6.** Validation of something a person wrote MUST collect every finding in one pass. That applies to pipeline validation and to compile errors. Inside a pipeline of steps, stop at the first failure.
- **R7.** A definition that fails to compile, throws while building its description, or returns nothing is an expected failure and MUST be an outcome case. The user's code being wrong is never a tilegen exception.
- **R8.** Stopping a generation run is not an exception and not a Core outcome: the engine stops enumerating (`functional-core R10`) and reports the run as stopped. A cancelled compile or file write follows the platform convention and throws `OperationCanceledException`.
- **R9.** The only way off the railway is a match. No `if (r.IsOk) r.Value`. An unwrap-or-throw helper is allowed only in tests.
- **R10.** Don't add a combinator (`Bind`, `Map`, async variants) before a second caller needs it. Start with the outcome type and a `switch`.
- **R11.** Don't add cases for symmetry. No `Unknown`, `Other` or `Failed(string)`.
- **R12.** Adapters MUST catch the specific exceptions of their technology and return the port's outcome type. Exceptions don't cross into Engine, and outcome types don't carry native types such as `IOException` or a Roslyn `Diagnostic`.
- **R13.** When the question is "valid or not?", "no" is a successful answer: a validation returns its findings as an ordinary result.

## Anti-patterns

- **`catch (Exception)` that logs and returns `null`.** Instead: catch the specific exceptions and return a named case.
- **`_ => "something went wrong"` in the renderer.** Instead: one arm per case, so a new case fails the build.
- **`Failed(string message)`.** Instead: named cases with fields.
- **Throwing from validation at the first problem.** Instead: collect every finding (R6).
- **Letting a definition's exception unwind through the engine.** Instead: the adapter returns the "definition threw" case (R7).
- **Asserting `Throws` in a test for an expected failure.** Instead: assert the outcome case (`testing R15`).

## Enforcement

- **The compiler:** CS8509 is an error in `.editorconfig`, whatever `TreatWarningsAsErrors` says. A switch *expression* over a `closed` type, a `union` or an enum that misses a case fails the build. Covers R2 for switch expressions.
  - Switch *statements* are not checked by the compiler. IDE0010 also flags complete switch statements and wants a `default`, so it is not used. A switch statement over an outcome stays with review, so write a switch expression where you can.
  - CS8524 is silenced, because with it on a switch expression that handles every named enum member still fails and only a discard arm fixes it (`illegal-states R5`). A missing named member still fails with CS8509.
- **Tests:** each case of an outcome type is produced by at least one test (`testing.md`). There are no outcome types yet, so nothing checks this; review checks it when the first arrives.
- **Review alone:** R1, R3 to R13, and R2 for switch statements. A discard arm over an outcome type is always a finding.

## PR checklist

- [ ] Every expected failure shows in a signature; no new `throw`, `null` return or `TryX(out)` for one.
- [ ] Every outcome type is closed, and every switch over it is exhaustive with no discard arm.
- [ ] Error types are per workflow and carry data; no string-only case.
- [ ] Text is rendered only in Desktop.
- [ ] Adapters convert their technology's exceptions; nothing native leaks.
- [ ] Every case has a test that produces it.

## References

- Architecture plugin skill: `railway-oriented-programming` (its .NET 11 `closed`/`union` idioms).
- `ports-and-adapters.md` for where outcomes are produced at the boundary.
