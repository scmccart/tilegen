---
paths:
  - "src/Tilegen.Engine/**"
---
# Engine: ports and orchestration, with no decisions of its own

You are editing `Tilegen.Engine`. Read `docs/architecture/functional-core.md`, `docs/architecture/ports-and-adapters.md` and `docs/architecture/railway.md` before non-trivial changes.

- Gather, call Core, enact. Don't branch except to dispatch on what Core returned or on what the user asked for (`functional-core R2`). A new `if` that changes what is generated belongs in Core.
- Run only a validated pipeline, never a raw description (`illegal-states R3`).
- Construct one `Random` per stage from the seed Core derives (`functional-core R9`).
- A yielded frame is valid only until the next step: copy what the timeline keeps (`functional-core R4`).
- Ports use only Core and Engine types, and exist only for platform things (`ports-and-adapters R2`, `R3`).
- Script integration stays one port returning a description. Don't build a plugin system (`ports-and-adapters R9`).
- Port outcomes are closed types, switched over with no discard arm (`railway R2`).
- Every port has a hand-written fake that tests use (`ports-and-adapters R7`).

Engine references Core only. If the code needs Roslyn, MonoGame, Apos.Gui or an imaging package, it belongs in an adapter or in Desktop, behind a port.

Enforced by: the architecture tests and the declared-reference test (`tests/Tilegen.ArchitectureTests`). Engine must depend on Core and on no adapter, Desktop, MonoGame, Apos or Roslyn namespace, may reference only Core, and may declare no package.
