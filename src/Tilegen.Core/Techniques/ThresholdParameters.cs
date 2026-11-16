using Tilegen.Core.Pipelines;

namespace Tilegen.Core.Techniques;

/// <summary>
/// The checked parameters of a threshold stage: the stage it reads, and its level. The source is an
/// input slot, a parameter like the level (<c>docs/design/stage-inputs.md</c>).
/// <para>
/// Validation builds these only when the source names an earlier stage that is itself valid, so in a
/// <see cref="ValidatedPipeline"/> the source always runs first. <see cref="Create"/> can name any
/// stage, so that guarantee belongs to the validated pipeline, not to this record.
/// </para>
/// </summary>
public sealed record class ThresholdParameters
{
    internal ThresholdParameters(StageId source, ThresholdLevel level)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(level);
        Source = source;
        Level = level;
    }

    /// <summary>The stage whose last image this stage reads.</summary>
    public StageId Source { get; }

    public ThresholdLevel Level { get; }

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static ThresholdParameters Create(StageId source, ThresholdLevel level) => new(source, level);
}
