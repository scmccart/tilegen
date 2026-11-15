# Make illegal states unrepresentable

Types in `Tilegen.Core` are modelled so that a value which can be built is a value the domain allows. It governs Core's model above all: stages, the pipeline description, coordinates and sizes, terrains and Wang sets, and technique parameters. Code review enforces the **Rules** section; reviewers cite rules by number (`illegal-states R3`).

## Why

Two of tilegen's rules are only real if the types carry them. "A definition can't add a technique" holds because the set of stages is closed. "The engine never runs a broken pipeline" holds because it accepts only a type that validation produces. Without that, each becomes a check someone has to remember. Isometric work also mixes tile, cell and pixel coordinates constantly, and a bare `int` hides which one it is.

## The idea

- **Closed types:** one case, one shape. C# 15 `closed` hierarchies and `union` types, checked by the compiler.
- **Typed wrappers:** one value, one meaning.
- **Parse at the boundary:** validate once, into a more precise type, then trust it.

Illustrative code; the names are not final.

```csharp
// The set of stages is closed: a definition can construct these cases and cannot add one.
public closed record class Stage;
public sealed record class Noise(StageId Id, NoiseParameters Parameters) : Stage;
public sealed record class Threshold(StageId Id, StageId Input, Level Level) : Stage;
public sealed record class Erode(StageId Id, StageId Input, ErodeParameters Parameters) : Stage;

// A description is what a definition returns. A validated pipeline is the only thing the engine runs.
public sealed record class PipelineDescription(ImmutableArray<Stage> Stages, /* … */);
public sealed class ValidatedPipeline { /* constructed only by Pipeline.Validate */ }

// Units that can't be mixed.
public readonly record struct TileCoord(int X, int Y);
public readonly record struct PixelCoord(int X, int Y);
```

## How it applies here

| Domain fact | Carried by |
| --- | --- |
| Techniques live only in the app | `Stage` is a closed hierarchy in Core (R1) |
| A definition only configures | `PipelineDescription` is pure data (R2) |
| The engine never runs an invalid pipeline | Only `ValidatedPipeline` is accepted (R3) |
| Tile, cell and pixel coordinates differ | Separate types with explicit conversions (R7) |
| Parameters have ranges | Types whose only constructors check (R8) |
| A Wang set covers every combination it claims | Checked once when the tileset model is built (R3, R8) |

## Rules

- **R1.** The set of stages MUST be one closed hierarchy in Core, with sealed cases. A definition can construct stages and cannot define one. Adding a technique means adding a case, and the compiler then lists every switch to update.
- **R2.** A pipeline description MUST be pure data: records made only of Core types. It holds no delegate, no lambda, and no instance of a type declared outside Core.
- **R3.** The engine MUST accept only a validated pipeline. One Core function turns a description into a validated pipeline or into findings, checking the cross-stage invariants once: inputs that exist, no cycles, compatible sizes. It reports every finding in one pass (`railway R6`).
- **R4.** Mutually exclusive states MUST be one closed type, not a set of flags or optional fields whose meaning depends on each other. Use an enum when the cases carry no data.
- **R5.** A switch over a closed type MUST be exhaustive with no discard arm (`railway R2`).
- **R6.** Identifiers MUST be typed wrappers: a stage id, a terrain id and a seed are not interchangeable `int`s or `string`s.
- **R7.** Quantities MUST carry their unit. Tile coordinates, cell coordinates in the atlas and pixel coordinates are different types, as are a cell's size and its diamond's size. Conversions are explicit methods.
- **R8.** A value with an invariant MUST be a type whose only constructors enforce it. Offer a checking factory that returns an outcome where the value comes from a definition or a file, and a throwing one for constants, where failure is a bug.
- **R9.** Parse, don't validate. A check that returns `bool`, or the type it was given, throws the knowledge away; return the more precise type.
- **R10.** Absence that means something MUST be a case, not `null` and not a sentinel such as `-1` or an empty string. `T?` is fine for a lookup that can miss; turn it into a case or an outcome straight away.
- **R11.** Derived values MUST be computed, not stored: an atlas's pixel size comes from its grid and cell size.
- **R12.** Every wrapper MUST carry an invariant or a meaning. A wrapper that only renames a type is noise.
- **R13.** Validated types MUST be immutable. A mutable value validated at construction can be made invalid afterwards. The pixel buffer is the one mutable Core type (`functional-core R11`) and carries no validated invariant beyond its size.
- **R14.** Tiled's file shapes MUST exist only in the export adapter's codec. In memory the tileset is Core's typed model.

## Anti-patterns

- **`bool IsTransition, bool IsCorner, bool IsEdge` on a tile.** Instead: one closed type for the kind of tile.
- **`int` for tile, cell and pixel positions alike.** Instead: three types.
- **A stage parameter of type `Func<…>` or `object`.** Instead: a new case or a typed parameter in Core (R2).
- **`Validate(description)` returning `bool`, with the engine taking the description.** Instead: return a `ValidatedPipeline` and take only that.
- **`-1` for "no input" or `""` for "no terrain".** Instead: a case.
- **Range checks repeated in each technique.** Instead: a parameter type that checks once.
- **A wrapper with a public constructor and no check, around a value that has a rule.** Instead: a private constructor and a factory.
- **A record derived from a record where the two must compare equal.** Equality becomes one-directional.

## Enforcement

- **The compiler:** CS8509 is an error in `.editorconfig`, whatever `TreatWarningsAsErrors` says. A switch *expression* that misses a case of a `closed` type, a `union` or a named enum member fails the build. Covers R1 and R5 for switch expressions.
  - Switch *statements* are not checked by the compiler. IDE0010 can't fill the gap: it also flags complete switch statements and wants a `default`. A switch statement over a closed type or union stays with review, so write a switch expression where you can.
  - CS8524 is silenced (owner decision, 2026-11-14). With it on, a switch expression that handles every named enum member still fails, and only a discard arm fixes it, which R5 forbids. With it off, a missing named member still fails with CS8509. An out-of-range enum value throws at run time, which is a bug.
- **Constructors:** hidden constructors and factories make R3 and R8 structural.
- **Structure:** R14 in part. Core and Engine may not depend on `Tilegen.Export` at all: the dependency rule forbids it, and the architecture tests in `tests/Tilegen.ArchitectureTests` check it. So the codec's types can't reach either. That Tiled's shapes aren't redefined elsewhere is review.
- **Review alone:** R2 (until an analyzer or test checks description types), R4, R6, R7, R9 to R13, R14 in part, and R5 for switch statements.

## PR checklist

- [ ] A new technique is a new sealed case of the closed stage type, with every switch updated and no discard arm added.
- [ ] Descriptions contain no delegates and no types from outside Core.
- [ ] The engine's entry points take a validated pipeline, never a raw description.
- [ ] No new flags or optional fields for exclusive states; no new sentinels.
- [ ] IDs and quantities are typed; units can't be mixed.
- [ ] Every type with an invariant has a hidden constructor and a checking factory, with tests at its edges.

## References

- Architecture plugin skill: `illegal-states-unrepresentable`.
- C# 15 closed hierarchies and union types (Microsoft Learn, "What's new in C# 15").
