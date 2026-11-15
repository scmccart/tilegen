using StbImageSharp;
using StbImageWriteSharp;
using Tilegen.Core.Imaging;

namespace Tilegen.TestKit.Goldens;

/// <summary>PNG encoding and decoding for test helpers, over <see cref="PixelBuffer"/>.</summary>
public static class Png
{
    private const int Channels = 4;

    /// <summary>Encodes the buffer as an 8-bit RGBA PNG.</summary>
    public static byte[] Encode(PixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var data = new byte[checked(buffer.Width * buffer.Height * Channels)];
        var offset = 0;
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var pixel = buffer[x, y];
                data[offset++] = pixel.R;
                data[offset++] = pixel.G;
                data[offset++] = pixel.B;
                data[offset++] = pixel.A;
            }
        }

        using var stream = new MemoryStream();
        new ImageWriter().WritePng(
            data,
            buffer.Width,
            buffer.Height,
            StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha,
            stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Decodes a PNG to a buffer, whatever its colour type, as 8-bit RGBA. Throws
    /// <see cref="InvalidOperationException"/> for bytes that aren't a PNG StbImageSharp can read:
    /// a corrupt golden is a bug in the repository, not an expected outcome.
    /// </summary>
    public static PixelBuffer Decode(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);

        var image = ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        var buffer = new PixelBuffer(image.Width, image.Height);
        var offset = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                buffer[x, y] = new Rgba32(
                    image.Data[offset],
                    image.Data[offset + 1],
                    image.Data[offset + 2],
                    image.Data[offset + 3]);
                offset += Channels;
            }
        }

        return buffer;
    }
}
