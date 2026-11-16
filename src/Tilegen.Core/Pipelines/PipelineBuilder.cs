using System.Collections.Immutable;

namespace Tilegen.Core.Pipelines;

/// <summary>
/// Builds a <see cref="PipelineDescription"/> by adding stages in order, and hands out a
/// <see cref="StageHandle"/> for each, so a later stage can name an earlier one as its input.
/// </summary>
/// <remarks>
/// Mutable while a description is built (functional-core R11): it collects stages in order and
/// gives each a handle. It doesn't escape the code that builds the description, and what it builds
/// is an immutable snapshot, so no description changes when the builder does. An immutable builder
/// would make every definition thread a (description, handle) pair through each line; this is the
/// trade <see cref="ImmutableArray{T}.Builder"/> makes.
/// </remarks>
public sealed class PipelineBuilder
{
    private readonly ImmutableArray<StageDescription>.Builder _stages =
        ImmutableArray.CreateBuilder<StageDescription>();

    /// <summary>
    /// Appends <paramref name="stage"/> and returns its handle: its position, and its id as written.
    /// </summary>
    public StageHandle Add(StageDescription stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _stages.Add(stage);
        return new StageHandle(_stages.Count - 1, stage.Id ?? "");
    }

    /// <summary>
    /// A description of the stages added so far, in order. It is a copy: a later <see cref="Add"/>
    /// doesn't change it, and <see cref="Build"/> may be called again. Nothing is checked here;
    /// <see cref="Pipeline.Validate"/> does that, and an empty builder gives an empty description.
    /// </summary>
    public PipelineDescription Build() => new(_stages.ToImmutable());
}
