using Tilegen.Core.Geometry;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Tilegen.TestKit.Goldens;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

/// <summary>
/// The cut-tile golden (testing R3). The test takes its input image, tile width and position from
/// the golden's provenance file, so the two can't drift apart (testing R5). The input is a stored,
/// approved golden of another technique, not a fresh run of it, so a change to that technique doesn't
/// move this golden; re-recording the input golden does, and both then go to the owner.
/// </summary>
public class CutTileGoldenTests
{
    private const string Technique = "cut-tile";

    /// <summary>The run never reads the source's id; it is given the image directly.</summary>
    private static readonly StageId Source = StageId.Create("noise");

    [Fact]
    public void The_basic_cut_tile_stage_matches_its_approved_golden()
    {
        var provenance = Golden.ReadProvenance(Technique, "basic");
        Assert.Equal(Technique, provenance.Technique);
        var recorded = provenance.Parameters;
        var input = recorded.GetProperty("input").GetString();
        Assert.NotNull(input);
        var parameters = CutTileParameters.Create(
            Source,
            IsoTileSize.Create(recorded.GetProperty("tileWidth").GetInt32()),
            new PixelPoint(recorded.GetProperty("left").GetInt32(), recorded.GetProperty("top").GetInt32()));

        // The input is named relative to Goldens/, with '/' between the parts on every platform.
        var inputPath = Path.Combine([AppContext.BaseDirectory, "Goldens", .. input.Split('/')]);
        var source = Png.Decode(File.ReadAllBytes(inputPath)).ToImage();

        var output = Assert.Single(Pixels.Snapshots(DiamondCut.Run(source, parameters, new Random(provenance.StageSeed))));

        Golden.AssertMatches(Technique, "basic", output, provenance);
    }
}
