using CsCheck;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Seeding;
using Xunit;

namespace Tilegen.Core.Tests.Seeding;

public class StageSeedProperties
{
    /// <summary>Any non-whitespace characters, lone surrogates included, so the UTF-8 step sees odd input.</summary>
    private static readonly Gen<string> Ids =
        Gen.Char.Where(c => !char.IsWhiteSpace(c)).Array[1, 12].Select(chars => new string(chars));

    [Fact]
    public void Every_derived_seed_is_non_negative()
    {
        Gen.Select(Gen.Int, Ids).Sample(
            (runSeed, id) =>
            {
                Assert.True(StageSeed.Derive(new RunSeed(runSeed), StageId.Create(id)).Value >= 0);
            },
            iter: 2000);
    }
}
