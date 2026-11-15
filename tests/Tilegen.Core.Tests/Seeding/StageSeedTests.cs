using System.Collections.Immutable;
using System.Globalization;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Seeding;
using Xunit;

namespace Tilegen.Core.Tests.Seeding;

public class StageSeedTests
{
    private static StageSeed Derive(int runSeed, string id) =>
        StageSeed.Derive(new RunSeed(runSeed), StageId.Create(id));

    // Each row was computed once, by this derivation and independently by a separate scratch program
    // that follows docs/design/stage-seeds.md. A change here means every seeded stage's output changes.
    [Theory]
    [InlineData(0, "height", 2088840183)]
    [InlineData(1, "height", 111902220)]
    [InlineData(-1, "height", 1701028560)]
    [InlineData(42, "height", 575862152)]
    [InlineData(42, "moisture", 398336233)]
    [InlineData(1, "moisture", 506649416)]
    [InlineData(int.MaxValue, "moisture", 688311321)]
    [InlineData(int.MaxValue, "a", 180471320)]
    [InlineData(0, "höhe", 884408310)]
    [InlineData(-1, "höhe", 1868809810)]
    public void A_pinned_seed_holds(int runSeed, string id, int expected)
    {
        Assert.Equal(expected, Derive(runSeed, id).Value);
    }

    [Fact]
    public void The_same_inputs_give_the_same_seed()
    {
        Assert.Equal(Derive(42, "height"), Derive(42, "height"));
    }

    [Fact]
    public void Different_ids_give_different_seeds()
    {
        Assert.NotEqual(Derive(42, "height"), Derive(42, "moisture"));
    }

    [Fact]
    public void Different_run_seeds_give_different_seeds()
    {
        Assert.NotEqual(Derive(1, "height"), Derive(2, "height"));
    }

    [Fact]
    public void Adding_a_stage_leaves_the_other_seeds_alone()
    {
        var runSeed = new RunSeed(42);
        var before = Describe("a", "b");
        var after = Describe("c", "a", "b");

        var seedsBefore = SeedsById(runSeed, before);
        var seedsAfter = SeedsById(runSeed, after);

        Assert.Equal(seedsBefore["a"], seedsAfter["a"]);
        Assert.Equal(seedsBefore["b"], seedsAfter["b"]);
    }

    [Fact]
    public void Culture_does_not_change_a_seed()
    {
        const string id = "höhe-ITEM-ışık";
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariant = Derive(42, id);

            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var turkish = Derive(42, id);

            Assert.Equal(invariant, turkish);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// Why the seed is masked non-negative: <c>new Random(s)</c> and <c>new Random(-s)</c> give the same
    /// sequence. This is a fact about the runtime that the design relies on, so it is checked here.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(12345)]
    [InlineData(int.MaxValue)]
    public void A_negated_random_seed_gives_the_same_sequence(int seed)
    {
        var positive = new Random(seed);
        var negated = new Random(-seed);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(positive.Next(), negated.Next());
        }
    }

    private static PipelineDescription Describe(params string[] ids) =>
        new(ids.Select(id => (StageDescription)new NoiseDescription(id, 8, 8, 1, 4, 0.5)).ToImmutableArray());

    private static Dictionary<string, StageSeed> SeedsById(RunSeed runSeed, PipelineDescription description) =>
        description.Stages
            .Cast<NoiseDescription>()
            .ToDictionary(stage => stage.Id, stage => StageSeed.Derive(runSeed, StageId.Create(stage.Id)));
}
