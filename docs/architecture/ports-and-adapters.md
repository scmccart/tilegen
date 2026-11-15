# Ports and adapters

A port is a small interface in `Tilegen.Engine`, written in tilegen's own types, for something that is platform. An adapter implements it against a real technology: `Tilegen.Scripting.CSharp` against Roslyn and the filesystem, `Tilegen.Export` against a PNG encoder and the filesystem. `Tilegen.Desktop` is the MonoGame and Apos.Gui shell and the composition root. Code review enforces the **Rules** section; reviewers cite rules by number (`ports-and-adapters R4`).

This is the "ports at the IO edge only" variant. Core sees values, never ports (`functional-core R6`).

## Why

Three outside technologies would otherwise spread through the code: Roslyn, MonoGame with Apos.Gui, and the filesystem. Each is slow, hard to test against, or likely to be replaced. The script front-end is meant to be swappable for another language one day, the UI library was chosen on a short evaluation, and tests must run with no window. Keeping each technology behind one boundary makes all three cheap.

## The idea

- **Engine owns the ports.** It asks for a pipeline description and asks for a tileset to be written, in its own vocabulary.
- **Adapters translate and make no decisions.** Roslyn diagnostics become tilegen findings; a tileset model becomes bytes.
- **Desktop wires it together with plain `new`,** then only draws and gathers input.
- **Script integration is a seam, not a plugin system.** The engine consumes a description and doesn't know what language produced it.

Illustrative code; the names are not final.

```csharp
namespace Tilegen.Engine.Ports;

// The script front-end. No Roslyn type, Assembly, Stream or path-watching API appears here.
public interface IDefinitionSource
{
    ValueTask<DefinitionOutcome> LoadAsync(DefinitionLocation location, CancellationToken ct);   // closed outcome (railway.md)
    DefinitionChanges PollChanges();                                                             // a value the engine reads
}

// Export. The adapter writes exactly the model it is given.
public interface ITilesetWriter
{
    ValueTask<ExportOutcome> WriteAsync(TilesetExport export, ExportLocation location, CancellationToken ct);
}
```

## How it applies here

| Project | Role | May reference |
| --- | --- | --- |
| `Tilegen.Core` | No ports. Pure. | The .NET base library (analyzer-only packages aside) |
| `Tilegen.Engine` | Declares ports; steps techniques; timeline; reload | Core |
| `Tilegen.Scripting.CSharp` | Adapter: compile definitions with Roslyn, load them, watch files | Engine, Core, `Microsoft.CodeAnalysis.CSharp` |
| `Tilegen.Export` | Adapter: PNG encoding, the Tiled `.tsj`/`.tsx` codec, file writing | Engine, Core, one PNG encoding package |
| `Tilegen.Desktop` | Composition root; drawing; input; UI | Everything above, `MonoGame.Framework.DesktopGL`, `Apos.Gui` |
| A definition | Returns a pipeline description | Core and the base library only |

`Apos.Gui` brings Apos.Shapes, Apos.Input, Apos.Tweens and MonoGame.Extended transitively; they stay in Desktop with it.

Every package an inner layer may reference is listed in this table. Adding one is an architecture change: update the table in the same change.

## Rules

- **R1.** References MUST point inward only (the dependency rule in `README.md`), and the build or tests MUST check it.
- **R2.** A port MUST take and return only types owned by Core or Engine. No `Microsoft.CodeAnalysis` type, `Assembly`, `Stream`, `Texture2D` or `GameTime` appears in a port signature; the adapter converts.
- **R3.** Ports exist only for things that are platform: compiling and loading definitions, watching files, writing exports. There is no interface for a technique, a validation or any other logic, and none is added "for mockability".
- **R4.** Adapters MUST be dumb. An adapter translates and contains no branch on a domain concept. If it needs a decision, the decision moves into Core as a function producing data the adapter consumes.
- **R5.** Compose with plain `new` at the composition root in Desktop, in a fixed order: adapters, then engine state, then the loop wiring. No DI container.
- **R6.** Expected IO and compiler failures become values at the adapter (`railway R12`). Only bugs throw.
- **R7.** Every port MUST have a production adapter and a hand-written fake that tests use.
- **R8.** Async stays at the boundary. Only slow ports are async (compiling, writing files). Core is synchronous, and an async result reaches the engine as a value.
- **R9.** Script integration MUST stay a single port that returns a pipeline description. Keep the seam, and MUST NOT build ahead of need: no second front-end, plugin discovery or language-neutral description format until a second language is actually wanted.
- **R10.** A definition is compiled against Core and the base library only, and nothing from its assembly may be retained after loading except the description, which is made only of Core types (`illegal-states R2`). That is what lets the collectible load context unload on the next reload.
- **R11.** Desktop MUST only present and gather input. MonoGame and Apos types stay in Desktop. The UI is rebuilt each frame from Engine and Core state; Desktop keeps no copy of that state to synchronise.
- **R12.** The export adapter MUST write exactly the tileset model it was given, with culture-invariant number formatting. Layout, tile order and Wang-set content are Core's.
- **R13.** Don't add a mediator, repository, service or manager layer that duplicates what the structure already gives. Each is an indirection with no boundary behind it.

## Anti-patterns

- **`ITechnique` or `IPipelineValidator` so a test can substitute it.** Instead: call the Core function and test it directly.
- **A Roslyn `Diagnostic` passed up to the UI.** Instead: the adapter maps it to tilegen's finding type; Desktop renders that.
- **The Roslyn adapter deciding which stages to rerun, or the export adapter deciding atlas layout.** Instead: Core decides, the adapter executes.
- **Core referencing MonoGame "just for `Color` or `Point`".** Instead: Core's own values, converted in Desktop.
- **Holding a `Type`, delegate or object from a definition's assembly.** Instead: keep only the description (R10).
- **A plugin loader, front-end registry or scripting abstraction layer with one implementation.** Instead: the one port (R9).
- **View-model state in Desktop mirroring engine state.** Instead: read the state each frame (R11).

## Enforcement

Planned; the scaffold change puts these in place.

- **Project references** follow the table above. Covers R1.
- **Architecture tests,** negative and positive: Engine references no `Microsoft.Xna.Framework`, `Apos`, `Microsoft.CodeAnalysis` or imaging namespace; only Desktop references MonoGame and Apos; only Scripting.CSharp references Roslyn; Engine does reference Core; each adapter does reference Engine. Covers R1, R2 and R11 in part.
- **Review alone:** R3, R4, R5, R7, R9, R10, R12, R13.

## PR checklist

- [ ] No new reference points outward; a new project or package is in the table and in the architecture tests.
- [ ] A new interface is a port to something platform, not an abstraction over logic.
- [ ] Port signatures use only Core and Engine types.
- [ ] Adapters contain no domain branching; their expected failures come back as values.
- [ ] Every port touched has a fake, and tests use it.
- [ ] Nothing from a definition's assembly outlives the load except the description.
- [ ] Desktop holds no decision and no mirrored state.

## References

- Architecture plugin skill: `ports-and-adapters` (the default, values-in-values-out variant).
- `functional-core.md` for what Core may see; `railway.md` for outcome types at the boundary.
