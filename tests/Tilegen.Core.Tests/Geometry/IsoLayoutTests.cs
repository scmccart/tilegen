using Tilegen.Core.Geometry;
using Xunit;

namespace Tilegen.Core.Tests.Geometry;

public class IsoLayoutTests
{
    private static readonly IsoTileSize Size = IsoTileSize.Create(64);

    [Fact]
    public void The_tile_at_one_zero_sits_half_a_width_right_and_half_a_height_down()
    {
        Assert.Equal(new PixelPoint(32, 16), IsoLayout.Origin(Size, new TileCoordinate(1, 0)));
    }

    [Fact]
    public void The_tile_at_zero_one_sits_half_a_width_left_and_half_a_height_down()
    {
        Assert.Equal(new PixelPoint(-32, 16), IsoLayout.Origin(Size, new TileCoordinate(0, 1)));
    }

    [Fact]
    public void The_tile_at_zero_zero_sits_at_the_origin()
    {
        Assert.Equal(new PixelPoint(0, 0), IsoLayout.Origin(Size, new TileCoordinate(0, 0)));
    }

    [Fact]
    public void A_coordinate_far_enough_out_to_overflow_throws()
    {
        Assert.Throws<OverflowException>(() => IsoLayout.Origin(Size, new TileCoordinate(int.MaxValue, 0)));
    }
}
