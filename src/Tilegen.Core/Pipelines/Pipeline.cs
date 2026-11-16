using System.Collections.Immutable;
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

    /// <summary>
    /// Checks the whole description in one pass (railway R6). Stages are checked in description
    /// order; within a stage the id comes first, then each parameter in declaration order, inputs
    /// included, then the cross-field rule. A description with no findings gives a
    /// <see cref="Valid"/> pipeline.
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
            };

            built[index] = stage;
            if (stage is not null)
            {
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
