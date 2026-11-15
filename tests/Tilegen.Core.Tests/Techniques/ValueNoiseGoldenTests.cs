using Tilegen.Core.Imaging;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Tilegen.TestKit.Goldens;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

/// <summary>
/// The value-noise goldens (testing R3). Each test takes its parameters and stage seed from the
/// golden's provenance file, so the two can't drift apart (testing R5).
/// </summary>
public class ValueNoiseGoldenTests
{
    private const string Technique = "value-noise";

    [Fact]
    public void The_basic_noise_stage_matches_its_approved_golden()
    {
        var provenance = Golden.ReadProvenance(Technique, "basic");
        Assert.Equal(Technique, provenance.Technique);
        var recorded = provenance.Parameters;
        var parameters = NoiseParameters.Create(
            PixelSize.Create(recorded.GetProperty("width").GetInt32()),
            PixelSize.Create(recorded.GetProperty("height").GetInt32()),
            Octaves.Create(recorded.GetProperty("octaves").GetInt32()),
            CellSize.Create(recorded.GetProperty("cellSize").GetInt32()),
            Persistence.Create(recorded.GetProperty("persistence").GetDouble()));

        var output = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(provenance.StageSeed)))[^1];

        Golden.AssertMatches(Technique, "basic", output, provenance);
    }
}
