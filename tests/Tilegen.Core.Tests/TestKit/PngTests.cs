using Tilegen.Core.Imaging;
using Tilegen.TestKit.Goldens;
using Xunit;

namespace Tilegen.Core.Tests.TestKit;

public class PngTests
{
    [Fact]
    public void Encoding_then_decoding_gives_back_the_same_size_and_pixels_including_alpha()
    {
        var buffer = new PixelBuffer(5, 3);
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                buffer[x, y] = new Rgba32(
                    (byte)((x * 51) + y),
                    (byte)(255 - (x * 37) - (y * 11)),
                    (byte)((x * y * 17) + 3),
                    (byte)((x + (y * buffer.Width)) * 17));
            }
        }

        var decoded = Png.Decode(Png.Encode(buffer));

        Assert.Equal((buffer.Width, buffer.Height), (decoded.Width, decoded.Height));
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                Assert.Equal(buffer[x, y], decoded[x, y]);
            }
        }
    }

    [Fact]
    public void A_translucent_pixel_keeps_its_colour_channels_when_alpha_is_zero()
    {
        var buffer = new PixelBuffer(1, 1);
        buffer[0, 0] = new Rgba32(200, 100, 50, 0);

        var decoded = Png.Decode(Png.Encode(buffer));

        Assert.Equal(new Rgba32(200, 100, 50, 0), decoded[0, 0]);
    }

    [Fact]
    public void Encoded_bytes_start_with_the_png_signature()
    {
        var bytes = Png.Encode(new PixelBuffer(1, 1));

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);
    }
}
