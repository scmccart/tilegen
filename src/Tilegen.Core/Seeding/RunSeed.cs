namespace Tilegen.Core.Seeding;

/// <summary>The seed of a whole run. Every 32-bit signed whole number is valid.</summary>
public readonly record struct RunSeed(int Value)
{
    /// <summary>
    /// The seed after this one. <see cref="int.MaxValue"/> wraps to <see cref="int.MinValue"/>:
    /// every value is a valid seed, so wrapping loses nothing.
    /// </summary>
    public RunSeed Next() => new(unchecked(Value + 1));
}
