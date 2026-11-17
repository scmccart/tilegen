namespace Tilegen.Core.Geometry;

/// <summary>
/// A 2:1 isometric tile's size: a width from <see cref="Min"/> to <see cref="Max"/> that is a
/// multiple of <see cref="WidthStep"/>. The height is half the width and is computed, never stored.
/// </summary>
public sealed record class IsoTileSize
{
    public const int Min = 4;
    public const int Max = 4096;
    public const int WidthStep = 4;

    internal IsoTileSize(int width)
    {
        if (!IsInRange(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"A tile width is {Min} to {Max}.");
        }

        if (!IsMultipleOfStep(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"A tile width is a multiple of {WidthStep}.");
        }

        Width = width;
    }

    public int Width { get; }

    /// <summary>Half the width. Derived, so a tile that is not 2:1 cannot be written.</summary>
    public int Height => Width / 2;

    public static bool IsInRange(int width) => width is >= Min and <= Max;

    public static bool IsMultipleOfStep(int width) => width % WidthStep == 0;

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static IsoTileSize Create(int width) => new(width);
}
