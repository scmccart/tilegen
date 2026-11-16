# Proposal

## Why

Today every stage generates from nothing, and a pipeline runs its stages side by side. Every later piece of phase one is a technique that transforms another stage's output: cutting a tile, building a terrain map, blending a transition. So a stage has to be able to read an earlier stage's output before any of them can exist. The description model is easiest to settle now, while it holds only two kinds of stage (`docs/roadmap.md`, change 1).

## What Changes

- **Typed stage handles (Core).** A definition builds its description with a pipeline builder. Adding a stage returns a handle to it, and a later stage names its input by passing that handle. A handle can only be had for a stage already added, so most wrong references can't be written. The description stays pure data: a handle is a Core value, not a delegate.
- **Inputs are parameters (Core).** A technique's inputs are named slots in its parameters, beside its numbers. A technique can have several slots. The one added here has one.
- **Input findings (Core).** Validation reports, as closed findings and in the same single pass as every other finding:
  - an input that names no stage;
  - an input that names no stage of this description, such as a handle from another builder;
  - an input that names a later stage;
  - an input that names the stage itself.
- **An immutable image type (Core).** The pixels the engine keeps are a new immutable `Image`. A stage's input is the `Image` the engine kept for its source, so the compiler stops a technique from writing to its input. The timeline keeps `Image`s too.
- **Threshold, the second technique (Core).** It reads one input. Each pixel at or above a brightness level becomes white, and every other pixel becomes black: a two-terrain map. It yields one frame, and its output is the size of its input. It has a golden, made from the approved value-noise golden.
- **The engine passes inputs (Engine).** The engine passes each stage the last kept frame of each stage it reads.
- **The inputs-unchanged property arrives** (`testing R9`). A Core property checks after every step that the input's pixels are unchanged, and an Engine scenario checks that a downstream stage leaves its source's timeline entries unchanged.
- **The built-in pipeline is noise, then threshold (Desktop),** so the preview shows noise turning into a two-colour map. Desktop has text for each new finding.
- **BREAKING (internal API):**
  - `StageDescription` gains `Id` on its base record.
  - `TimelineEntry` holds an `Image` instead of a `Frame`.
  - `Generation.Run` takes the kept outputs of earlier stages.

  No definition files exist yet, so nothing outside the repo breaks.

## Capabilities

### New Capabilities

- `threshold`: the threshold technique. It covers its parameters, the rule that turns brightness into two terrains, its single frame and its output size, and that it leaves its input unchanged.

### Modified Capabilities

- `pipeline-validation`: a description has a second stage kind and holds handles to earlier stages. New requirements cover how handles are issued and the four input findings.
- `generation-run`: a stage with inputs receives its sources' last kept frames. "A stage's frames equal its technique run alone" now includes those inputs. A downstream stage leaves its sources' timeline entries unchanged.
- `preview-window`: the built-in description is a noise stage followed by a threshold stage that reads it.

## Out of scope

- **An input's kind.** Every output is the same RGBA8 buffer, and no output carries a semantic kind, so no input can be of the wrong kind. Any stage can feed any slot. The roadmap's wrong-kind finding moves to change 4, the first technique whose inputs differ in meaning (owner decision, 2026-11-15).
- **Size compatibility between several inputs.** Threshold has one input and takes its size from it, so nothing can mismatch. The check arrives with the first technique that has two inputs (change 4).
- **The definition API proper:** Roslyn, a definition's entry point, file watching and reload. The builder here is the Core value a definition will use. Changes 7 and 8 load it.
- **Rerunning only the stages that changed.** A run still runs every stage. Handles and stage seeds keep that possible later.
- **Cycles.** An input can only name an earlier stage, so a cycle is impossible and gets no finding of its own.
- **Colour.** The threshold writes black and white. Terrain colours and textures come with change 4.
- **Wang sets, isometric geometry and export.**

## Impact

- **Core:**
  - `PipelineBuilder` and `StageHandle`;
  - `Id` moves to the base `StageDescription`;
  - `ThresholdDescription`, the `Threshold` stage, `ThresholdParameters` and `ThresholdLevel`;
  - the `Input` enum and four findings, and `Parameter.Level`;
  - `Image`, with `Frame.ToImage()`;
  - `StageOutputs`, and the new `Generation.Run` signature;
  - the technique `TerrainThreshold.Run`.
  - `ImmutableCollectionsMarshal` joins Core's banned list.
- **Engine:** the run keeps each finished stage's last image and passes the kept outputs to `Generation.Run`. `TimelineEntry` holds an `Image`.
- **Desktop:**
  - `BuiltInPipeline` uses the builder and a threshold stage;
  - `FindingText` covers the new findings and the `Level` parameter;
  - `FrameTexture` uploads from an `Image`.
- **Tests:**
  - Core validation tests for each new finding;
  - threshold tests, properties and its golden;
  - Engine scenarios for passing inputs and leaving sources unchanged;
  - updated tests wherever `TimelineEntry.Frame` or `Generation.Run` is used.
- **Docs:**
  - new topic docs `docs/design/threshold.md` and `docs/design/stage-inputs.md`;
  - updates to `timeline-and-playback.md` and `value-noise.md`;
  - the Enforcement sections of `functional-core.md` and `testing.md`, and the README's "Enforcement status";
  - row 1 of `docs/roadmap.md`.
- **No change** to the adapters, CI or packages.
