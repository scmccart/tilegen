using System.Collections.Immutable;

namespace Tilegen.Core.Imaging;

/// <summary>
/// Kept pixels that nothing can write: an immutable, row-major grid of <see cref="Rgba32"/>, at
/// least 1 by 1. A technique is given its inputs as images, so writing to an input doesn't compile
/// (functional-core R4). It has no setter and no writable view, and the marshal and unsafe types
/// that could turn its pixels back into an array are banned in Core. Made with
/// <see cref="PixelBuffer.ToImage"/> or <see cref="Frame.ToImage"/>.
/// </summary>
public sealed class Image
{
    private readonly ImmutableArray<Rgba32> _pixels;

    /// <summary>Takes <paramref name="pixels"/>, which the caller copied and holds no writer to.</summary>
    internal Image(int width, int height, ImmutableArray<Rgba32> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (pixels.IsDefault || pixels.Length != checked(width * height))
        {
            throw new ArgumentException("An image holds exactly width × height pixels.", nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public Rgba32 this[int x, int y]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(x);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
            ArgumentOutOfRangeException.ThrowIfNegative(y);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
            return _pixels[(y * Width) + x];
        }
    }

    /// <summary>
    /// Copies the pixels into <paramref name="destination"/> in row-major order, for a bulk read
    /// such as a texture upload. A longer span is allowed, and only its first width × height
    /// elements are written.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than width × height.</exception>
    public void CopyTo(Span<Rgba32> destination)
    {
        if (destination.Length < _pixels.Length)
        {
            throw new ArgumentException(
                $"The destination holds {destination.Length} pixels; the image has {_pixels.Length}.",
                nameof(destination));
        }

        _pixels.AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// A mutable copy, for a technique that wants to start from the image. It shares nothing with
    /// the image.
    /// </summary>
    public PixelBuffer ToBuffer() => new(Width, Height, _pixels.ToArray());
}
