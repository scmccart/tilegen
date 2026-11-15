---
paths:
  - "src/Tilegen.Core/**"
---
# Core: the pure layer that holds every decision

You are editing `Tilegen.Core`. Read `docs/architecture/functional-core.md`, `docs/architecture/illegal-states.md` and `docs/architecture/railway.md` before non-trivial changes. For a technique, read its topic doc in `docs/design/` too, and update it in the same task group.

- No IO, clock, environment or static mutable state (`functional-core R1`). No `CancellationToken`: stopping a run is the engine's (`functional-core R10`).
- A technique is an iterator that yields a frame per step, owns its working buffer, and never mutates an input (`functional-core R4`).
- Randomness comes only from the `System.Random` parameter. Never construct one or use `Random.Shared` (`functional-core R3`).
- Output must not depend on unordered enumeration, hash codes, threads, culture or time (`functional-core R8`).
- A new technique is a new sealed case of the closed stage type; update every switch and add no discard arm (`illegal-states R1`, `R5`).
- A pipeline description is pure data: no delegates, no types from outside Core (`illegal-states R2`).
- IDs and quantities are typed; tile, cell and pixel coordinates don't mix (`illegal-states R6`, `R7`).
- Expected failures are closed outcome types, and validation reports every finding in one pass (`railway R1`, `R6`).

Core references the .NET base library only. If the code needs MonoGame, Roslyn, a file or an imaging package, it belongs in an adapter or in Desktop, and Core should return data for it to act on.

Enforced by: `src/Tilegen.Core/BannedSymbols.txt` with RS0030 as an error (`.editorconfig`), which bans `Random` constructors and `Random.Shared`, the clock, `File`, `Directory`, `Task` and `CancellationToken`. RS0030 does not see a banned type that appears only in a signature, so a `CancellationToken` parameter or a `Task` return is a review finding. The architecture tests forbid any dependency on another tilegen layer, MonoGame, Apos, Roslyn or `System.IO`, and the declared-reference test allows no project reference and only the banned-API analyzer as a package (`tests/Tilegen.ArchitectureTests`).
