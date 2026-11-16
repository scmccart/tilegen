using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;

namespace Tilegen.Engine;

/// <summary>
/// One kept frame and its place in the run: the stage that yielded it, and its step within that
/// stage, counting from 1. <see cref="Image"/> is an immutable copy taken when the frame was
/// yielded, so later steps, and the stages that read it as an input, can't change it
/// (functional-core R4).
/// </summary>
public sealed record class TimelineEntry(StageId Stage, int Step, Image Image)
{
    /// <summary>Declared get-only, so a <c>with</c> expression can't skip the check.</summary>
    public int Step { get; } = Step >= 1
        ? Step
        : throw new ArgumentOutOfRangeException(nameof(Step), Step, "A step counts from 1 within its stage.");
}
