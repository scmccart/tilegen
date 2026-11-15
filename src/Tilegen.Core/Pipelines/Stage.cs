using System.Collections.Immutable;
using Tilegen.Core.Techniques;

namespace Tilegen.Core.Pipelines;

/// <summary>A stage whose id and parameters have been checked. One sealed case per technique.</summary>
public closed record class Stage;

public sealed record class Noise(StageId Id, NoiseParameters Parameters) : Stage;

/// <summary>
/// A pipeline the engine may run. Only validation builds one (illegal-states R3), so it has no
/// public constructor.
/// </summary>
public sealed class ValidatedPipeline
{
    internal ValidatedPipeline(ImmutableArray<Stage> stages)
    {
        if (stages.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A validated pipeline has at least one stage.", nameof(stages));
        }

        Stages = stages;
    }

    public ImmutableArray<Stage> Stages { get; }
}
