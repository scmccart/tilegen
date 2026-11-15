using System.Buffers.Binary;
using System.Text;
using Tilegen.Core.Pipelines;

namespace Tilegen.Core.Seeding;

/// <summary>
/// The seed of one stage's <see cref="System.Random"/>, derived from the run seed and the stage's id.
/// Never negative: <c>new Random(s)</c> and <c>new Random(-s)</c> give the same sequence, so a negative
/// seed would let two derivations collide.
/// </summary>
public readonly record struct StageSeed
{
    private const ulong NonNegativeMask = 0x7FFF_FFFFUL;

    private StageSeed(int value) => Value = value;

    public int Value { get; }

    /// <summary>
    /// FNV-1a 64 over the run seed's four bytes (little-endian) followed by the id's UTF-8 bytes, then
    /// <c>h ^ (h >> 32)</c> masked to 31 bits. A function of the run seed and the id alone.
    /// </summary>
    public static StageSeed Derive(RunSeed runSeed, StageId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        var idByteCount = Encoding.UTF8.GetByteCount(id.Value);
        var bytes = new byte[sizeof(int) + idByteCount];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, runSeed.Value);
        Encoding.UTF8.GetBytes(id.Value, bytes.AsSpan(sizeof(int)));

        var hash = Fnv1a64.Hash(bytes);
        return new StageSeed((int)((hash ^ (hash >> 32)) & NonNegativeMask));
    }
}
