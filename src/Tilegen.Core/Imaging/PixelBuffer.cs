using System.Collections.Immutable;

namespace Tilegen.Core.Imaging;

/// <summary>
/// A mutable, row-major grid of <see cref="Rgba32"/> pixels, at least 1 by 1. Mutable by design
/// (functional-core R11): a technique owns its working buffer and nothing else writes to it. What
/// is kept, or passed to another stage, is an <see cref="Image"/>.
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

    /// <summary>
    /// Takes <paramref name="pixels"/>, which the caller made for this buffer and holds no other
    /// reference to.
    /// </summary>
    internal PixelBuffer(int width, int height, Rgba32[] pixels)
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

    /// <summary>
    /// An immutable copy of the pixels as they are now. Later writes to the buffer don't change it.
    /// </summary>
    public Image ToImage() => new(Width, Height, ImmutableArray.Create(_pixels));

    /// <summary>
    /// Copies the pixels into <paramref name="destination"/> in row-major order, for a bulk read
    /// such as a texture upload. A longer span is allowed, and only its first width × height
    /// elements are written. The buffer hands out no span over its own pixels, so it stays their
    /// only writer.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than width × height.</exception>
    public void CopyTo(Span<Rgba32> destination)
    {
        if (destination.Length < _pixels.Length)
        {
            throw new ArgumentException(
                $"The destination holds {destination.Length} pixels; the buffer has {_pixels.Length}.",
                nameof(destination));
        }

        _pixels.AsSpan().CopyTo(destination);
    }

    private int IndexOf(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return (y * Width) + x;
    }
}
