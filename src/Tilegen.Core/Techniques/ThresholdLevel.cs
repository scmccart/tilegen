namespace Tilegen.Core.Techniques;

/// <summary>
/// The brightness at or above which a threshold stage writes terrain 1, from <see cref="Min"/> to
/// <see cref="Max"/>: the range of a pixel's brightness, the mean of its 8-bit channels.
/// </summary>
public sealed record class ThresholdLevel
{
    public const int Min = 0;
    public const int Max = 255;

    internal ThresholdLevel(int value)
    {
        if (!IsInRange(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"A threshold level is {Min} to {Max}.");
        }

        Value = value;
    }

    public int Value { get; }

    public static bool IsInRange(int value) => value is >= Min and <= Max;

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static ThresholdLevel Create(int value) => new(value);
}
