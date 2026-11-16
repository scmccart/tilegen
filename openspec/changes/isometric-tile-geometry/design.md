# Design

## Context

- **Starting point.**
  - `stage-inputs` gave stage handles, the `Input` slots, input findings, the immutable `Image`, `StageOutputs`, and the threshold. A stage can read the last image of any earlier stage.
  - Validation keeps the stage built at each position, but nothing knows a stage's output size.
  - Core has no geometry: no tile, no point and no rectangle type.
  - The preview scales a frame by the largest whole factor that fits. `FrameTexture` already makes a new texture when the frame's size changes, and nothing in `src/Tilegen.Engine` reads a frame's width or height (checked by grep while proposing). So a run whose stages differ in size should need no Engine or Desktop change. Task 4.1's screenshot confirms it.
  - See proposal.md for why tile geometry comes now and what is out of scope.
- **Rules that shape this change:**
  - `illegal-states R1` to `R3` (the stage set and one validation that checks "compatible sizes"), `R7` (tile, pixel and diamond-size units are separate types), `R8`, `R11` (derived values are computed), `R12`;
  - `functional-core R2`, `R4`, `R7` (Core has its own point values), `R8`, `R12`;
  - `railway R1`, `R2`, `R4`, `R6`, `R11`;
  - `testing R3`, `R5`, `R6`, `R8`, `R9`, `R15`.
- **Owner decisions, 2026-11-16,** settled one at a time before this design:
  1. **Ratio and size:** 2:1 diamonds only. The tile size is one number, the width, a multiple of 4 from 4 to 4096. The height is width ÷ 2.
  2. **Edge pixels:** a pixel is inside when its centre is inside the diamond. No centre ever lies on an edge, so there is no tie-break. The rows are 2, 6, 10, … wide, and the widest are width − 2.
  3. **Where the mask lives:** a pure Core value made from the tile size alone, computed rather than stored. Core also owns the isometric layout and tile coordinates.
  4. **The cut:** one source, a tile width, and the top-left of the tile's box in the source. Pixels outside the diamond are transparent black. One frame, the size of the tile's box.
  5. **Findings:** the existing out-of-range finding for the three numbers, a new not-a-multiple finding for the width, and a new tile-outside-source finding. For the last, validation works out each stage's output size.
  6. **Golden and preview:** the golden cuts the approved value-noise golden. The built-in pipeline cuts the threshold map. If the screenshot shows too little contrast against the panel, a Desktop backdrop is the follow-up, and the cut doesn't change.

## Goals / Non-Goals

**Goals:**

- A tile shape whose edge rule is exact: proven by properties to tile the plane with no gap and no overlap, with no tie-break left to choose.
- Tile sizes that can't be wrong once made, so later changes take an `IsoTileSize` and never re-check it.
- A cut that can't step outside its source in a validated pipeline.
- A stage output size that change 4 can reuse for its size checks between inputs.

**Non-Goals:**

- No tile sheets, atlas grid or Wang set (changes 3 to 5).
- No rectangle type. A tile's box is its position plus its size; a general rectangle waits for a second user.
- No Engine change and no new Desktop control.
- No combinators over outcomes (`railway R10`).

## Layer mapping

| Piece | Layer | Notes |
| --- | --- | --- |
| `IsoTileSize`, `DiamondMask`, `IsoLayout`, `TileCoordinate`, `PixelPoint` | Core | Pure values and functions in a new `Tilegen.Core.Geometry` namespace |
| `ImageSize` | Core | A width and height of at least 1, for the output-size check |
| `CutTileDescription`, `CutTile`, `CutTileParameters` | Core | Pure data, then a stage built only by validation (`illegal-states R2`, `R3`) |
| `Parameter.TileWidth`, `Left`, `Top`; `NotMultipleOf`, `TileOutsideSource` | Core | Closed findings carrying data (`railway R4`) |
| Output sizes in `Pipeline.Validate` | Core | A decision about validity (`functional-core R2`) |
| `DiamondCut.Run` and its `Generation.Run` arm | Core | The technique, an iterator (`functional-core R4`) |
| `BuiltInPipeline`, `FindingText` | Desktop | Composition, and text for a person (`railway R5`) |
| Engine, ports, adapters | None | Nothing here is platform, and the run is unchanged |

## Decisions

### D1. The tile size: one width, a multiple of 4

```csharp
// Illustrative; names are not final.
namespace Tilegen.Core.Geometry;

/// A 2:1 isometric tile's size: a width from Min to Max that is a multiple of 4. The height is derived.
public sealed record class IsoTileSize
{
    public const int Min = 4;
    public const int Max = 4096;
    public const int WidthStep = 4;

    internal IsoTileSize(int width);        // throws unless IsValid(width)
    public int Width { get; }
    public int Height => Width / 2;          // computed, never stored (illegal-states R11)

    public static bool IsInRange(int width);         // Min..Max
    public static bool IsMultipleOfStep(int width);  // width % 4 == 0
    public static IsoTileSize Create(int width);     // for constants and tests
}
```

- **Why a multiple of 4, not "even".** Tiles in the layout sit at offsets of (±W/2, H/2) from each other (D3). Those are whole pixels only when H is even, and with W = 2H that means W is a multiple of 4. A script during proposal checked widths 6, 10 and 30, which are even but not multiples of 4: their pixel-centre masks have W·H/2 + 1 pixels, and their half-tile offsets fall between pixels.
- **One parameter.** A separate height would make "not 2:1" a state that has to be reported. With the height derived, it can't be written (`illegal-states R8`, `R11`).
- **Its own type, not `PixelSize`.** `illegal-states R7` names a diamond's size as its own unit. An `IsoTileSize` can't be passed where an image's width is expected.
- **Two predicates, not one.** Validation reports a width out of range and a width that isn't a multiple of 4 as different findings (D5). The second is checked only when the first passes.
- **Alternatives rejected:**
  - Width and height with a ratio finding: it describes ratios that are deferred.
  - A half-height parameter k, with W = 4k: always valid, but a definition would write `TileK: 16` for a 64-pixel tile, and the findings would name a number nobody draws.

### D2. The diamond mask: pixel centres, computed from the size

```csharp
// Illustrative.
public sealed class DiamondMask                // a class, so no default value can hold a null size
{
    public DiamondMask(IsoTileSize size);       // throws on null; the size carries every invariant
    public IsoTileSize Size { get; }

    /// True when pixel (x, y) of the W × H box is inside: |2x + 1 − W| + 2·|2y + 1 − H| ≤ W.
    public bool Contains(int x, int y);       // throws outside the box

    /// The row's single span: First is W/2 − Width/2, Width is 4·min(y, H − 1 − y) + 2.
    public RowSpan Row(int y);                // throws outside the box
}

public readonly record struct RowSpan(int First, int Width);
```

- **The rule.** A pixel (x, y) has its centre at (x + ½, y + ½). The diamond's corners are the midpoints of the box's sides, so the centre is inside when |x + ½ − W/2| / (W/2) + |y + ½ − H/2| / (H/2) ≤ 1. Multiplied out with W = 2H, that is |2x + 1 − W| + 2·|2y + 1 − H| ≤ W, in whole numbers.
- **No ties.** |2x + 1 − W| is odd, and 2·|2y + 1 − H| is twice an odd number, so their sum is odd. W is a multiple of 4, so the sum never equals W. Every pixel centre is strictly inside or strictly outside. The same holds for every tile in the layout, because the layout offsets are whole pixels. So the pixel-centre rule gives an exact partition of the plane with no tie-break.
- **What it looks like:**
  - rows of 2, 6, 10, … pixels, centred, stepping 2 pixels a row on each side: the usual 2:1 pixel line;
  - the two middle rows are W − 2 wide, so columns 0 and W − 1 are empty. The side neighbours' tips fill them;
  - exactly W·H/2 pixels.
- **Computed, not stored.** The mask is a size and two formulas. A stored bool grid would be a second copy that could disagree with them (`illegal-states R11`). `Row` exists because the cut, and change 4's blend, work a row at a time. A property checks that `Contains` and `Row` agree.
- **A function of the size alone.** Nothing else goes in: no position, no source, no seed.
- **Out of the box throws.** Asking about a pixel outside W × H is a bug, not an outcome.
- **Alternatives rejected:**
  - A full-width diamond, the common hand-drawn one, with rows of 4, 8, …, W, W, …, 8, 4: it has 2H more pixels than the area allows (1088 rather than 1024 at 64 × 32), so neighbours overlap along their edges and "no overlap" fails by construction.
  - A half-open rule (left and top edges in, right and bottom out): it needs a tie-break that pixel centres never meet, and it isn't symmetric.
  - Sampling at pixel corners: asymmetric, and it has ties on every edge.

### D3. The isometric layout and its coordinate types

```csharp
// Illustrative.
public readonly record struct TileCoordinate(int I, int J);   // a tile's place in the isometric layout
public readonly record struct PixelPoint(int X, int Y);       // a pixel position; may be negative

public static class IsoLayout
{
    /// The top-left corner of the box of the tile at (i, j): ((i − j)·W/2, (i + j)·H/2).
    public static PixelPoint Origin(IsoTileSize size, TileCoordinate tile);
}
```

- **The layout** is the diamond layout of Tiled's "isometric" orientation: i runs down and to the right, j runs down and to the left. The topic doc cites Tiled's documentation for it. Nothing comes from Tiled's source (CLAUDE.md).
- **In Core now,** because the "no gap, no overlap" property is about how tiles meet. A layout written only in a test would prove a formula the product doesn't have. Change 4's edge-matching property needs the same neighbour relation.
- **Two point types** (`illegal-states R7`). A tile coordinate and a pixel position are both pairs of `int`s and must not be interchangeable. `PixelPoint` has no range: layout origins are negative for i < j, and the cut's position range belongs to `CutTileParameters` (D4).
- **Checked arithmetic.** `Origin` uses `checked`, so a coordinate far enough out to overflow throws rather than wrapping.

### D4. The cut-tile stage and technique

```csharp
// Illustrative.
public sealed record class CutTileDescription(
    string Id, StageHandle Source, int TileWidth, int Left, int Top) : StageDescription(Id);

public sealed record class CutTile(StageId Id, CutTileParameters Parameters) : Stage(Id);

public sealed record class CutTileParameters      // internal constructor; Create for tests
{
    public const int MinPosition = 0;
    public const int MaxPosition = 4095;
    public StageId Source { get; }
    public IsoTileSize Size { get; }
    public PixelPoint Position { get; }           // each coordinate MinPosition..MaxPosition
}

public static class DiamondCut
{
    public static readonly Rgba32 Outside = new(0, 0, 0, 0);
    public static IEnumerable<Frame> Run(Image source, CutTileParameters parameters, Random random);
}

// Generation.Run gains:  CutTile cut => DiamondCut.Run(outputs[cut.Parameters.Source], cut.Parameters, random),
```

- **One step.** Calling `Run` checks its arguments:
  - nulls;
  - a box that doesn't fit the source, which throws `ArgumentException`.

  A validated pipeline can't produce that, so it is a bug (D5). Nothing else happens until the first frame is requested. The step then:
  1. allocates a W × H working buffer;
  2. fills each row's `Row(y)` span with the source pixels at (Left + x, Top + y), alpha included;
  3. writes `Outside` everywhere else, explicitly rather than relying on the buffer's zero fill;
  4. yields the frame.
- **The output is the box,** W × H, not the source's size and not trimmed. The atlas grid and Tiled expect uniform W × H cells.
- **Transparent black outside,** (0, 0, 0, 0), one exact value, so the golden and the tests pin it. The golden codec round-trips alpha as 8-bit RGBA.
- **Alpha inside is copied.** The cut changes which pixels exist, not their colour.
- **No randomness.** It takes the `Random` so every technique has the same shape, and draws nothing. A test checks that.
- **Position is the box's top-left,** like every other coordinate in tilegen. A centre would read more naturally for a diamond, but would be the one centred coordinate in the codebase.
- **Names.** The stage is `CutTile` and the technique is `DiamondCut`, so the record and the static class don't clash, as with `Threshold` and `TerrainThreshold`.
- **Not representable after validation:**
  - a tile width that isn't 4 to 4096 or isn't a multiple of 4 (`IsoTileSize`);
  - a position outside 0 to 4095 (`CutTileParameters`);
  - a cut whose source is missing, later or itself (`stage-inputs.md`);
  - a cut whose box runs outside its source. Only validation builds a `ValidatedPipeline`, so the guarantee belongs to the pipeline, as the threshold's source guarantee does. `CutTileParameters.Create` can describe any box, for tests.

### D5. Findings, and the output size of each stage

```csharp
// Illustrative.
public enum Parameter { Width, Height, Octaves, CellSize, Persistence, Level, TileWidth, Left, Top }

/// The value is in range but isn't a multiple of Divisor.
public sealed record class NotMultipleOf(StageRef Stage, Parameter Parameter, int Value, int Divisor) : Finding;

/// The tile's box, at Left, Top and TileWidth × TileHeight, doesn't lie inside its source's SourceWidth × SourceHeight.
public sealed record class TileOutsideSource(
    StageRef Stage, int Left, int Top, int TileWidth, int TileHeight, int SourceWidth, int SourceHeight) : Finding;

/// A width and height of at least 1: the size of an image a stage outputs. A class with an
/// internal constructor that enforces it, not a positional struct, whose public constructor and
/// default value would bypass the invariant (illegal-states R8).
public sealed record class ImageSize
{
    internal ImageSize(int width, int height);   // throws unless both are at least 1
    public int Width { get; }
    public int Height { get; }
}
```

- **Order within a cut stage:**
  1. the id;
  2. the source (`CheckInput`, unchanged);
  3. the tile width: its range, then, only when it is in range, whether it is a multiple of 4;
  4. left;
  5. top;
  6. the fit check.
- **`NotMultipleOf` is general over `Parameter`.** `OutOfRange` can't express it, because its data is a range. It carries the divisor so Desktop can say "must be a multiple of 4" without knowing which parameter has the rule. It has one user today, and it isn't a case added for symmetry (`railway R11`).
- **The output-size mechanism.**
  - Beside the stage built at each position, `Pipeline.Validate` keeps that stage's output size, or nothing where no stage was built.
  - One private function gives a built stage's size by switching over every `Stage` case with no discard arm (`illegal-states R5`):
    - `Noise`: its width × height;
    - `Threshold`: the size at its source's position;
    - `CutTile`: its W × H box.
  - A new stage kind doesn't compile until it says what size it outputs.
- **The fit check runs only when** the source resolved to a built stage and the cut's own parameters added no finding. A bad source's findings are reported once, against it (`stage-inputs.md`). A bad width doesn't also give a fit finding.
- **The fit rule:** Left + W ≤ source width and Top + H ≤ source height. Left and Top are already at least 0. A tile larger than its source fails the same rule, so it needs no finding of its own.
- **This is not the deferred size check.** Change 4's check is about two inputs of one stage disagreeing. This one compares one input with the stage's own parameters. It reuses the same output sizes, which change 4 will also need.
- **Why at validation and not in the run.** A box outside its source is something a person wrote, so it is an expected failure (`railway R1`), and "compatible sizes" is validation's job (`illegal-states R3`). A technique can't return an outcome; it can only yield frames or throw.
- **Alternatives rejected:**
  - Clipping, writing transparent pixels for the part outside the source: no finding would be needed, but a typo in `Top` would give a half-empty tile with nothing to say why.
  - Storing the output size on each `Stage` record: a derived value stored (`illegal-states R11`), and the threshold's size would be copied from its source.

### D6. The built-in pipeline

```csharp
var pipeline = new PipelineBuilder();
var height = pipeline.Add(new NoiseDescription("height", Width: 128, Height: 128, Octaves: 5, CellSize: 32, Persistence: 0.5));
var terrain = pipeline.Add(new ThresholdDescription("terrain", Source: height, Level: 128));
pipeline.Add(new CutTileDescription("tile", Source: terrain, TileWidth: 128, Left: 0, Top: 32));
```

- **Seven frames:** five octaves, the map, then the tile.
- **A 128 × 64 tile** from the middle band of the map, as wide as the map. At the default 1280 × 800 window, the frame area is 960 × 800, the window minus the 320-pixel control column (`TilegenGame.FrameArea`). `Fit` takes the smaller of 960 ÷ 128 and 800 ÷ 64, so it scales the tile by 7, to 896 × 448. Each pixel is a 7-pixel block, and the 2-pixel steps of the edges are easy to see.
- **The start seed stays 1.**
- **The contrast risk.** The frame is drawn with `BlendState.NonPremultiplied` over `Theme.Panel`, so a transparent pixel shows the panel colour. `Theme.Panel` comes from Apos.Gui, and its value wasn't read while proposing. If it is dark, black terrain may sit close to it. Task 4.1's screenshot settles it. If the diamond's outline is hard to see, the implementer raises it with the owner. A backdrop behind the frame would be a Desktop-only follow-up with its own `preview-window` change, not part of this change (proposal, Out of scope). Either way the cut doesn't change.
- The preview-window spec's built-in description becomes noise, threshold, cut.

### D7. Desktop text

`FindingText` gains:
- the parameter names "tile width", "left" and "top";
- an arm for `NotMultipleOf`, for example: "Stage at index 2 ("tile"): tile width is 30, which is not a multiple of 4.";
- an arm for `TileOutsideSource`, for example: "Stage at index 2 ("tile"): the 64 × 32 tile at (0, 40) doesn't fit inside its 64 × 64 source."

Its switches have no discard arm, so the build fails until every arm exists.

### D8. Tests

- **Geometry** (`tests/Tilegen.Core.Tests/Geometry/`):
  - **Properties**, CsCheck over widths from 4 to 256 in steps of 4:
    - symmetry: `Contains(x, y)` equals `Contains(W−1−x, y)` and `Contains(x, H−1−y)`;
    - the count: exactly W·H/2 pixels are inside;
    - no gap, no overlap: tiles are placed with `IsoLayout.Origin` at every coordinate with |i|, |j| ≤ 3, and every pixel of the region covered by the origin tile's box and its eight surrounding box-sized neighbours is inside exactly one diamond. That region is well inside the placed tiles;
    - rows: each row is one contiguous run, and it agrees with `Row(y)`: width 4·min(y, H−1−y) + 2, first W/2 − width/2.
  - **Examples:**
    - the 8 × 4 mask written out literally;
    - the 16 × 8 row widths;
    - for 64 × 32: the top tip at x 31 and 32, columns 0 and 63 empty, middle rows 62 wide, and 1024 pixels;
    - `Origin` of (1, 0) and (0, 1) for 64 × 32: (32, 16) and (−32, 16);
    - `Contains` and `Row` throwing outside the box.
  - **Wrapper tests** in the style of `NoiseWrapperTests`: `IsoTileSize` accepts 4, 64 and 4096, refuses 0, 2, 30 and 4100, and gives height = width ÷ 2; `Create` throws for a bad width.
- **The cut** (`tests/Tilegen.Core.Tests/Techniques/`):
  - one test per scenario in `specs/cut-tile/spec.md` apart from the validation and golden ones;
  - `ArgumentException` at the call for a box outside the source;
  - **CsCheck properties** over generated sources up to 64 × 64 with random pixels (alpha included), tile widths that fit, and positions that fit:
    - determinism;
    - one frame of W × H;
    - inside pixels equal the source at the offset, and outside pixels equal `Outside`;
    - the input unchanged after every yielded frame (`testing R9`).
- **The golden** `tests/Tilegen.Core.Tests/Goldens/cut-tile/basic.png`:
  - **Input:** the approved `Goldens/value-noise/basic.png`, decoded into an `Image`;
  - **Cut:** tile width 64, left 0, top 16, which gives 64 × 32;
  - **Provenance:** `technique: "cut-tile"`, `parameters: { "input": "value-noise/basic.png", "tileWidth": 64, "left": 0, "top": 16 }`, `stageSeed: 20261116` (required by the format and unused), `dotnet: "11.0"`, `approvedIn: "isometric-tile-geometry"`;
  - **Why noise and not the threshold golden:** grey texture makes it easy to see that the edge pixels were copied rather than cleared, and the tile spans the source's full width, so the side columns are exercised;
  - **Approval:** the implementer runs the test and shows the owner the actual image beside its input. It is copied into `Goldens/` only after the owner approves it (`testing R6`).
- **Validation** (`tests/Tilegen.Core.Tests/Pipelines/`):
  - one test per validation scenario in `specs/cut-tile/spec.md`, each finding asserted as its case (`testing R15`);
  - the order within a cut stage;
  - a cut reading a cut;
  - `ClosedTypeTests` gains `CutTileDescription`, `CutTile`, the two findings and the three parameters;
  - `PipelineValidationProperties` generates cut stages whose boxes fit their sources, and its determinism property still holds.
- **Generation:** a `GenerationTests` case in which `Generation.Run` on a `CutTile` gives the same frame as `DiamondCut.Run` on its source's image.
- **Engine:** no new test. The run is unchanged, and a frame of a different size is already handled. The existing scenarios still pass.
- **Architecture:** no change. `Tilegen.Core.Geometry` sits under Core's root namespace, which the existing test checks.

## Risks / Trade-offs

- **[The widest rows are W − 2, which may surprise someone who expects a full-width diamond]** → It is the only symmetric rule that tiles exactly. The topic doc shows the 8 × 4 mask and says why, and an example test pins it.
- **[Tiled may place tile images differently from this layout, for example by a pixel]** → Not checked in this change. Tiled's documented placement of isometric tile images is read when change 6 writes the tileset, and its manual check in Tiled is where a seam would show. If it differs, the layout or the atlas cell gains an offset there; the mask doesn't change.
- **[The cut golden depends on the value-noise golden]** → As with the threshold golden, the input is the stored PNG, so a change to value noise doesn't move it. If the value-noise golden is ever re-recorded, both go to the owner.
- **[The output-size function is new machinery in validation]** → It is one exhaustive switch beside the existing `built` array. Change 4 needs it anyway, and the switch forces each new stage to declare its size.
- **[Black terrain against a dark panel]** → Checked by screenshot. The fix is a Desktop backdrop (D6), and the cut doesn't change.
- **[`NotMultipleOf` is general but has one user]** → Its data is what Desktop needs, and it names no tile concept. If no second user appears, it costs nothing.

## Topic docs

- **Creates `docs/design/isometric-geometry.md`:**
  - the 2:1 diamond, and why the width is a multiple of 4 (D1);
  - the pixel-centre rule in whole numbers, why there are no ties, the row widths, W − 2, the 8 × 4 picture and the W·H/2 count (D2);
  - the layout, with tile and pixel coordinates (D3);
  - sources: Tiled's documentation for the isometric orientation, checked in its text, and any article on 2:1 pixel-art tiles that is cited, also checked in its text. No code was copied;
  - decisions and limits: the rejected full-width and half-open rules, no other ratios, no height;
  - where it lives, and the properties and examples that prove it.
- **Creates `docs/design/cut-tile.md`:**
  - what it does;
  - one step and what it writes inside and outside;
  - its parameters, ranges and findings, including the fit check;
  - sources: none beyond `isometric-geometry.md`;
  - decisions: the box size, transparent black, alpha kept, top-left position, no randomness;
  - where it lives;
  - the golden and its provenance.
- **Updates `docs/design/stage-inputs.md`:**
  - validation now keeps each built stage's output size, and the fit check is the first use;
  - "No size checks between inputs" now points at the output sizes change 4 will compare;
  - "Where it lives" gains the new tests.
- **Updates `docs/design/timeline-and-playback.md`:**
  - the built-in pipeline gains the cut, so the memory example becomes 7 frames: six of 128 × 128 and one of 128 × 64, 416 KiB;
  - the run time becomes about 1.75 seconds to generate and 2.0 to complete, and "the built-in 6-frame run" becomes 7;
  - a note that frames on one timeline may differ in size.
- **Updates `docs/design/threshold.md`:** the built-in pipeline's cut now reads its output. One line, in "What it does".
- **Architecture docs** (not topic docs): the Enforcement texts that list each technique's properties. These are the Tests bullet in `functional-core.md`, "Property tests in place" in `testing.md`, and "The technique properties" in the README's "Enforcement status". Each gains the cut's determinism and inputs-unchanged properties. Task 3.5 also greps `.claude/rules/` for stale text.
