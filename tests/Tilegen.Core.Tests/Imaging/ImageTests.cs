using System.Reflection;
using Tilegen.Core.Imaging;
using Xunit;

namespace Tilegen.Core.Tests.Imaging;

public class ImageTests
{
    private static PixelBuffer TwoByTwo()
    {
        var buffer = new PixelBuffer(2, 2);
        buffer[0, 0] = new Rgba32(1, 0, 0, 255);
        buffer[1, 0] = new Rgba32(2, 0, 0, 255);
        buffer[0, 1] = new Rgba32(3, 0, 0, 255);
        buffer[1, 1] = new Rgba32(4, 0, 0, 255);
        return buffer;
    }

    [Fact]
    public void An_image_of_a_buffer_has_the_buffers_size_and_pixels()
    {
        var buffer = new PixelBuffer(3, 2);
        buffer[2, 1] = new Rgba32(1, 2, 3, 4);

        var image = buffer.ToImage();

        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(new Rgba32(1, 2, 3, 4), image[2, 1]);
    }

    [Fact]
    public void A_write_to_the_buffer_after_ToImage_does_not_change_the_image()
    {
        var buffer = new PixelBuffer(1, 1);
        buffer[0, 0] = new Rgba32(1, 1, 1, 255);

        var image = buffer.ToImage();
        buffer[0, 0] = new Rgba32(2, 2, 2, 255);

        Assert.Equal(new Rgba32(1, 1, 1, 255), image[0, 0]);
    }

    [Fact]
    public void A_write_to_a_frames_buffer_after_ToImage_does_not_change_the_image()
    {
        var buffer = new PixelBuffer(1, 1);
        buffer[0, 0] = new Rgba32(1, 1, 1, 255);
        var frame = new Frame(buffer);

        var image = frame.ToImage();
        buffer[0, 0] = new Rgba32(2, 2, 2, 255);

        Assert.Equal(new Rgba32(1, 1, 1, 255), image[0, 0]);
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
    }

    [Fact]
    public void An_image_copied_to_a_span_is_in_row_major_order()
    {
        var image = TwoByTwo().ToImage();
        var destination = new Rgba32[4];

        image.CopyTo(destination);

        Assert.Equal(
            [new Rgba32(1, 0, 0, 255), new Rgba32(2, 0, 0, 255), new Rgba32(3, 0, 0, 255), new Rgba32(4, 0, 0, 255)],
            destination);
    }

    [Fact]
    public void An_image_copied_to_a_longer_span_writes_only_its_pixels()
    {
        var image = new PixelBuffer(1, 1).ToImage();
        var marker = new Rgba32(9, 9, 9, 9);
        Rgba32[] destination = [marker, marker];

        image.CopyTo(destination);

        Assert.Equal([default, marker], destination);
    }

    [Fact]
    public void An_image_copied_to_a_span_shorter_than_the_image_throws()
    {
        var image = TwoByTwo().ToImage();
        var destination = new Rgba32[3];

        Assert.Throws<ArgumentException>(() => image.CopyTo(destination));
    }

    [Fact]
    public void ToBuffer_gives_a_copy_with_the_images_size_and_pixels()
    {
        var image = TwoByTwo().ToImage();

        var copy = image.ToBuffer();

        Assert.Equal(2, copy.Width);
        Assert.Equal(2, copy.Height);
        Assert.Equal(
            [image[0, 0], image[1, 0], image[0, 1], image[1, 1]],
            [copy[0, 0], copy[1, 0], copy[0, 1], copy[1, 1]]);
    }

    [Fact]
    public void A_write_to_a_buffer_from_ToBuffer_does_not_change_the_image_or_another_copy()
    {
        var image = TwoByTwo().ToImage();
        var first = image.ToBuffer();
        var second = image.ToBuffer();

        first[0, 0] = new Rgba32(7, 7, 7, 7);

        Assert.Equal(new Rgba32(1, 0, 0, 255), image[0, 0]);
        Assert.Equal(new Rgba32(1, 0, 0, 255), second[0, 0]);
    }

    [Fact]
    public void An_image_has_no_public_constructor_and_no_public_way_to_write_its_pixels()
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var members = typeof(Image).GetMembers(Declared).Select(member => member.Name).Order(StringComparer.Ordinal);
        var indexer = typeof(Image).GetProperty("Item");

        // Getters, a copy into the caller's span and a copy into a new buffer; nothing that hands
        // out the pixels themselves.
        Assert.Equal(
            ["CopyTo", "Height", "Item", "ToBuffer", "Width", "get_Height", "get_Item", "get_Width"], members);
        Assert.NotNull(indexer);
        Assert.False(indexer.CanWrite);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    [InlineData(2, 1)]
    public void Reading_a_pixel_outside_the_image_throws(int x, int y)
    {
        // (2, 0) and (2, 1) are inside the pixel count, so a flat-index check would read the next row.
        var image = TwoByTwo().ToImage();

        Assert.Throws<ArgumentOutOfRangeException>(() => image[x, y]);
    }
}
