using System.Collections.Immutable;
using Tilegen.Core.Geometry;
using Tilegen.Core.Imaging;
using Tilegen.Core.Techniques;

namespace Tilegen.Core.Pipelines;

/// <summary>Turns a description into a pipeline the engine may run, or into every finding against it.</summary>
public static class Pipeline
{
    private static readonly ParameterRange PixelSizeRange = new(PixelSize.Min, MinInclusive: true, PixelSize.Max);
    private static readonly ParameterRange OctavesRange = new(Octaves.Min, MinInclusive: true, Octaves.Max);
    private static readonly ParameterRange CellSizeRange = new(CellSize.Min, MinInclusive: true, CellSize.Max);
    private static readonly ParameterRange PersistenceRange =
        new(Persistence.ExclusiveMin, MinInclusive: false, Persistence.Max);
    private static readonly ParameterRange LevelRange =
        new(ThresholdLevel.Min, MinInclusive: true, ThresholdLevel.Max);
    private static readonly ParameterRange TileWidthRange = new(IsoTileSize.Min, MinInclusive: true, IsoTileSize.Max);
    private static readonly ParameterRange PositionRange =
        new(CutTileParameters.MinPosition, MinInclusive: true, CutTileParameters.MaxPosition);

    /// <summary>
    /// Checks the whole description in one pass (railway R6). Stages are checked in description
    /// order; within a stage the id comes first, then each parameter in declaration order, inputs
    /// included, then the cross-field rule. A cut-tile stage's last check is whether its tile fits
    /// inside its source's output, which needs the output size of each stage built so far. A
    /// description with no findings gives a <see cref="Valid"/> pipeline.
    /// </summary>
    public static ValidationOutcome Validate(PipelineDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);

        if (description.Stages.IsDefaultOrEmpty)
        {
            return new Invalid([new EmptyPipeline()]);
        }

        var findings = ImmutableArray.CreateBuilder<Finding>();
        var stages = ImmutableArray.CreateBuilder<Stage>(description.Stages.Length);
        var firstUse = new Dictionary<string, int>(StringComparer.Ordinal);

        // The stage built at each position, or null where the stage added a finding or its source
        // was not built. An input names only an earlier position, which is already filled in.
        var built = new Stage?[description.Stages.Length];

        // Beside it, the size of the image the stage built at each position outputs, or null where
        // none was built. A built stage names its source by id, so the position of each built stage is
        // kept by id too. Built ids are unique, because a repeated id is never built.
        var sizes = new ImageSize?[description.Stages.Length];
        var positions = new Dictionary<StageId, int>();

        for (var index = 0; index < description.Stages.Length; index++)
        {
            var described = description.Stages[index];

            // The id is on the base record, so the stage is named the same way whatever its technique.
            var at = new StageRef(index, described.Id ?? "");

            Stage? stage = described switch
            {
                NoiseDescription noise => ValidateNoise(at, noise, firstUse, findings),
                ThresholdDescription threshold =>
                    ValidateThreshold(at, threshold, description, built, firstUse, findings),
                CutTileDescription cut => ValidateCutTile(at, cut, description, built, sizes, firstUse, findings),
            };

            built[index] = stage;
            if (stage is not null)
            {
                sizes[index] = OutputSize(stage, sizes, positions);
                positions.Add(stage.Id, index);
                stages.Add(stage);
            }
        }

        return findings.Count == 0
            ? new Valid(new ValidatedPipeline(stages.ToImmutable()))
            : new Invalid(findings.ToImmutable());
    }

    /// <summary>Returns the stage, or null when it added a finding.</summary>
    private static Noise? ValidateNoise(
        StageRef at, NoiseDescription noise, Dictionary<string, int> firstUse, ImmutableArray<Finding>.Builder findings)
    {
        var findingsBefore = findings.Count;

        var id = CheckId(at, firstUse, findings);

        CheckRange(at, Parameter.Width, noise.Width, PixelSize.IsInRange(noise.Width), PixelSizeRange, findings);
        CheckRange(at, Parameter.Height, noise.Height, PixelSize.IsInRange(noise.Height), PixelSizeRange, findings);
        var octavesInRange =
            CheckRange(at, Parameter.Octaves, noise.Octaves, Octaves.IsInRange(noise.Octaves), OctavesRange, findings);
        var cellSizeInRange =
            CheckRange(at, Parameter.CellSize, noise.CellSize, CellSize.IsInRange(noise.CellSize), CellSizeRange, findings);
        CheckRange(
            at, Parameter.Persistence, noise.Persistence, Persistence.IsInRange(noise.Persistence), PersistenceRange,
            findings);

        // Checked only when both values are in range, so one wrong value doesn't report twice.
        if (octavesInRange && cellSizeInRange)
        {
            var smallest = NoiseParameters.SmallestValidCellSize(noise.Octaves);
            if (noise.CellSize < smallest)
            {
                findings.Add(new FinestOctaveBelowOnePixel(at, noise.CellSize, noise.Octaves, smallest));
            }
        }

        // Every wrapper below is in range only when this stage added no finding.
        if (id is null || findings.Count != findingsBefore)
        {
            return null;
        }

        var parameters = new NoiseParameters(
            new PixelSize(noise.Width),
            new PixelSize(noise.Height),
            new Octaves(noise.Octaves),
            new CellSize(noise.CellSize),
            new Persistence(noise.Persistence));
        return new Noise(id, parameters);
    }

    /// <summary>
    /// Returns the stage, or null when it added a finding or its source was not built. Checks the
    /// id, then the source, then the level.
    /// </summary>
    private static Threshold? ValidateThreshold(
        StageRef at, ThresholdDescription threshold, PipelineDescription description, ReadOnlySpan<Stage?> built,
        Dictionary<string, int> firstUse, ImmutableArray<Finding>.Builder findings)
    {
        var findingsBefore = findings.Count;

        var id = CheckId(at, firstUse, findings);
        var source = CheckInput(at, Input.Source, threshold.Source, description, built, findings);
        CheckRange(
            at, Parameter.Level, threshold.Level, ThresholdLevel.IsInRange(threshold.Level), LevelRange, findings);

        // The level is in range only when this stage added no finding. A null source with no finding
        // here is a source with findings of its own: the pipeline is invalid through those.
        if (id is null || source is null || findings.Count != findingsBefore)
        {
            return null;
        }

        return new Threshold(id, new ThresholdParameters(source, new ThresholdLevel(threshold.Level)));
    }

    /// <summary>
    /// Returns the stage, or null when it added a finding or its source was not built. Checks the
    /// id, then the source, then the tile width: its range, then, only when it is in range, whether
    /// it is a multiple of <see cref="IsoTileSize.WidthStep"/>. Then left, then top, and last
    /// whether the tile's box fits inside the source's output. The fit is checked only when the
    /// source was built and the tile width, left and top added no finding, so a bad source or a
    /// bad value isn't reported a second time as a tile that doesn't fit. A bad id doesn't stop
    /// it: the id and the box are separate mistakes, and both are reported.
    /// </summary>
    private static CutTile? ValidateCutTile(
        StageRef at, CutTileDescription cut, PipelineDescription description, ReadOnlySpan<Stage?> built,
        ReadOnlySpan<ImageSize?> sizes, Dictionary<string, int> firstUse, ImmutableArray<Finding>.Builder findings)
    {
        var findingsBefore = findings.Count;

        var id = CheckId(at, firstUse, findings);
        var source = CheckInput(at, Input.Source, cut.Source, description, built, findings);

        var findingsBeforeValues = findings.Count;
        var tileWidthInRange = CheckRange(
            at, Parameter.TileWidth, cut.TileWidth, IsoTileSize.IsInRange(cut.TileWidth), TileWidthRange, findings);
        if (tileWidthInRange && !IsoTileSize.IsMultipleOfStep(cut.TileWidth))
        {
            findings.Add(new NotMultipleOf(at, Parameter.TileWidth, cut.TileWidth, IsoTileSize.WidthStep));
        }

        CheckRange(at, Parameter.Left, cut.Left, IsPosition(cut.Left), PositionRange, findings);
        CheckRange(at, Parameter.Top, cut.Top, IsPosition(cut.Top), PositionRange, findings);

        // Every value is in range only when the values added no finding, so the sums below can't
        // overflow. A null source with no finding here is a source with findings of its own.
        if (source is null || findings.Count != findingsBeforeValues)
        {
            return null;
        }

        // The source resolved, so its handle names the earlier position where it was built.
        var sourceSize = SizeAt(cut.Source.Index, sizes);
        var size = new IsoTileSize(cut.TileWidth);
        if (cut.Left + size.Width > sourceSize.Width || cut.Top + size.Height > sourceSize.Height)
        {
            findings.Add(new TileOutsideSource(
                at, cut.Left, cut.Top, size.Width, size.Height, sourceSize.Width, sourceSize.Height));
        }

        // A bad id was reported above and leaves the stage unbuilt, as does a tile that doesn't fit.
        if (id is null || findings.Count != findingsBefore)
        {
            return null;
        }

        return new CutTile(id, new CutTileParameters(source, size, new PixelPoint(cut.Left, cut.Top)));
    }

    private static bool IsPosition(int value) =>
        value is >= CutTileParameters.MinPosition and <= CutTileParameters.MaxPosition;

    /// <summary>
    /// The size of the image <paramref name="stage"/> outputs. One arm per stage case and no discard
    /// arm (illegal-states R5), so a new stage kind doesn't compile until it says what size it
    /// outputs. A stage's source is an earlier built stage, so the source's size is already known.
    /// </summary>
    private static ImageSize OutputSize(
        Stage stage, ReadOnlySpan<ImageSize?> sizes, Dictionary<StageId, int> positions) => stage switch
    {
        Noise noise => new ImageSize(noise.Parameters.Width.Value, noise.Parameters.Height.Value),
        Threshold threshold => SizeAt(positions[threshold.Parameters.Source], sizes),
        CutTile cut => new ImageSize(cut.Parameters.Size.Width, cut.Parameters.Size.Height),
    };

    /// <summary>The output size of the stage built at <paramref name="position"/>. None there is a bug in validation.</summary>
    private static ImageSize SizeAt(int position, ReadOnlySpan<ImageSize?> sizes) =>
        sizes[position] ?? throw new InvalidOperationException($"No stage was built at position {position}.");

    /// <summary>
    /// Resolves one input slot. Returns the source's id, or null when the input gave a finding or
    /// names an earlier stage that was not built. The cases, in order:
    /// <list type="number">
    /// <item>No handle: <see cref="MissingInput"/>.</item>
    /// <item>
    /// A position outside the description, or one whose stage was given a different id:
    /// <see cref="UnknownInput"/>. This is how a handle from another builder shows up.
    /// </item>
    /// <item>The stage's own position: <see cref="SelfInput"/>.</item>
    /// <item>A later position: <see cref="LaterInput"/>.</item>
    /// <item>
    /// An earlier position: no finding. The handle is matched against the id as written, so a
    /// source with an invalid or repeated id, or a bad parameter, still resolves; its findings are
    /// reported once, against it, and this input returns null without adding one.
    /// </item>
    /// </list>
    /// The whole description is in hand, so a later stage is told apart from an unknown one.
    /// </summary>
    private static StageId? CheckInput(
        StageRef at, Input slot, StageHandle? handle, PipelineDescription description, ReadOnlySpan<Stage?> built,
        ImmutableArray<Finding>.Builder findings)
    {
        if (handle is null)
        {
            findings.Add(new MissingInput(at, slot));
            return null;
        }

        if (handle.Index >= description.Stages.Length
            || !string.Equals(description.Stages[handle.Index].Id ?? "", handle.Id, StringComparison.Ordinal))
        {
            findings.Add(new UnknownInput(at, slot, handle.Index, handle.Id));
            return null;
        }

        if (handle.Index == at.Index)
        {
            findings.Add(new SelfInput(at, slot));
            return null;
        }

        if (handle.Index > at.Index)
        {
            findings.Add(new LaterInput(at, slot, handle.Index));
            return null;
        }

        return built[handle.Index]?.Id;
    }

    /// <summary>
    /// An id is either invalid or a duplicate, never both. Only a valid id is remembered as a
    /// first use, and a repeat always names the first index, not the previous repeat.
    /// </summary>
    private static StageId? CheckId(
        StageRef at, Dictionary<string, int> firstUse, ImmutableArray<Finding>.Builder findings)
    {
        if (!StageId.IsValid(at.GivenId))
        {
            findings.Add(new InvalidStageId(at));
            return null;
        }

        if (firstUse.TryGetValue(at.GivenId, out var firstIndex))
        {
            findings.Add(new DuplicateStageId(at, firstIndex));
            return null;
        }

        firstUse.Add(at.GivenId, at.Index);
        return new StageId(at.GivenId);
    }

    private static bool CheckRange(
        StageRef at, Parameter parameter, double value, bool inRange, ParameterRange range,
        ImmutableArray<Finding>.Builder findings)
    {
        if (!inRange)
        {
            findings.Add(new OutOfRange(at, parameter, value, range));
        }

        return inRange;
    }
}
