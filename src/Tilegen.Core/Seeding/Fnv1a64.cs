namespace Tilegen.Core.Seeding;

/// <summary>
/// The 64-bit FNV-1a hash, as published by Fowler, Noll and Vo: start from the offset basis, and for
/// each byte xor it in and multiply by the prime, modulo 2^64.
/// </summary>
internal static class Fnv1a64
{
    internal const ulong OffsetBasis = 0xcbf29ce484222325UL;
    internal const ulong Prime = 0x100000001b3UL;

    internal static ulong Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = OffsetBasis;
        foreach (var b in bytes)
        {
            hash = unchecked((hash ^ b) * Prime);
        }

        return hash;
    }
}
