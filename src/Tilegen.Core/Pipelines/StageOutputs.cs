using System.Collections.Immutable;
using Tilegen.Core.Imaging;

namespace Tilegen.Core.Pipelines;

/// <summary>
/// The last image of each stage that has finished in a run. Immutable: <see cref="With"/> returns
/// a new value. The engine adds each finished stage, and <see cref="Generation.Run"/> picks a
/// stage's inputs from it (functional-core R2). It is only looked up by id and never enumerated,
/// so hash order can't reach the pixels (functional-core R8).
/// </summary>
public sealed class StageOutputs
{
    private readonly ImmutableDictionary<StageId, Image> _images;

    private StageOutputs(ImmutableDictionary<StageId, Image> images)
    {
        _images = images;
    }

    /// <summary>No stage has finished yet.</summary>
    public static StageOutputs Empty { get; } = new(ImmutableDictionary<StageId, Image>.Empty);

    /// <summary>
    /// The last image of <paramref name="stage"/>. A missing stage is a bug: validation guarantees
    /// that every source runs before the stage that reads it.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No image was added for <paramref name="stage"/>.</exception>
    public Image this[StageId stage]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(stage);
            return _images.TryGetValue(stage, out var image)
                ? image
                : throw new KeyNotFoundException($"No output has been kept for stage \"{stage}\".");
        }
    }

    /// <summary>
    /// These outputs, with <paramref name="last"/> as <paramref name="stage"/>'s last image. This
    /// value doesn't change.
    /// </summary>
    public StageOutputs With(StageId stage, Image last)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(last);
        return new StageOutputs(_images.SetItem(stage, last));
    }
}
