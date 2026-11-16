namespace Tilegen.Core.Pipelines;

/// <summary>
/// Names a stage of a description by its position and the id it was added with, so another stage
/// can take it as an input. Only <see cref="PipelineBuilder.Add"/> makes one, and it does so after
/// the stage is appended, so within one builder a handle can name only a stage added before the
/// one that holds it. A handle is a value: two with the same position and id are equal, which keeps
/// descriptions comparable and printable. Validation checks a handle against the description it is
/// used in.
/// </summary>
public sealed record class StageHandle
{
    // Get-only properties and no positional parameters: a public constructor or an init accessor
    // would let a handle be made, or changed with `with`, without adding a stage.
    internal StageHandle(int index, string id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(id);
        Index = index;
        Id = id;
    }

    /// <summary>The stage's position in the description the builder builds, counting from 0.</summary>
    public int Index { get; }

    /// <summary>The id the stage was added with, as written: <c>""</c> for a null id.</summary>
    public string Id { get; }
}
