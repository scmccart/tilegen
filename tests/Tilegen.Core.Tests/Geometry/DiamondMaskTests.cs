using Tilegen.Core.Geometry;
using Xunit;

namespace Tilegen.Core.Tests.Geometry;

public class DiamondMaskTests
{
    /// <summary>The mask row by row, with <c>#</c> inside and <c>.</c> outside.</summary>
    private static string[] Picture(DiamondMask mask)
    {
        var rows = new string[mask.Size.Height];
        for (var y = 0; y < rows.Length; y++)
        {
            var chars = new char[mask.Size.Width];
            for (var x = 0; x < chars.Length; x++)
            {
                chars[x] = mask.Contains(x, y) ? '#' : '.';
            }

            rows[y] = new string(chars);
        }

        return rows;
    }

    [Fact]
    public void A_null_size_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new DiamondMask(null!));
    }

    [Fact]
    public void The_mask_of_an_eight_by_four_tile_is_the_smallest_diamond_with_two_rows_of_steps()
    {
        var mask = new DiamondMask(IsoTileSize.Create(8));

        Assert.Equal(["...##...", ".######.", ".######.", "...##..."], Picture(mask));
    }

    [Fact]
    public void A_sixty_four_by_thirty_two_tile_has_its_tips_in_the_middle_and_empty_side_columns()
    {
        var mask = new DiamondMask(IsoTileSize.Create(64));
        var picture = Picture(mask);

        Assert.Equal(new string('.', 31) + "##" + new string('.', 31), picture[0]);
        Assert.Equal(new string('.', 31) + "##" + new string('.', 31), picture[31]);
        Assert.All(picture, row =>
        {
            Assert.Equal('.', row[0]);
            Assert.Equal('.', row[63]);
        });
    }

    [Fact]
    public void The_two_middle_rows_of_a_sixty_four_by_thirty_two_tile_are_sixty_two_pixels_wide()
    {
        var mask = new DiamondMask(IsoTileSize.Create(64));
        var picture = Picture(mask);

        Assert.Equal(62, picture[15].Count(c => c == '#'));
        Assert.Equal(62, picture[16].Count(c => c == '#'));
        Assert.Equal(new RowSpan(1, 62), mask.Row(15));
        Assert.Equal(new RowSpan(1, 62), mask.Row(16));
    }

    [Fact]
    public void A_sixty_four_by_thirty_two_tile_has_1024_pixels_inside()
    {
        var mask = new DiamondMask(IsoTileSize.Create(64));

        Assert.Equal(1024, Picture(mask).Sum(row => row.Count(c => c == '#')));
    }

    [Fact]
    public void The_rows_of_a_sixteen_by_eight_tile_grow_by_two_pixels_on_each_side()
    {
        var mask = new DiamondMask(IsoTileSize.Create(16));
        int[] widths = [2, 6, 10, 14, 14, 10, 6, 2];

        for (var y = 0; y < widths.Length; y++)
        {
            Assert.Equal(new RowSpan(8 - (widths[y] / 2), widths[y]), mask.Row(y));
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 4)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void Contains_outside_the_box_throws(int x, int y)
    {
        var mask = new DiamondMask(IsoTileSize.Create(8));

        Assert.Throws<ArgumentOutOfRangeException>(() => mask.Contains(x, y));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void Row_outside_the_box_throws(int y)
    {
        var mask = new DiamondMask(IsoTileSize.Create(8));

        Assert.Throws<ArgumentOutOfRangeException>(() => mask.Row(y));
    }
}
