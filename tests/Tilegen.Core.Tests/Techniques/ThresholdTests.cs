using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

/// <summary>
/// One test per scenario in specs/threshold, apart from the validation scenarios
/// (<c>PipelineValidationTests</c>) and the golden (<c>ThresholdGoldenTests</c>), plus the rounding,
/// alpha, randomness and argument rules of the technique itself.
/// </summary>
public class ThresholdTests
{
    private const int Seed = 20261115;

    /// <summary>The run never reads the source's id; it is given the image directly.</summary>
    private static readonly StageId Source = StageId.Create("height");

    private static Rgba32 Grey(byte value) => Rgba32.Opaque(value, value, value);

    private static ThresholdParameters AtLevel(int level) =>
        ThresholdParameters.Create(Source, ThresholdLevel.Create(level));

    /// <summary>A one-row image of the given pixels, left to right.</summary>
    private static Image Row(params Rgba32[] pixels)
    {
        var buffer = new PixelBuffer(pixels.Length, 1);
        for (var x = 0; x < pixels.Length; x++)
        {
            buffer[x, 0] = pixels[x];
        }

        return buffer.ToImage();
    }

    /// <summary>
    /// A 16 by 16 image holding every brightness b from 0 to 255 once, row-major. Red is b + d and
    /// blue b − d, with d up to 7, so the channels sum to exactly 3b and differ wherever b isn't
    /// near an end; alpha varies too, so few pixels are a plain opaque grey.
    /// </summary>
    private static Image EveryBrightness()
    {
        var buffer = new PixelBuffer(16, 16);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var brightness = (y * 16) + x;
                var spread = Math.Min(Math.Min(brightness, 255 - brightness), 7);
                buffer[x, y] = new Rgba32(
                    (byte)(brightness + spread), (byte)brightness, (byte)(brightness - spread), (byte)(255 - brightness));
            }
        }

        return buffer.ToImage();
    }

    private static PixelBuffer OneFrame(Image source, int level, int seed = Seed) =>
        Assert.Single(Pixels.Snapshots(TerrainThreshold.Run(source, AtLevel(level), new Random(seed))));

    // Requirement: Brightness at or above the level is the second terrain.

    [Fact]
    public void Pixels_either_side_of_the_level()
    {
        var frame = OneFrame(Row(Grey(127), Grey(128), Grey(255)), level: 128);

        Assert.Equal(
            [Rgba32.Opaque(0, 0, 0), Rgba32.Opaque(255, 255, 255), Rgba32.Opaque(255, 255, 255)],
            [frame[0, 0], frame[1, 0], frame[2, 0]]);
    }

    [Fact]
    public void Brightness_averages_the_colour_channels()
    {
        var frame = OneFrame(Row(Rgba32.Opaque(255, 30, 15)), level: 100);

        Assert.Equal(Rgba32.Opaque(255, 255, 255), frame[0, 0]);
    }

    [Fact]
    public void Level_0_makes_every_pixel_the_second_terrain()
    {
        var frame = OneFrame(EveryBrightness(), level: 0);

        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                Assert.Equal(Rgba32.Opaque(255, 255, 255), frame[x, y]);
            }
        }
    }

    /// <summary>
    /// (127 + 128 + 128) / 3 is 127⅔ and (128 + 128 + 129) / 3 is 128⅓. Rounded down they are
    /// 127 and 128, so only the second reaches level 128.
    /// </summary>
    [Fact]
    public void Brightness_is_rounded_down()
    {
        var frame = OneFrame(Row(Rgba32.Opaque(127, 128, 128), Rgba32.Opaque(128, 128, 129)), level: 128);

        Assert.Equal(Rgba32.Opaque(0, 0, 0), frame[0, 0]);
        Assert.Equal(Rgba32.Opaque(255, 255, 255), frame[1, 0]);
    }

    [Fact]
    public void Alpha_is_ignored_and_the_output_is_opaque()
    {
        var frame = OneFrame(Row(new Rgba32(200, 200, 200, 0), new Rgba32(10, 10, 10, 255)), level: 128);

        Assert.Equal(Rgba32.Opaque(255, 255, 255), frame[0, 0]);
        Assert.Equal(Rgba32.Opaque(0, 0, 0), frame[1, 0]);
    }

    [Fact]
    public void Each_pixel_is_white_exactly_when_its_brightness_is_at_or_above_the_level()
    {
        var frame = OneFrame(EveryBrightness(), level: 100);

        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var expected = (y * 16) + x >= 100 ? Rgba32.Opaque(255, 255, 255) : Rgba32.Opaque(0, 0, 0);
                Assert.Equal(expected, frame[x, y]);
            }
        }
    }

    [Fact]
    public void The_two_terrains_are_named_opaque_black_and_opaque_white()
    {
        Assert.Equal(Rgba32.Opaque(0, 0, 0), TerrainThreshold.Below);
        Assert.Equal(Rgba32.Opaque(255, 255, 255), TerrainThreshold.AtOrAbove);
    }

    // Requirement: The output is the input's size, in one frame.

    [Fact]
    public void One_frame_of_the_inputs_size()
    {
        var source = new PixelBuffer(40, 24).ToImage();

        var frames = Pixels.Snapshots(TerrainThreshold.Run(source, AtLevel(128), new Random(Seed)));

        var frame = Assert.Single(frames);
        Assert.Equal((40, 24), (frame.Width, frame.Height));
    }

    // Requirement: The threshold draws no randomness.

    [Fact]
    public void The_seed_does_not_matter()
    {
        var source = EveryBrightness();

        var one = OneFrame(source, level: 128, seed: 1);
        var two = OneFrame(source, level: 128, seed: 2);

        Assert.Null(Pixels.Difference(one, two));
    }

    [Fact]
    public void A_whole_run_draws_nothing_from_the_random()
    {
        var random = new Random(Seed);

        Assert.Single(Pixels.Snapshots(TerrainThreshold.Run(EveryBrightness(), AtLevel(128), random)));

        Assert.Equal(new Random(Seed).NextDouble(), random.NextDouble());
    }

    // Requirement: The threshold leaves its input unchanged.

    [Fact]
    public void The_input_is_unchanged_after_the_run()
    {
        var source = EveryBrightness();
        var before = source.ToBuffer();

        Assert.Single(Pixels.Snapshots(TerrainThreshold.Run(source, AtLevel(128), new Random(Seed))));

        Assert.Null(Pixels.Difference(before, source.ToBuffer()));
    }

    // The iterator: arguments are checked at the call, before any frame is requested.

    [Fact]
    public void A_missing_argument_is_refused_at_the_call_without_enumerating()
    {
        var source = Row(Grey(1));

        Assert.Throws<ArgumentNullException>(() => TerrainThreshold.Run(null!, AtLevel(128), new Random(Seed)));
        Assert.Throws<ArgumentNullException>(() => TerrainThreshold.Run(source, null!, new Random(Seed)));
        Assert.Throws<ArgumentNullException>(() => TerrainThreshold.Run(source, AtLevel(128), null!));
    }
}
