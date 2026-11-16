# Roadmap

This is the plan for phase one, cut into OpenSpec changes that each fit one PR. Each change leaves the app working, and its result can be seen in the preview window or checked by a test. CLAUDE.md, Direction, defines when phase one is done and what is deferred. Nothing is planned past phase one.

## How to use it

- **Pick the next change** from the first row in the table whose dependencies are all done. Rows that don't depend on each other can go in either order.
- **The PR that proposes a change** sets its row to "in progress" and links the OpenSpec change name. **The PR that archives it** sets the row to "done" and names the archive folder.
- **The scope here is a starting point, not a spec.** The change's proposal and design decide the details. If a change turns out bigger than one PR, split it here first, and say so in the PR.
- **Open questions** in a row are settled in that change's design, not before it.
- **Edit this file** when the plan changes: rows added, merged, split or reordered. Say why in the commit.

## Done

| Change | Archive | What it gave |
| --- | --- | --- |
| Scaffold | `2026-11-14-scaffold-solution` | The five projects, the layer rules enforced by the build and the architecture tests, CI |
| Pipeline spine | `2026-11-15-pipeline-spine` | Pixel buffers and frames, value noise, pipeline descriptions and validation, stage seeds, goldens |
| Engine preview | `2026-11-15-engine-preview` | Stepwise runs onto a timeline, playback rules, the preview window with replay controls, paced live generation, the screenshot hook |
| Stage inputs | `2026-11-16-stage-inputs` | Stage handles from a pipeline builder, input findings, immutable images, the threshold technique, runs that pass each stage its sources' last images |

## Planned

| # | Change | Depends on | Status |
| --- | --- | --- | --- |
| 1 | Stage inputs | none | done (`2026-11-16-stage-inputs`) |
| 2 | Isometric tile geometry | none | in progress (openspec: isometric-tile-geometry) |
| 3 | Wang-set model | none | planned |
| 4 | Transition technique | 1, 2, 3 | planned |
| 5 | PNG atlas export | 4 | planned |
| 6 | Tiled tileset export | 5 | planned |
| 7 | C# definitions | 1 | planned |
| 8 | Hot reload | 7 | planned |

Changes 5 and 6 together, with 4, meet phase one's Tiled acceptance check. Changes 7 and 8 meet its definition and hot-reload check. 7 can start any time after 1, but the description model settles best once 4 has shown what a connected pipeline needs.

### 1. Stage inputs

- **Goal:** a stage can take another stage's output as input, so a pipeline connects techniques instead of running them side by side.
- **Scope:**
  - The pipeline description says which earlier stage feeds which input of a stage.
  - Validation reports a reference to an unknown stage, to a later stage or to the stage itself, and an input of the wrong kind, as closed findings.
  - The engine passes the referenced stage's last kept frame to the stage that reads it.
  - One small technique that reads an input proves the wiring, for example a threshold that turns noise into a two-terrain map.
  - The built-in pipeline uses it.
- **Seen by:** the preview shows noise turning into a two-colour map. Golden for the new technique. Validation tests for each new finding.
- **Open questions:** how an input is named (by stage id, or by a typed handle); whether a stage can read several inputs; whether a technique's inputs are declared in its parameters record; what "unchanged inputs" means for the inputs-unchanged property (`testing R9`), which arrives with this change.

### 2. Isometric tile geometry

- **Goal:** Core knows the isometric tile shape.
- **Scope:** the diamond's size rules (the width-to-height ratio, and even sizes), a pure mask of which pixels are inside it, and a technique that cuts one isometric tile out of a source image.
- **Seen by:** the preview shows a single diamond tile. Property tests on the mask, such as symmetry and that adjacent tiles fit without gaps or overlaps. Golden.
- **Open questions:** 2:1 diamonds or another ratio; how edge pixels are assigned so neighbouring tiles share no pixel and leave no gap.

### 3. Wang-set model

- **Goal:** a pure model of the set of transition tiles for two terrains.
- **Scope:** the Wang-set kind, the terrain at each tile's corners or edges, every combination the set claims, and the layout of the tiles on a uniform atlas grid. No pixels.
- **Seen by:** tests only, including a property that the set covers every combination it claims (`testing R9`).
- **Open questions:** corner, edge or mixed Wang set (Tiled supports all three; corner sets are the usual choice for terrain); the order of tiles in the atlas.

### 4. Transition technique

- **Goal:** generate the full transition tileset for two terrains.
- **Scope:** for each tile in the Wang set, blend two terrain textures inside the isometric diamond, using a mask built from the tile's corner or edge terrains with a noisy boundary. A pipeline produces the whole set. The algorithm comes from papers or articles, cited in its topic doc.
- **Seen by:** the preview shows the transition sheet build up tile by tile. Goldens you approve by eye. A property that adjacent tiles match along their shared edge (`testing R9`).
- **Open questions:** how a frame is shown for a whole sheet (one frame per tile, or per pass); how the terrain textures are produced (value noise per terrain, tinted); whether this change needs splitting into "the mask" and "the blend".

### 5. PNG atlas export

- **Goal:** write the generated sheet as a PNG atlas on a uniform grid.
- **Scope:** the first port (writing a file) and its adapter in `Tilegen.Export`, PNG encoding, and an export control in the window. Replaces `ExportMarker`.
- **Seen by:** the file opens in an image viewer. An adapter test writes and reads back the PNG. Engine tests use a fake port.
- **Open questions:** where files go and how they are named; whether export runs only on a completed run.

### 6. Tiled tileset export

- **Goal:** a Tiled tileset that Tiled's terrain brush paints correctly.
- **Scope:** a `.tsj` (and `.tsx` if wanted) beside the atlas, with the Wang set, implemented from Tiled's documentation and never from its source (CLAUDE.md).
- **Seen by:** **phase one's acceptance check:** open the tileset in Tiled and paint with the terrain brush. Codec tests against the documented format.
- **Open questions:** `.tsj` only, or both formats; which Tiled version's format to target.

### 7. C# definitions

- **Goal:** a pipeline described in a C# definition file, compiled with Roslyn, replacing `BuiltInPipeline`.
- **Scope:** the definition API (a definition only builds a pipeline description: pure data, no delegates, no types of its own), the compile port and its Roslyn adapter in `Tilegen.Scripting.CSharp`, and compile errors shown in the window like findings. Replaces `ScriptingMarker`. No reload yet.
- **Seen by:** the app runs a pipeline from a file. Adapter tests compile good and bad definitions.
- **Open questions:** where the definition file lives and how the app finds it; how compile errors map to text.

### 8. Hot reload

- **Goal:** save the definition, and the preview reruns with it.
- **Scope:** a file watcher, loading each compile into a collectible assembly context and unloading the old one, and rerunning on a valid change. Rerunning only the stages that changed may come here or later.
- **Seen by:** edit, save, and the preview updates. A test that an old definition assembly unloads.
- **Open questions:** debouncing saves; what the preview shows while a bad edit is on disk; whether "rerun only what changed" belongs here.

## Known follow-ups

Small items noticed along the way. Each can ride along with a change that touches the same code, or be its own small PR.

- **Restart and next-seed pacing** were checked only by screenshot. Check them by eye in the window.
- **Narrow windows:** finding lines are cut off rather than wrapped, and the frame can extend under the control column.
- **Button wiring is checked only by reading the code.** Nothing clicks the buttons, and the screenshot hook can't yet.
- **Run speed:** at 4 steps a second, a run with hundreds of steps takes minutes to watch. A speed control or a step budget comes with the first technique that needs one, probably change 4.
