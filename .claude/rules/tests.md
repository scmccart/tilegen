---
paths:
  - "tests/**"
---
# Tests: prove a rule, in the right layer, with nothing shared

You are editing tests. Read `docs/architecture/testing.md` before adding a test of a new kind or touching a golden.

- Never record or re-record a golden without the owner's approval, and never to make a test pass. On a failing golden, stop and show the before and after images (`testing R6`).
- Compare goldens as decoded pixels, never as PNG bytes, and keep each golden's provenance beside it (`testing R4`, `R5`).
- No third-party art or tilesets as fixtures (`testing R8`).
- Test Core by calling it with values: no fakes, no engine, no app (`testing R1`).
- No sleeps, wall clock, network or shared statics. Use a unique temporary directory per test and poll for a condition (`testing R12`).
- Fix a flake at its cause. No retries or skips (`testing R13`).
- Assert an expected failure as its outcome case, not as a throw (`testing R15`).
- Prove export by parsing what was written, not by a text snapshot alone (`testing R10`).

There are no end-to-end or GUI tests. Don't add a harness that drives the window.

Enforced by: the banned-API list for tests, `tests/BannedSymbols.txt`, with RS0030 as an error (`testing R12` in part). The golden comparer is not built yet and arrives with the first technique change. CI runs every test project (`.github/workflows/build.yml`; see `docs/architecture/README.md`, "Enforcement status").
