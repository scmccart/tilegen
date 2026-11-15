using Tilegen.Core.Imaging;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.TestKit;

public class PixelsTests
{
    [Fact]
    public void Buffers_with_the_same_size_and_pixels_have_no_difference()
    {
        var buffer = new PixelBuffer(2, 1);
        buffer[1, 0] = new Rgba32(1, 2, 3, 4);

        Assert.Null(Pixels.Difference(buffer, buffer.Copy()));
    }

    [Fact]
    public void A_size_difference_names_both_sizes()
    {
        var difference = Pixels.Difference(new PixelBuffer(2, 2), new PixelBuffer(3, 1));

        Assert.NotNull(difference);
        Assert.Contains("2x2", difference);
        Assert.Contains("3x1", difference);
    }

    [Fact]
    public void A_pixel_difference_gives_the_count_and_the_first_in_row_major_order()
    {
        var expected = new PixelBuffer(2, 2);
        var actual = expected.Copy();
        actual[1, 0] = new Rgba32(1, 2, 3, 4);
        actual[0, 1] = new Rgba32(5, 6, 7, 8);

        var difference = Pixels.Difference(expected, actual);

        Assert.NotNull(difference);
        Assert.Contains("2 pixel(s) differ", difference);
        Assert.Contains("(1, 0)", difference);
    }

    [Fact]
    public void Snapshots_keep_each_frame_as_it_was_yielded()
    {
        var snapshots = Pixels.Snapshots(Counting(3));

        Assert.Equal([0, 1, 2], snapshots.Select(buffer => (int)buffer[0, 0].R));
    }

    // One working buffer, rewritten before each yield, as a technique does.
    private static IEnumerable<Frame> Counting(int steps)
    {
        var buffer = new PixelBuffer(1, 1);
        var frame = new Frame(buffer);
        for (var step = 0; step < steps; step++)
        {
            buffer[0, 0] = Rgba32.Opaque((byte)step, 0, 0);
            yield return frame;
        }
    }
}
