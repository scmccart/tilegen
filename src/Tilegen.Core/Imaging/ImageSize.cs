namespace Tilegen.Core.Imaging;

/// <summary>
/// A width and height of at least 1: the size of the image a stage outputs, which validation works
/// out for each stage it builds. A class with an internal constructor that enforces it, not a
/// positional struct, whose public constructor and default value would bypass the invariant
/// (illegal-states R8).
/// </summary>
public sealed record class ImageSize
{
    internal ImageSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }
}
