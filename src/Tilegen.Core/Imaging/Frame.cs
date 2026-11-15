namespace Tilegen.Core.Imaging;

/// <summary>
/// A read-only view over a technique's working buffer. It is valid only until the run's next
/// step (functional-core R4): call <see cref="ToBuffer"/> to keep one. It carries no step number
/// or stage id; the timeline that needs them is the engine's.
/// </summary>
public sealed class Frame
{
    private readonly PixelBuffer _buffer;

    public Frame(PixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
    }

    public int Width => _buffer.Width;

    public int Height => _buffer.Height;

    public Rgba32 this[int x, int y] => _buffer[x, y];

    /// <summary>An independent copy of the frame's pixels as they are now.</summary>
    public PixelBuffer ToBuffer() => _buffer.Copy();

    /// <summary>
    /// Copies the frame's pixels as they are now into <paramref name="destination"/>, in row-major
    /// order. See <see cref="PixelBuffer.CopyTo"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than width × height.</exception>
    public void CopyTo(Span<Rgba32> destination) => _buffer.CopyTo(destination);
}
