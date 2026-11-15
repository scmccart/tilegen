using Tilegen.Core.Imaging;

namespace Tilegen.Core.Techniques;

/// <summary>
/// The checked parameters of a value-noise stage. Beyond each wrapper's own range, the finest
/// octave must be at least one pixel: cell size is at least <see cref="SmallestValidCellSize"/>.
/// </summary>
public sealed record class NoiseParameters
{
    internal NoiseParameters(
        PixelSize width, PixelSize height, Octaves octaves, CellSize cellSize, Persistence persistence)
    {
        ArgumentNullException.ThrowIfNull(width);
        ArgumentNullException.ThrowIfNull(height);
        ArgumentNullException.ThrowIfNull(octaves);
        ArgumentNullException.ThrowIfNull(cellSize);
        ArgumentNullException.ThrowIfNull(persistence);

        var smallest = SmallestValidCellSize(octaves.Value);
        if (cellSize.Value < smallest)
        {
            throw new ArgumentException(
                $"With {octaves.Value} octaves the cell size is at least {smallest}, not {cellSize.Value}.",
                nameof(cellSize));
        }

        Width = width;
        Height = height;
        Octaves = octaves;
        CellSize = cellSize;
        Persistence = persistence;
    }

    public PixelSize Width { get; }

    public PixelSize Height { get; }

    public Octaves Octaves { get; }

    public CellSize CellSize { get; }

    public Persistence Persistence { get; }

    /// <summary>The smallest cell size that keeps the finest octave at one pixel or more: 2^(octaves - 1).</summary>
    public static int SmallestValidCellSize(int octaves)
    {
        if (!Techniques.Octaves.IsInRange(octaves))
        {
            throw new ArgumentOutOfRangeException(nameof(octaves), octaves, "Octaves are out of range.");
        }

        return 1 << (octaves - 1);
    }

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static NoiseParameters Create(
        PixelSize width, PixelSize height, Octaves octaves, CellSize cellSize, Persistence persistence) =>
        new(width, height, octaves, cellSize, persistence);
}
