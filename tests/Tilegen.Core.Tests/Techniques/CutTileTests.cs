using Tilegen.Core.Geometry;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

/// <summary>
/// One test per scenario in specs/cut-tile, apart from the validation scenarios
/// (<c>PipelineValidationTests</c>) and the golden (<c>CutTileGoldenTests</c>), plus the argument
/// rules of the technique itself.
/// </summary>
public class CutTileTests
{
    private const int Seed = 20261116;

    /// <summary>The run never reads the source's id; it is given the image directly.</summary>
    private static readonly StageId Source = StageId.Create("noise");

    private static CutTileParameters Cut(int tileWidth, int left, int top) =>
        CutTileParameters.Create(Source, IsoTileSize.Create(tileWidth), new PixelPoint(left, top));

    /// <summary>An image whose pixels are all different, with alpha varying down the rows.</summary>
    private static Image Distinct(int width, int height)
    {
        var buffer = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer[x, y] = new Rgba32((byte)(x + 1), (byte)(y + 1), (byte)(x + y + 1), (byte)(255 - y));
            }
        }

        return buffer.ToImage();
    }

    private static Image Filled(int width, int height, Rgba32 pixel)
    {
        var buffer = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer[x, y] = pixel;
            }
        }

        return buffer.ToImage();
    }

    private static PixelBuffer OneFrame(Image source, CutTileParameters parameters, int seed = Seed) =>
        Assert.Single(Pixels.Snapshots(DiamondCut.Run(source, parameters, new Random(seed))));

    // Requirement: The cut keeps the diamond and clears the rest.

    [Fact]
    public void Inside_copies_outside_clears()
    {
        var source = Distinct(16, 16);

        var frame = OneFrame(source, Cut(tileWidth: 8, left: 2, top: 3));

        Assert.Equal((8, 4), (frame.Width, frame.Height));
        Assert.Equal(source[5, 3], frame[3, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), frame[0, 0]);
    }

    [Fact]
    public void Every_pixel_inside_the_diamond_is_the_source_pixel_at_the_offset_and_every_other_is_outside()
    {
        var source = Distinct(40, 30);
        var size = IsoTileSize.Create(16);
        var mask = new DiamondMask(size);

        var frame = OneFrame(source, Cut(tileWidth: 16, left: 7, top: 5));

        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var expected = mask.Contains(x, y) ? source[7 + x, 5 + y] : DiamondCut.Outside;
                Assert.Equal(expected, frame[x, y]);
            }
        }
    }

    [Fact]
    public void The_outside_colour_is_transparent_black()
    {
        Assert.Equal(new Rgba32(0, 0, 0, 0), DiamondCut.Outside);
    }

    [Fact]
    public void Outside_is_written_even_where_the_source_is_opaque()
    {
        var source = Filled(8, 4, Rgba32.Opaque(200, 100, 50));

        var frame = OneFrame(source, Cut(tileWidth: 8, left: 0, top: 0));

        Assert.Equal(DiamondCut.Outside, frame[0, 0]);
        Assert.Equal(DiamondCut.Outside, frame[7, 3]);
        Assert.Equal(Rgba32.Opaque(200, 100, 50), frame[3, 1]);
    }

    [Fact]
    public void Source_alpha_is_kept()
    {
        var translucent = new Rgba32(10, 20, 30, 100);
        var source = Filled(8, 4, translucent);
        var mask = new DiamondMask(IsoTileSize.Create(8));

        var frame = OneFrame(source, Cut(tileWidth: 8, left: 0, top: 0));

        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                if (mask.Contains(x, y))
                {
                    Assert.Equal(translucent, frame[x, y]);
                    Assert.Equal(100, frame[x, y].A);
                }
            }
        }
    }

    // Requirement: The cut yields one frame.

    [Fact]
    public void One_frame_of_the_tiles_size()
    {
        var source = Distinct(128, 128);

        var frames = Pixels.Snapshots(DiamondCut.Run(source, Cut(tileWidth: 64, left: 0, top: 0), new Random(Seed)));

        var frame = Assert.Single(frames);
        Assert.Equal((64, 32), (frame.Width, frame.Height));
    }

    [Fact]
    public void A_tile_filling_the_source_exactly_fits()
    {
        var frame = OneFrame(Distinct(8, 4), Cut(tileWidth: 8, left: 0, top: 0));

        Assert.Equal((8, 4), (frame.Width, frame.Height));
    }

    // Requirement: The cut draws no randomness.

    [Fact]
    public void The_seed_does_not_matter()
    {
        var source = Distinct(40, 30);
        var parameters = Cut(tileWidth: 16, left: 7, top: 5);

        var one = OneFrame(source, parameters, seed: 1);
        var two = OneFrame(source, parameters, seed: 2);

        Assert.Null(Pixels.Difference(one, two));
    }

    [Fact]
    public void A_whole_run_draws_nothing_from_the_random()
    {
        var random = new Random(Seed);

        Assert.Single(Pixels.Snapshots(DiamondCut.Run(Distinct(40, 30), Cut(tileWidth: 16, left: 7, top: 5), random)));

        Assert.Equal(new Random(Seed).NextDouble(), random.NextDouble());
    }

    // Requirement: The cut leaves its input unchanged.

    [Fact]
    public void The_input_is_unchanged_after_the_run()
    {
        var source = Distinct(40, 30);
        var before = source.ToBuffer();

        Assert.Single(Pixels.Snapshots(DiamondCut.Run(source, Cut(tileWidth: 16, left: 7, top: 5), new Random(Seed))));

        Assert.Null(Pixels.Difference(before, source.ToBuffer()));
    }

    // The iterator: arguments are checked at the call, before any frame is requested.

    [Fact]
    public void A_missing_argument_is_refused_at_the_call_without_enumerating()
    {
        var source = Distinct(16, 16);
        var parameters = Cut(tileWidth: 8, left: 0, top: 0);

        Assert.Throws<ArgumentNullException>(() => DiamondCut.Run(null!, parameters, new Random(Seed)));
        Assert.Throws<ArgumentNullException>(() => DiamondCut.Run(source, null!, new Random(Seed)));
        Assert.Throws<ArgumentNullException>(() => DiamondCut.Run(source, parameters, null!));
    }

    [Theory]
    [InlineData(8, 9, 0)] // runs off the right edge: 9 + 8 > 16
    [InlineData(8, 0, 13)] // runs off the bottom edge: 13 + 4 > 16
    [InlineData(32, 0, 0)] // wider than the source
    public void A_box_outside_the_source_is_refused_at_the_call_without_enumerating(int tileWidth, int left, int top)
    {
        var source = Distinct(16, 16);

        Assert.Throws<ArgumentException>(() => DiamondCut.Run(source, Cut(tileWidth, left, top), new Random(Seed)));
    }

    [Fact]
    public void A_box_touching_the_far_edges_is_accepted()
    {
        var frame = OneFrame(Distinct(16, 16), Cut(tileWidth: 8, left: 8, top: 12));

        Assert.Equal((8, 4), (frame.Width, frame.Height));
    }
}
