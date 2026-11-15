# Proposal

## Why

The scaffold has layers and checks but no domain. Every later piece needs the same small spine before it can exist: a technique to run, a description to validate, a seed per stage, and a way to prove pixels against an approved image. Building it with one real technique keeps the spine honest. The types are shaped by a technique that actually yields frames and is proven by a golden, not by guesswork.

## What Changes

- **Pixel buffer and frames (Core).** A mutable RGBA8 `PixelBuffer` built on Core's own `Rgba32`, plus a read-only frame view that a technique yields once per step.
- **Pipeline description (Core).** The pure-data shape a definition will return: a list of stage descriptions holding plain values. It has one case so far, noise.
- **Validation (Core).** One function turns a description into a validated pipeline of typed stages, or into every finding at once: an empty pipeline, empty or duplicate stage ids, parameters out of range, and parameter combinations that make no sense. Only a validated pipeline can be run.
- **Per-stage seeds (Core).** A pure, stable function derives each stage's seed from the run seed and the stage id. A stage's seed doesn't change when another stage is added, removed or edited, nor between launches.
- **Value noise, the first technique (Core).** Value noise summed over octaves (fractal Brownian motion), written as grey. It yields one frame per octave, and each frame is a complete image of the octaves summed so far.
- **Golden comparer (test kit).** It loads a PNG golden and its provenance, compares decoded pixels exactly, and on a mismatch or a missing golden writes the actual image out for inspection. It never records or re-records a golden by itself.
- **The first golden.** One value-noise golden, approved by the owner by eye.
- **Property tests.** A determinism property and parameter properties, written with CsCheck. This is the first property test, so CsCheck is pinned here (`testing.md`).
- **Packages.** StbImageSharp (decode) and StbImageWriteSharp (encode) are added to the test kit. CsCheck is added to `Tilegen.Core.Tests`. The architecture tests' allowlist and the docs that describe it are updated in the same change.

## Capabilities

### New Capabilities

- `pipeline-validation`: turning a pipeline description into a runnable, validated pipeline, or into the complete list of what is wrong with it.
- `stage-seeding`: how each stage's random seed is derived from the run seed and the stage's identity, and what that seed must stay independent of.
- `value-noise`: the value-noise technique. It covers its parameters, its step-by-step frames, its output, and its determinism.

### Modified Capabilities

None. No specs exist yet.

## Out of scope

- **The engine.** That means running a pipeline, the frame timeline, rerunning only changed stages, and the run's completed or stopped outcome. Techniques are run directly by Core tests in this change.
- **Definitions.** No Roslyn, no definition API, no file watching or hot reload. Descriptions are built in tests.
- **Stages with inputs,** and the cross-stage checks that need them: missing inputs, cycles, size compatibility. Noise has no input. The validation's shape allows those findings later, but none are added now.
- **Export.** No PNG writing in `Tilegen.Export`, no atlas and no Tiled tileset. Export adopts the PNG packages in its own change.
- **Desktop.** No preview, no playback and no UI change.
- **Isometric geometry, terrains, transitions and Wang sets.**
- **Seamlessly tiling noise.** The noise doesn't wrap at its edges. A later technique or option can add wrapping when tiles need it.
- **Float or multi-channel fields.** The one buffer type is RGBA8 (owner decision, 2026-11-14).

## Impact

- **Core:** new types and functions for buffers, frames, the description, validation, seeds and value noise. The `CoreMarker` placeholder is removed once real types exist.
- **Test kit:** the golden comparer and its provenance reader; StbImageSharp 2.30.16 and StbImageWriteSharp 1.16.7.
- **Core tests:** value tests, property tests (CsCheck 5.0.1) and the golden test; the placeholder test is replaced. Goldens live beside the Core tests.
- **Architecture tests:** the test kit's allowlist row gains the two PNG packages. Test projects other than the test kit have no row, so CsCheck needs no allowlist change. `Layers.cs` names a real Core type in place of `CoreMarker`.
- **Docs:**
  - new topic docs `docs/design/value-noise.md` and `docs/design/stage-seeds.md`;
  - `testing.md` and `ports-and-adapters.md`, where they describe the golden comparer, CsCheck and the test kit's packages;
  - `.claude/rules/tests.md`.
- **No change** to Engine, the adapters, Desktop or CI.
