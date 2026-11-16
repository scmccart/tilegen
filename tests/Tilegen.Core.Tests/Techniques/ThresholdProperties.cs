using CsCheck;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

/// <summary>
/// Properties of <see cref="TerrainThreshold.Run"/> over generated images up to 32 by 32 with random
/// pixels, alpha included, and levels from 0 to 255.
/// <para>
/// The inputs-unchanged property (testing R9, functional-core R4) lives here: threshold is the
/// first technique that reads another stage's output. An <see cref="Image"/> has no writer, so the
/// type already rules a change out; the property guards against the type being weakened later.
/// </para>
/// </summary>
public class ThresholdProperties
{
    private const int Iterations = 100;

    /// <summary>The run never reads the source's id; it is given the image directly.</summary>
    private static readonly StageId Source = StageId.Create("height");

    /// <summary>Picks the range's edges often, because uniform draws rarely land on them.</summary>
    private static Gen<int> IntIn(int min, int max) =>
        Gen.Frequency<int>((4, Gen.Int[min, max]), (1, Gen.Const(min)), (1, Gen.Const(max)));

    /// <summary>
    /// Any pixel, alpha included, and sometimes a grey, whose brightness is exactly its channel
    /// value, so pixels land on the level itself.
    /// </summary>
    private static readonly Gen<Rgba32> AnyPixel = Gen.Frequency<Rgba32>(
        (3, Gen.UInt.Select(bits => new Rgba32((byte)bits, (byte)(bits >> 8), (byte)(bits >> 16), (byte)(bits >> 24)))),
        (1, Gen.Select(Gen.Byte, Gen.Byte, (grey, alpha) => new Rgba32(grey, grey, grey, alpha))));

    private static readonly Gen<Image> SmallImages =
        from width in IntIn(1, 32)
        from height in IntIn(1, 32)
        from pixels in AnyPixel.Array[width * height]
        select ImageOf(width, height, pixels);

    private static readonly Gen<int> Levels = IntIn(ThresholdLevel.Min, ThresholdLevel.Max);

    /// <summary>Stage seeds are never negative (stage-seeds.md).</summary>
    private static readonly Gen<int> Seeds = Gen.Int[0, int.MaxValue];

    private static Image ImageOf(int width, int height, Rgba32[] pixels)
    {
        var buffer = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer[x, y] = pixels[(y * width) + x];
            }
        }

        return buffer.ToImage();
    }

    private static IEnumerable<Frame> Run(Image source, int level, int seed) =>
        TerrainThreshold.Run(source, ThresholdParameters.Create(Source, ThresholdLevel.Create(level)), new Random(seed));

    [Fact]
    public void Two_runs_on_the_same_input_level_and_seed_give_equal_frames()
    {
        Gen.Select(SmallImages, Levels, Seeds).Sample(
            (source, level, seed) =>
            {
                var first = Pixels.Snapshots(Run(source, level, seed));
                var second = Pixels.Snapshots(Run(source, level, seed));

                Assert.Equal(first.Count, second.Count);
                Assert.All(first.Zip(second), pair => Assert.Null(Pixels.Difference(pair.First, pair.Second)));
            },
            iter: Iterations);
    }

    [Fact]
    public void A_run_yields_one_frame_the_size_of_its_input()
    {
        Gen.Select(SmallImages, Levels, Seeds).Sample(
            (source, level, seed) =>
            {
                var frame = Assert.Single(Pixels.Snapshots(Run(source, level, seed)));

                Assert.Equal((source.Width, source.Height), (frame.Width, frame.Height));
            },
            iter: Iterations);
    }

    [Fact]
    public void Every_pixel_is_opaque_black_or_white_and_white_exactly_where_the_brightness_reaches_the_level()
    {
        Gen.Select(SmallImages, Levels, Seeds).Sample(
            (source, level, seed) =>
            {
                var frame = Assert.Single(Pixels.Snapshots(Run(source, level, seed)));

                for (var y = 0; y < source.Height; y++)
                {
                    for (var x = 0; x < source.Width; x++)
                    {
                        var input = source[x, y];
                        var brightness = (input.R + input.G + input.B) / 3;
                        var expected = brightness >= level ? Rgba32.Opaque(255, 255, 255) : Rgba32.Opaque(0, 0, 0);
                        Assert.Equal(expected, frame[x, y]);
                    }
                }
            },
            iter: Iterations);
    }

    /// <summary>Checked after every yielded frame, not only at the end (testing R9).</summary>
    [Fact]
    public void The_input_is_unchanged_after_every_frame()
    {
        Gen.Select(SmallImages, Levels, Seeds).Sample(
            (source, level, seed) =>
            {
                var before = source.ToBuffer();

                var frames = 0;
                foreach (var frame in Run(source, level, seed))
                {
                    frames++;
                    Assert.Null(Pixels.Difference(before, source.ToBuffer()));
                }

                Assert.Equal(1, frames);
            },
            iter: Iterations);
    }
}
