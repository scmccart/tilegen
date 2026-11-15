namespace Tilegen.Core.Seeding;

/// <summary>The seed of a whole run. Every 32-bit signed whole number is valid.</summary>
public readonly record struct RunSeed(int Value);
