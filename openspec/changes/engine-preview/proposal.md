# Proposal

## Why

The spine can validate a pipeline and run value noise, but only a Core test enumerates it. Nothing yet runs a pipeline stage by stage, keeps the frames, or shows them. Every later technique, the transitions above all, is built and judged by eye, and goldens are approved by eye too. A viewer that runs a pipeline and replays it step by step should exist before them. Doing it now, with the one technique we have, also proves the MonoGame and Apos.Gui path (the HiDef profile, uploading pixels to a texture) before anything depends on it.

## What Changes

- **Generation runs (Engine).** A run takes a validated pipeline and a run seed. It goes through the stages in order, gives each stage its own `Random` seeded from Core's stage seed, and advances one technique step each time it is asked. Its state is one closed type: running, completed or stopped. Stopping is the engine no longer enumerating.
- **The frame timeline (Engine).** Every frame a run yields is copied onto an ordered timeline, together with its stage id and step number. Frames are handed out as read-only views. Everything is kept for the length of a run.
- **Playback (Core).** A pure value, plus functions that decide which timeline frame is shown: follow the latest frame, pause at a frame, play forward at a fixed interval, step forward or back, jump to the first or last frame. Elapsed time comes in as an argument.
- **Bulk pixel read (Core).** `PixelBuffer` gains a way to copy all its pixels out at once, so Desktop can upload a frame without going through the indexer one pixel at a time.
- **Next run seed (Core).** A pure function gives the seed after a given one, wrapping at the end of the range, for a "new seed" control.
- **The preview window (Desktop).** It replaces the scaffold's smoke window. It validates a pipeline description written in Desktop's code: two noise stages, so the timeline crosses a stage boundary. It runs the pipeline live and shows the selected frame scaled to fit, sharp-edged, with labels for the stage, the step and the seed. It has controls for playback, restarting, and moving to the next seed. An invalid description shows its findings as text.
- **Scaffold markers.** `EngineMarker` is deleted once Engine has real types. Desktop keeps a real reference to both adapter assemblies until they have real types.

## Capabilities

### New Capabilities

- `generation-run`: how the engine runs a validated pipeline with a seed. It covers stage order, per-stage randomness, stepping, the completed and stopped states, and the timeline of kept frames.
- `frame-playback`: which frame of a timeline is shown, and how play, pause, stepping, jumping and following the latest frame move it.
- `preview-window`: what the desktop window shows and which controls it offers for watching and replaying a generation run.

### Modified Capabilities

None. `stage-seeding` and `value-noise` already require what the engine relies on: a seed per stage that other stages don't affect, frames valid until the next step, and runs that can be abandoned after any frame.

## Out of scope

- **Definitions.** No Roslyn, no definition API, no file watching and no hot reload. The description is written in Desktop's code until the definitions change replaces it.
- **Rerunning only the stages that changed.** Every restart reruns the whole pipeline. That arrives with reload, when descriptions start to change.
- **Ports and fakes.** Nothing in this change is a platform, so no port is added (`ports-and-adapters R3`).
- **A worker thread or async generation.** A run advances on the UI thread, a step at a time. A slow step blocks the window for its length.
- **Limits on the timeline.** Every frame is kept. There is no cap, eviction or down-sampling.
- **Editing parameters in the UI,** typing a seed, a scrubbing slider, zoom and pan, and choosing the playback speed.
- **Export, isometric geometry, terrains, transitions and Wang sets.**
- **Goldens.** None is added or changed. No technique changes.

## Impact

- **Core:**
  - new playback types and functions, under a new `Tilegen.Core.Playback` namespace;
  - `PixelBuffer.CopyTo`;
  - `RunSeed.Next`.
- **Engine:** new run, run-state and timeline types. `EngineMarker` is deleted.
- **Desktop:** `SmokeGame` is replaced by the preview game, plus the code-written description, texture upload and the controls. The packages are unchanged.
- **Tests:**
  - Core tests for playback, the bulk copy and the next seed;
  - the first real Engine tests, which replace the placeholder;
  - `Layers.cs` names a real Engine type.
  - No golden changes.
- **Docs:**
  - a new topic doc, `docs/design/timeline-and-playback.md`;
  - any architecture doc or rule file that mentions `EngineMarker` or `SmokeGame` is brought up to date.
- **Adapters:** only `ScriptingMarker` and `ExportMarker` change. They point at a real Engine type in place of `EngineMarker`.
- **No change** to CI or the package versions.
