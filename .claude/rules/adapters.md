---
paths:
  - "src/Tilegen.Scripting.*/**"
  - "src/Tilegen.Export/**"
---
# Adapters: translate one outside technology, and decide nothing

You are editing an adapter: `Tilegen.Scripting.CSharp` (Roslyn, file watching) or `Tilegen.Export` (PNG, the Tiled codec, file writing). Read `docs/architecture/ports-and-adapters.md` and `docs/architecture/railway.md` before non-trivial changes.

- No branch on a domain concept. If the adapter needs a decision, Core makes it and returns data (`ports-and-adapters R4`).
- Catch the technology's specific exceptions and return the port's outcome type. Nothing native leaks: no Roslyn `Diagnostic`, no `IOException` (`railway R12`).
- A definition that fails to compile or throws is an outcome case, never a tilegen exception (`railway R7`).
- Compile a definition against Core and the base library only, and keep nothing from its assembly except the description (`ports-and-adapters R10`).
- Write exactly the tileset model given, with culture-invariant numbers. Layout and Wang-set content are Core's (`ports-and-adapters R12`).
- Implement the Tiled format from its documentation. Never copy code from Tiled's source: it is GPL.
- Adapter tests are integration tests against the real technology, in unique temporary directories, with no sleeps (`testing R11`, `R12`).

An adapter references Engine, Core and its own technology. A new package means updating the table in `docs/architecture/ports-and-adapters.md` in the same change.

Enforced by: the architecture tests and the declared-reference test (`tests/Tilegen.ArchitectureTests`). Each adapter must depend on Engine and on neither the other adapter, Desktop, MonoGame nor Apos (Export not on Roslyn either), must reference Engine and may reference Core besides, and may declare only the packages the architecture tests' allowlist names, which mirrors the `ports-and-adapters.md` table. Roslyn and the PNG package are added when they're first used.
