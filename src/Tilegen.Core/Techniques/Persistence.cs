namespace Tilegen.Core.Techniques;

/// <summary>
/// The factor by which each noise octave's amplitude is multiplied: finite, greater than
/// <see cref="ExclusiveMin"/> and at most <see cref="Max"/>.
/// </summary>
public sealed record class Persistence
{
    /// <summary>The lower bound, which is not itself allowed.</summary>
    public const double ExclusiveMin = 0.0;

    public const double Max = 1.0;

    internal Persistence(double value)
    {
        if (!IsInRange(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"Persistence is above {ExclusiveMin} and at most {Max}, and finite.");
        }

        Value = value;
    }

    public double Value { get; }

    public static bool IsInRange(double value) => double.IsFinite(value) && value > ExclusiveMin && value <= Max;

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static Persistence Create(double value) => new(value);
}
