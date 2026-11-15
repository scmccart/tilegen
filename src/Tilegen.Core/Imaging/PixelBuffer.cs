namespace Tilegen.Core.Imaging;

/// <summary>
/// A mutable, row-major grid of <see cref="Rgba32"/> pixels, at least 1 by 1. The one mutable
/// type in Core (functional-core R11): a technique owns its working buffer and nothing else
/// writes to it.
/// </summary>
public sealed class PixelBuffer
{
    private readonly Rgba32[] _pixels;

    public PixelBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        Width = width;
        Height = height;
        _pixels = new Rgba32[checked(width * height)];
    }

    private PixelBuffer(int width, int height, Rgba32[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public Rgba32 this[int x, int y]
    {
        get => _pixels[IndexOf(x, y)];
        set => _pixels[IndexOf(x, y)] = value;
    }

    /// <summary>A new buffer with the same size and pixels, sharing nothing with this one.</summary>
    public PixelBuffer Copy() => new(Width, Height, (Rgba32[])_pixels.Clone());

    private int IndexOf(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return (y * Width) + x;
    }
}
