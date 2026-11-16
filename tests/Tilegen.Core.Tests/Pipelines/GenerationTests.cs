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
}
