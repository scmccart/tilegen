using Tilegen.Core.Imaging;
using Xunit;

namespace Tilegen.Core.Tests.Imaging;

public class PixelBufferTests
{
    [Fact]
    public void A_new_buffer_has_the_given_size_and_transparent_black_pixels()
    {
        var buffer = new PixelBuffer(3, 2);

        Assert.Equal(3, buffer.Width);
        Assert.Equal(2, buffer.Height);
        Assert.Equal(new Rgba32(0, 0, 0, 0), buffer[2, 1]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 4)]
    [InlineData(4, -1)]
    public void A_buffer_needs_a_width_and_height_of_at_least_one(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(width, height));
    }

    [Fact]
    public void A_one_by_one_buffer_is_allowed()
    {
        var buffer = new PixelBuffer(1, 1);

        Assert.Equal(1, buffer.Width);
        Assert.Equal(1, buffer.Height);
    }

    [Fact]
    public void A_pixel_that_was_set_is_read_back_and_its_neighbours_are_untouched()
    {
        var buffer = new PixelBuffer(3, 2);
        var colour = new Rgba32(10, 20, 30, 40);

        buffer[1, 0] = colour;

        Assert.Equal(colour, buffer[1, 0]);
        Assert.Equal(default, buffer[0, 0]);
        Assert.Equal(default, buffer[2, 0]);
        Assert.Equal(default, buffer[1, 1]);
    }

    [Fact]
    public void Every_coordinate_addresses_its_own_pixel()
    {
        var buffer = new PixelBuffer(4, 3);

        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                buffer[x, y] = new Rgba32((byte)x, (byte)y, 0, 255);
            }
        }

        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                Assert.Equal(new Rgba32((byte)x, (byte)y, 0, 255), buffer[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 2)]
    public void A_pixel_outside_the_buffer_cannot_be_read_or_written(int x, int y)
    {
        var buffer = new PixelBuffer(3, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[x, y]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[x, y] = default);
    }

    [Fact]
    public void A_copy_has_the_same_size_and_pixels()
    {
        var buffer = new PixelBuffer(2, 2);
        buffer[1, 1] = new Rgba32(1, 2, 3, 4);

        var copy = buffer.Copy();

        Assert.Equal(2, copy.Width);
        Assert.Equal(2, copy.Height);
        Assert.Equal(new Rgba32(1, 2, 3, 4), copy[1, 1]);
    }

    [Fact]
    public void A_copy_is_independent_of_its_source_in_both_directions()
    {
        var source = new PixelBuffer(2, 1);
        source[0, 0] = new Rgba32(1, 1, 1, 255);
        var copy = source.Copy();

        source[0, 0] = new Rgba32(9, 9, 9, 255);
        copy[1, 0] = new Rgba32(7, 7, 7, 255);

        Assert.Equal(new Rgba32(1, 1, 1, 255), copy[0, 0]);
        Assert.Equal(default, source[1, 0]);
    }

    [Fact]
    public void Copying_to_a_span_writes_the_pixels_in_row_major_order()
    {
        var buffer = new PixelBuffer(3, 2);
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                buffer[x, y] = new Rgba32((byte)x, (byte)y, 0, 255);
            }
        }

        var destination = new Rgba32[6];
        buffer.CopyTo(destination);

        Assert.Equal(
            [
                new Rgba32(0, 0, 0, 255), new Rgba32(1, 0, 0, 255), new Rgba32(2, 0, 0, 255),
                new Rgba32(0, 1, 0, 255), new Rgba32(1, 1, 0, 255), new Rgba32(2, 1, 0, 255),
            ],
            destination);
    }

    [Fact]
    public void Copying_to_a_span_shorter_than_the_buffer_throws()
    {
        var buffer = new PixelBuffer(3, 2);
        var destination = new Rgba32[5];

        Assert.Throws<ArgumentException>(() => buffer.CopyTo(destination));
    }

    [Fact]
    public void Copying_to_a_longer_span_writes_only_its_prefix()
    {
        var buffer = new PixelBuffer(2, 1);
        buffer[0, 0] = new Rgba32(1, 1, 1, 255);
        buffer[1, 0] = new Rgba32(2, 2, 2, 255);
        var untouched = new Rgba32(9, 9, 9, 9);
        var destination = new[] { untouched, untouched, untouched };

        buffer.CopyTo(destination);

        Assert.Equal([new Rgba32(1, 1, 1, 255), new Rgba32(2, 2, 2, 255), untouched], destination);
    }

    [Fact]
    public void An_opaque_pixel_has_full_alpha()
    {
        Assert.Equal(new Rgba32(5, 6, 7, 255), Rgba32.Opaque(5, 6, 7));
    }
}
