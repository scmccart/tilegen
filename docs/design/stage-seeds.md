# Stage seeds

## What it does

Gives each stage of a run its own seed, derived from the run seed and the stage's id. The engine seeds the stage's `System.Random` with it (`new Random(seed.Value)`) and passes that `Random` to the technique. Core never constructs a `Random` itself.

A stage's seed depends on the run seed and the stage's id and nothing else: not its position in the pipeline, not the other stages, not its parameters. Adding, removing or reordering other stages leaves it unchanged. The same run seed and id give the same seed in every process, on every launch and under every culture.

## How it works

`StageSeed.Derive(RunSeed, StageId)` does four things:

1. **Bytes.** It builds one byte sequence: the run seed as 4 bytes, little-endian whatever the platform (`BinaryPrimitives.WriteInt32LittleEndian`), followed by the stage id's UTF-8 bytes (`Encoding.UTF8`). No separator is needed, because the first part always has a fixed length of four bytes.
2. **Hash.** It hashes the bytes with 64-bit FNV-1a. Start with `h = 0xcbf29ce484222325` (the offset basis). For each byte, `h = (h ^ byte) * 0x100000001b3` (the prime), modulo 2^64.
3. **Fold.** `h ^ (h >> 32)` xors the high half of the 64-bit hash into the low half.
4. **Mask.** The result is masked with `0x7FFF_FFFF` and cast to `int`. The seed is therefore never negative, so it has 31 bits.

The mask is there because `new Random(s)` and `new Random(-s)` give the same sequence. Checked on .NET 11.0.0 for 1, 2, 42, 12345, 1,000,000 and `int.MaxValue` (each against its negation), and for `int.MinValue` against `int.MaxValue`: the first 1000 values of `Next()` and `NextDouble()` were identical in every pair. If seeds could be negative, two derivations could differ and still produce the same stage output. `StageSeedTests.A_negated_random_seed_gives_the_same_sequence` keeps that fact checked.

`RunSeed` is a `readonly record struct` around an `int`, and every `int` is a valid run seed. `StageSeed` has a private constructor, so the only way to get one is `Derive`, which makes "non-negative" true by construction.

## Inputs

There are no parameters. The inputs are the run seed and the stage id.

## Sources

- FNV hash specification: G. Fowler, L. Noll, K. Vo, D. Eastlake, T. Hansen, "The FNV Non-Cryptographic Hash Algorithm", Internet-Draft draft-eastlake-fnv-21 (January 2024), https://www.ietf.org/archive/id/draft-eastlake-fnv-21.txt, and Landon Curt Noll's FNV page, https://www.isthe.com/chongo/tech/comp/fnv/. The draft gives the FNV-1a pseudo-code (xor the byte, then multiply by the prime), the 64-bit offset basis `0xCBF29CE484222325` and the 64-bit prime `0x100000001B3`. The same constants appear in `hash_64a.c` of the reference implementation at https://github.com/lcn2/fnv.
- XOR folding: section 3 of the draft, "Other Hash Sizes and XOR Folding", describes making a k-bit hash from a larger FNV as `(temp xor temp>>k) bitwise-and (2**k - 1)`.
- Test vectors: `fnv1a_64_vector` in `test_fnv.c`, https://raw.githubusercontent.com/lcn2/fnv/master/test_fnv.c (https://www.isthe.com/chongo/src/fnv/test_fnv.c redirects there). Checked from that file: `""` gives `0xcbf29ce484222325`, `"a"` gives `0xaf63dc4c8601ec8c`, `"b"` gives `0xaf63df4c8601f1a5`, `"fo"` gives `0x08985907b541d342`, `"foo"` gives `0xdcb27518fed9d577`, and `"foobar"` gives `0x85944171f73967e8`.
- No code was copied. The function is a few lines written from the specification.

The fold and the mask follow the draft's xor folding, with one difference: the draft shifts by k, which here would be 31, and tilegen shifts by 32 so that the fold brings the whole high half into the low half before the mask keeps 31 bits. The test vectors cover only the 64-bit hash, not the fold and mask; the pinned table covers those.

## Decisions and limits

- **Why FNV-1a.** It is a published hash with published test vectors, it has no per-process randomization and no culture, and it is a few lines. It is not an RNG, so tilegen still uses the framework's `Random` for randomness.
- **Alternatives rejected:**
  - `HashCode.Combine` and `string.GetHashCode`: randomized per process, so a seed would change on every launch.
  - The stage's position in the pipeline: inserting a stage would change every seed after it.
  - SHA-256 from `System.Security.Cryptography`: more machinery than needed, and it brings a cryptography namespace into Core for no gain.
  - A SplitMix-style finalizer: a better mixer, but it edges towards writing an RNG.
- **Collisions are possible in principle.** A seed has 31 bits, so two different (run seed, id) pairs can give the same seed. Two stages in one run whose seeds collide would draw the same sequence. With a handful of stages per pipeline that is very unlikely, and nothing checks for it.
- **Stable only for the same `Random` implementation.** The seed is stable everywhere, but the sequence `new Random(seed)` produces is documented only for the same .NET version and platform. A runtime that changes the seeded algorithm changes every stage's output. Golden provenance records the .NET version for that reason.
- **Fixed byte layout.** Changing the layout, the fold or the mask changes every seed, and so every golden. The pinned table in the tests exists to catch that.

## Where it lives

- `src/Tilegen.Core/Seeding/RunSeed.cs`
- `src/Tilegen.Core/Seeding/StageSeed.cs`: `StageSeed` and `Derive`.
- `src/Tilegen.Core/Seeding/Fnv1a64.cs`: the hash, internal, so tests can check it against the published vectors.
- `tests/Tilegen.Core.Tests/Seeding/Fnv1a64Tests.cs`: the published vectors and the two constants.
- `tests/Tilegen.Core.Tests/Seeding/StageSeedTests.cs`: a pinned table of ten (run seed, id) pairs, including a negative run seed, `int.MaxValue` and a non-ASCII id; same inputs; different ids; different run seeds; independence from other stages; the culture check under the invariant culture and tr-TR; and the `Random(s)` against `Random(-s)` fact.
- `tests/Tilegen.Core.Tests/Seeding/StageSeedProperties.cs`: a property that every derived seed is non-negative.
