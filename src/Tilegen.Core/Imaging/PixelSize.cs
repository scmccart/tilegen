namespace Tilegen.Core.Imaging;

/// <summary>A width or a height in pixels, from <see cref="Min"/> to <see cref="Max"/>.</summary>
public sealed record class PixelSize
{
    public const int Min = 1;
    public const int Max = 4096;

    internal PixelSize(int value)
    {
        if (!IsInRange(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"A pixel size is {Min} to {Max}.");
        }

        Value = value;
    }

    public int Value { get; }

    public static bool IsInRange(int value) => value is >= Min and <= Max;

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static PixelSize Create(int value) => new(value);
}
