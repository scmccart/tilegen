using Tilegen.Core.Imaging;
using Xunit;

namespace Tilegen.Core.Tests.Imaging;

public class ImageSizeTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(64, 32)]
    [InlineData(4096, 4096)]
    public void A_width_and_height_of_at_least_one_are_kept(int width, int height)
    {
        var size = new ImageSize(width, height);

        Assert.Equal((width, height), (size.Width, size.Height));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 64)]
    [InlineData(64, int.MinValue)]
    public void A_width_or_height_below_one_is_refused(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageSize(width, height));
    }

    [Fact]
    public void Sizes_with_the_same_width_and_height_are_equal()
    {
        Assert.Equal(new ImageSize(64, 32), new ImageSize(64, 32));
        Assert.NotEqual(new ImageSize(64, 32), new ImageSize(32, 64));
    }
}
