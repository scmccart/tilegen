using Tilegen.Core.Imaging;
using Xunit;

namespace Tilegen.Core.Tests.Imaging;

public class FrameTests
{
    [Fact]
    public void A_frame_reports_the_size_and_pixels_of_its_buffer()
    {
        var buffer = new PixelBuffer(3, 2);
        buffer[2, 1] = new Rgba32(1, 2, 3, 4);

        var frame = new Frame(buffer);

        Assert.Equal(3, frame.Width);
        Assert.Equal(2, frame.Height);
        Assert.Equal(new Rgba32(1, 2, 3, 4), frame[2, 1]);
    }

    [Fact]
    public void A_frame_is_a_view_so_it_shows_later_writes_to_its_buffer()
    {
        var buffer = new PixelBuffer(1, 1);
        var frame = new Frame(buffer);

        buffer[0, 0] = new Rgba32(8, 8, 8, 255);

        Assert.Equal(new Rgba32(8, 8, 8, 255), frame[0, 0]);
    }

    [Fact]
    public void A_kept_copy_is_unaffected_by_later_writes_to_the_buffer()
    {
        var buffer = new PixelBuffer(2, 1);
        buffer[0, 0] = new Rgba32(1, 1, 1, 255);
        var frame = new Frame(buffer);

        var kept = frame.ToBuffer();
        buffer[0, 0] = new Rgba32(2, 2, 2, 255);

        Assert.Equal(new Rgba32(1, 1, 1, 255), kept[0, 0]);
        Assert.Equal(2, kept.Width);
        Assert.Equal(1, kept.Height);
    }

    [Fact]
    public void A_kept_copy_can_be_written_without_changing_the_frame()
    {
        var buffer = new PixelBuffer(1, 1);
        var frame = new Frame(buffer);

        var kept = frame.ToBuffer();
        kept[0, 0] = new Rgba32(5, 5, 5, 255);

        Assert.Equal(default, frame[0, 0]);
    }
}
