using Tilegen.Core.Geometry;
using Xunit;

namespace Tilegen.Core.Tests.Geometry;

public class IsoTileSizeTests
{
    [Fact]
    public void The_range_and_step_are_published_as_constants()
    {
        Assert.Equal(4, IsoTileSize.Min);
        Assert.Equal(4096, IsoTileSize.Max);
        Assert.Equal(4, IsoTileSize.WidthStep);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(64)]
    [InlineData(4096)]
    public void A_width_in_range_and_a_multiple_of_four_is_accepted(int width)
    {
        Assert.True(IsoTileSize.IsInRange(width));
        Assert.True(IsoTileSize.IsMultipleOfStep(width));
        Assert.Equal(width, IsoTileSize.Create(width).Width);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(30)]
    [InlineData(4100)]
    public void A_width_out_of_range_or_not_a_multiple_of_four_is_refused(int width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IsoTileSize.Create(width));
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(2, false, false)]
    [InlineData(30, true, false)]
    [InlineData(4100, false, true)]
    public void The_two_predicates_report_range_and_step_separately(int width, bool inRange, bool multipleOfStep)
    {
        Assert.Equal(inRange, IsoTileSize.IsInRange(width));
        Assert.Equal(multipleOfStep, IsoTileSize.IsMultipleOfStep(width));
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(64, 32)]
    [InlineData(4096, 2048)]
    public void The_height_is_half_the_width(int width, int height)
    {
        Assert.Equal(height, IsoTileSize.Create(width).Height);
    }
}
