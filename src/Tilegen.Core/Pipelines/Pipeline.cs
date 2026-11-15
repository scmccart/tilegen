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

    /// <summary>
    /// Checks the whole description in one pass (railway R6). Stages are checked in description
    /// order; within a stage the id comes first, then each parameter in declaration order, then
    /// the cross-field rule. A description with no findings gives a <see cref="Valid"/> pipeline.
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

        for (var index = 0; index < description.Stages.Length; index++)
        {
            Stage? stage = description.Stages[index] switch
            {
                NoiseDescription noise => ValidateNoise(index, noise, firstUse, findings),
            };

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
        int index, NoiseDescription noise, Dictionary<string, int> firstUse, ImmutableArray<Finding>.Builder findings)
    {
        var at = new StageRef(index, noise.Id ?? "");
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
