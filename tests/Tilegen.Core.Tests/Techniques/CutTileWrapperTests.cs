using Tilegen.Core.Geometry;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

public class CutTileWrapperTests
{
    private static readonly StageId Source = StageId.Create("noise");

    private static readonly IsoTileSize Size = IsoTileSize.Create(64);

    [Fact]
    public void The_position_range_is_published_as_constants()
    {
        Assert.Equal(0, CutTileParameters.MinPosition);
        Assert.Equal(4095, CutTileParameters.MaxPosition);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 16)]
    [InlineData(4095, 4095)]
    [InlineData(4095, 0)]
    [InlineData(0, 4095)]
    public void A_position_inside_its_range_is_accepted(int x, int y)
    {
        var parameters = CutTileParameters.Create(Source, Size, new PixelPoint(x, y));

        Assert.Equal(new PixelPoint(x, y), parameters.Position);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4096, 0)]
    [InlineData(0, 4096)]
    [InlineData(int.MinValue, 0)]
    [InlineData(0, int.MaxValue)]
    public void A_position_outside_its_range_is_refused(int x, int y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CutTileParameters.Create(Source, Size, new PixelPoint(x, y)));
    }

    [Fact]
    public void Cut_tile_parameters_keep_the_values_they_were_built_from()
    {
        var parameters = CutTileParameters.Create(Source, Size, new PixelPoint(2, 3));

        Assert.Equal(Source, parameters.Source);
        Assert.Equal(Size, parameters.Size);
        Assert.Equal(new PixelPoint(2, 3), parameters.Position);
    }

    [Fact]
    public void Cut_tile_parameters_refuse_a_missing_source()
    {
        Assert.Throws<ArgumentNullException>(() => CutTileParameters.Create(null!, Size, new PixelPoint(0, 0)));
    }

    [Fact]
    public void Cut_tile_parameters_refuse_a_missing_size()
    {
        Assert.Throws<ArgumentNullException>(() => CutTileParameters.Create(Source, null!, new PixelPoint(0, 0)));
    }

    [Fact]
    public void Cut_tile_parameters_with_the_same_values_are_equal()
    {
        var first = CutTileParameters.Create(Source, Size, new PixelPoint(2, 3));
        var second = CutTileParameters.Create(Source, IsoTileSize.Create(64), new PixelPoint(2, 3));

        Assert.Equal(first, second);
        Assert.NotEqual(first, CutTileParameters.Create(Source, Size, new PixelPoint(2, 4)));
        Assert.NotEqual(first, CutTileParameters.Create(Source, IsoTileSize.Create(68), new PixelPoint(2, 3)));
        Assert.NotEqual(first, CutTileParameters.Create(StageId.Create("other"), Size, new PixelPoint(2, 3)));
    }
}
