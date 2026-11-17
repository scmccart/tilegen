using CsCheck;
using Tilegen.Core.Geometry;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

/// <summary>
/// Properties of <see cref="DiamondCut.Run"/> over generated sources up to 64 by 64 with random
/// pixels, alpha included, and tile widths and positions whose box fits inside the source.
/// <para>
/// The inputs-unchanged property (testing R9, functional-core R4) is checked after every yielded
/// frame, as it is for the threshold.
/// </para>
/// </summary>
public class CutTileProperties
{
    private const int Iterations = 100;

    /// <summary>The run never reads the source's id; it is given the image directly.</summary>
    private static readonly StageId Source = StageId.Create("noise");

    /// <summary>Picks the range's edges often, because uniform draws rarely land on them.</summary>
    private static Gen<int> IntIn(int min, int max) =>
        Gen.Frequency<int>((4, Gen.Int[min, max]), (1, Gen.Const(min)), (1, Gen.Const(max)));

    private static readonly Gen<Rgba32> AnyPixel =
        Gen.UInt.Select(bits => new Rgba32((byte)bits, (byte)(bits >> 8), (byte)(bits >> 16), (byte)(bits >> 24)));

    /// <summary>A source, a tile width that fits it, and a position that puts the tile's box inside it.</summary>
    private static readonly Gen<(Image Source, CutTileParameters Parameters)> Cuts =
        from width in IntIn(4, 64)
        from height in IntIn(2, 64)
        from pixels in AnyPixel.Array[width * height]
        from tileWidth in IntIn(1, Math.Min(width, height * 2) / 4).Select(steps => steps * 4)
        from left in IntIn(0, width - tileWidth)
        from top in IntIn(0, height - (tileWidth / 2))
        select (
            ImageOf(width, height, pixels),
            CutTileParameters.Create(Source, IsoTileSize.Create(tileWidth), new PixelPoint(left, top)));

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

    [Fact]
    public void Two_runs_on_the_same_input_parameters_and_seed_give_equal_frames()
    {
        Gen.Select(Cuts, Seeds).Sample(
            (cut, seed) =>
            {
                var first = Pixels.Snapshots(DiamondCut.Run(cut.Source, cut.Parameters, new Random(seed)));
                var second = Pixels.Snapshots(DiamondCut.Run(cut.Source, cut.Parameters, new Random(seed)));

                Assert.Equal(first.Count, second.Count);
                Assert.All(first.Zip(second), pair => Assert.Null(Pixels.Difference(pair.First, pair.Second)));
            },
            iter: Iterations);
    }

    [Fact]
    public void A_run_yields_one_frame_the_size_of_the_tiles_box()
    {
        Gen.Select(Cuts, Seeds).Sample(
            (cut, seed) =>
            {
                var frame = Assert.Single(Pixels.Snapshots(DiamondCut.Run(cut.Source, cut.Parameters, new Random(seed))));

                Assert.Equal((cut.Parameters.Size.Width, cut.Parameters.Size.Height), (frame.Width, frame.Height));
            },
            iter: Iterations);
    }

    [Fact]
    public void Inside_pixels_equal_the_source_at_the_offset_and_outside_pixels_are_the_outside_colour()
    {
        Gen.Select(Cuts, Seeds).Sample(
            (cut, seed) =>
            {
                var size = cut.Parameters.Size;
                var position = cut.Parameters.Position;
                var mask = new DiamondMask(size);
                var frame = Assert.Single(Pixels.Snapshots(DiamondCut.Run(cut.Source, cut.Parameters, new Random(seed))));

                for (var y = 0; y < size.Height; y++)
                {
                    for (var x = 0; x < size.Width; x++)
                    {
                        var expected = mask.Contains(x, y) ? cut.Source[position.X + x, position.Y + y] : DiamondCut.Outside;
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
        Gen.Select(Cuts, Seeds).Sample(
            (cut, seed) =>
            {
                var before = cut.Source.ToBuffer();

                var frames = 0;
                foreach (var frame in DiamondCut.Run(cut.Source, cut.Parameters, new Random(seed)))
                {
                    frames++;
                    Assert.Null(Pixels.Difference(before, cut.Source.ToBuffer()));
                }

                Assert.Equal(1, frames);
            },
            iter: Iterations);
    }
}
