---
paths:
  - "src/Tilegen.Desktop/**"
---
# Desktop: the MonoGame and Apos.Gui shell, and the composition root

You are editing `Tilegen.Desktop`. Read `docs/architecture/ports-and-adapters.md` and `docs/architecture/functional-core.md` before non-trivial changes.

- Present and gather input only. Draw order, the stage list and anything else that is a decision comes from Core as data (`functional-core R2`, `ports-and-adapters R11`).
- Rebuild the UI each frame from Engine and Core state. Keep no mirrored copy to synchronise (`ports-and-adapters R11`).
- MonoGame and Apos types stay here. Convert to and from Core's own colour, point and rectangle values at this edge (`functional-core R7`).
- Compose with plain `new`, in order: adapters, engine state, loop wiring. No DI container (`ports-and-adapters R5`).
- This is the only place an outcome or finding becomes text for a person, with one switch arm per case and no discard arm (`railway R2`, `R5`).
- No generation on the GPU and no custom shaders of tilegen's own without a decision first (CLAUDE.md, Conventions).
- There are no GUI tests. Say in the PR which manual checks you did (`testing R2`).

Apos.Gui needs the HiDef graphics profile. Use MonoGame's DesktopGL flavour, and avoid a Windows-only API where a portable one is free.

Enforced by: nothing yet. The scaffold change adds the architecture tests (`docs/architecture/README.md`, "Enforcement status").
