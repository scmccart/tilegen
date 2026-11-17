using Tilegen.Core.Pipelines;
using Tilegen.Core.Seeding;

namespace Tilegen.Desktop;

/// <summary>
/// The pipeline the preview window runs until definitions exist (stage-inputs design D7,
/// isometric-tile-geometry design D6): a noise stage, then a threshold that reads it, then a cut
/// that reads the threshold, each through the handle the builder gave. The window shows the noise
/// refining over five frames, then a black-and-white map, then one 128 × 64 diamond tile cut from
/// the map's middle band. It also gives the seed the first run starts with. It is a constant in
/// Desktop's code, and the definitions change deletes it.
/// </summary>
internal static class BuiltInPipeline
{
    public static PipelineDescription Description { get; } = Build();

    public static RunSeed StartSeed { get; } = new(1);

    private static PipelineDescription Build()
    {
        var pipeline = new PipelineBuilder();
        var height = pipeline.Add(
            new NoiseDescription("height", Width: 128, Height: 128, Octaves: 5, CellSize: 32, Persistence: 0.5));
        var terrain = pipeline.Add(new ThresholdDescription("terrain", Source: height, Level: 128));
        pipeline.Add(new CutTileDescription("tile", Source: terrain, TileWidth: 128, Left: 0, Top: 32));
        return pipeline.Build();
    }
}
