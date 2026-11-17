using Tilegen.Core.Geometry;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

public class GenerationTests
{
    private const int Seed = 20261114;

    [Fact]
    public void A_noise_stage_runs_as_value_noise()
    {
        var description = new PipelineDescription([new NoiseDescription("noise", 64, 32, 3, 16, 0.5)]);
        var valid = Assert.IsType<Valid>(Pipeline.Validate(description));
        var noise = Assert.IsType<Noise>(Assert.Single(valid.Pipeline.Stages));

        var expected = Pixels.Snapshots(ValueNoise.Run(noise.Parameters, new Random(Seed)));
        var actual = Pixels.Snapshots(Generation.Run(noise, StageOutputs.Empty, new Random(Seed)));

        Assert.Equal(3, actual.Count);
        Assert.Equal(expected.Count, actual.Count);
        Assert.All(expected.Zip(actual), pair => Assert.Null(Pixels.Difference(pair.First, pair.Second)));
    }

    /// <summary>
    /// The outputs hold a second image under another id, which thresholds differently, so reading
    /// the wrong output fails the comparison.
    /// </summary>
    [Fact]
    public void A_threshold_stage_runs_as_the_threshold_on_its_sources_output()
    {
        var height = NoiseImage(seed: 1);
        var other = NoiseImage(seed: 2);
        var parameters = ThresholdParameters.Create(StageId.Create("height"), ThresholdLevel.Create(128));
        var threshold = new Threshold(StageId.Create("terrain"), parameters);
        var outputs = StageOutputs.Empty.With(StageId.Create("other"), other).With(StageId.Create("height"), height);

        var expected = Pixels.Snapshots(TerrainThreshold.Run(height, parameters, new Random(Seed)));
        var fromOther = Pixels.Snapshots(TerrainThreshold.Run(other, parameters, new Random(Seed)));
        var actual = Pixels.Snapshots(Generation.Run(threshold, outputs, new Random(Seed)));

        Assert.NotNull(Pixels.Difference(Assert.Single(fromOther), Assert.Single(expected)));
        Assert.Null(Pixels.Difference(Assert.Single(expected), Assert.Single(actual)));
    }

    /// <summary>
    /// As for the threshold, the outputs hold a second image under another id, which cuts
    /// differently, so reading the wrong output fails the comparison. The 16 by 8 box at (4, 6) lies
    /// inside both 32 by 24 images.
    /// </summary>
    [Fact]
    public void A_cut_tile_stage_runs_as_the_diamond_cut_on_its_sources_output()
    {
        var height = NoiseImage(seed: 1);
        var other = NoiseImage(seed: 2);
        var parameters = CutTileParameters.Create(StageId.Create("height"), IsoTileSize.Create(16), new PixelPoint(4, 6));
        var cut = new CutTile(StageId.Create("tile"), parameters);
        var outputs = StageOutputs.Empty.With(StageId.Create("other"), other).With(StageId.Create("height"), height);

        var expected = Pixels.Snapshots(DiamondCut.Run(height, parameters, new Random(Seed)));
        var fromOther = Pixels.Snapshots(DiamondCut.Run(other, parameters, new Random(Seed)));
        var actual = Pixels.Snapshots(Generation.Run(cut, outputs, new Random(Seed)));

        Assert.NotNull(Pixels.Difference(Assert.Single(fromOther), Assert.Single(expected)));
        Assert.Null(Pixels.Difference(Assert.Single(expected), Assert.Single(actual)));
    }

    private static Image NoiseImage(int seed)
    {
        var parameters = NoiseParameters.Create(
            PixelSize.Create(32), PixelSize.Create(24), Octaves.Create(3), CellSize.Create(8), Persistence.Create(0.5));
        return ValueNoise.Run(parameters, new Random(seed)).Last().ToImage();
    }
}
