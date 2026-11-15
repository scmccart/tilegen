using Tilegen.Core.Pipelines;
using Tilegen.Core.Seeding;

namespace Tilegen.Desktop;

/// <summary>
/// The pipeline the preview window runs until definitions exist (design D7): two noise stages, so
/// the timeline crosses a stage boundary, and the seed the first run starts with. It is a constant
/// in Desktop's code, and the definitions change deletes it.
/// </summary>
internal static class BuiltInPipeline
{
    public static PipelineDescription Description { get; } = new(
    [
        new NoiseDescription("coarse", Width: 128, Height: 128, Octaves: 4, CellSize: 32, Persistence: 0.5),
        new NoiseDescription("fine", Width: 128, Height: 128, Octaves: 6, CellSize: 32, Persistence: 0.6),
    ]);

    public static RunSeed StartSeed { get; } = new(1);
}
