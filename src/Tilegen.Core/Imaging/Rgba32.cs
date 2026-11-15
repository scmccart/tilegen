namespace Tilegen.Core.Imaging;

/// <summary>One 8-bit-per-channel, non-premultiplied RGBA pixel. Core's own colour value.</summary>
public readonly record struct Rgba32(byte R, byte G, byte B, byte A)
{
    /// <summary>An opaque pixel (alpha 255).</summary>
    public static Rgba32 Opaque(byte r, byte g, byte b) => new(r, g, b, 255);
}
