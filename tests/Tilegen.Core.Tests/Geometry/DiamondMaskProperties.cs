using CsCheck;
using Tilegen.Core.Geometry;
using Xunit;

namespace Tilegen.Core.Tests.Geometry;

/// <summary>Properties of <see cref="DiamondMask"/> and <see cref="IsoLayout"/> over widths 4 to 256 in steps of 4.</summary>
public class DiamondMaskProperties
{
    private const int Iterations = 64;

    private static readonly Gen<IsoTileSize> Sizes =
        Gen.Int[1, 64].Select(steps => IsoTileSize.Create(steps * IsoTileSize.WidthStep));

    [Fact]
    public void The_mask_is_the_same_mirrored_left_to_right_and_top_to_bottom()
    {
        Sizes.Sample(
            size =>
            {
                var mask = new DiamondMask(size);
                for (var y = 0; y < size.Height; y++)
                {
                    for (var x = 0; x < size.Width; x++)
                    {
                        var inside = mask.Contains(x, y);
                        Assert.Equal(inside, mask.Contains(size.Width - 1 - x, y));
                        Assert.Equal(inside, mask.Contains(x, size.Height - 1 - y));
                    }
                }
            },
            iter: Iterations);
    }

    [Fact]
    public void Exactly_half_the_pixels_of_the_box_are_inside()
    {
        Sizes.Sample(
            size =>
            {
                var mask = new DiamondMask(size);
                var count = 0;
                for (var y = 0; y < size.Height; y++)
                {
                    for (var x = 0; x < size.Width; x++)
                    {
                        if (mask.Contains(x, y))
                        {
                            count++;
                        }
                    }
                }

                Assert.Equal(size.Width * size.Height / 2, count);
            },
            iter: Iterations);
    }

    [Fact]
    public void Each_row_is_one_contiguous_run_that_agrees_with_Row()
    {
        Sizes.Sample(
            size =>
            {
                var mask = new DiamondMask(size);
                for (var y = 0; y < size.Height; y++)
                {
                    var span = mask.Row(y);

                    Assert.Equal((4 * Math.Min(y, size.Height - 1 - y)) + 2, span.Width);
                    Assert.Equal((size.Width / 2) - (span.Width / 2), span.First);

                    for (var x = 0; x < size.Width; x++)
                    {
                        var inRun = x >= span.First && x < span.First + span.Width;
                        Assert.Equal(inRun, mask.Contains(x, y));
                    }
                }
            },
            iter: Iterations);
    }

    [Fact]
    public void Tiles_placed_by_the_layout_cover_every_pixel_around_the_origin_tile_exactly_once()
    {
        const int Reach = 3;

        Sizes.Sample(
            size =>
            {
                var mask = new DiamondMask(size);
                var width = size.Width;
                var height = size.Height;

                // The 3W x 3H region around the origin tile's box: x from -W to 2W - 1, y from -H to 2H - 1.
                var covered = new int[3 * width * 3 * height];

                for (var i = -Reach; i <= Reach; i++)
                {
                    for (var j = -Reach; j <= Reach; j++)
                    {
                        var origin = IsoLayout.Origin(size, new TileCoordinate(i, j));
                        for (var y = 0; y < height; y++)
                        {
                            var regionY = origin.Y + y + height;
                            if (regionY < 0 || regionY >= 3 * height)
                            {
                                continue;
                            }

                            // Walk the mask through Contains, so the property covers the rule itself.
                            for (var x = 0; x < width; x++)
                            {
                                var regionX = origin.X + x + width;
                                if (regionX >= 0 && regionX < 3 * width && mask.Contains(x, y))
                                {
                                    covered[(regionY * 3 * width) + regionX]++;
                                }
                            }
                        }
                    }
                }

                Assert.All(covered, count => Assert.Equal(1, count));
            },
            iter: Iterations);
    }
}
