namespace Tilegen.Core.Techniques;

/// <summary>
/// How many octaves a noise stage sums, from <see cref="Min"/> to <see cref="Max"/>. With
/// lacunarity 2, the finest-octave rule also needs a cell size of at least 2^(octaves - 1).
/// </summary>
public sealed record class Octaves
{
    public const int Min = 1;
    public const int Max = 12;

    internal Octaves(int value)
    {
        if (!IsInRange(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Octaves are {Min} to {Max}.");
        }

        Value = value;
    }

    public int Value { get; }

    public static bool IsInRange(int value) => value is >= Min and <= Max;

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static Octaves Create(int value) => new(value);
}
