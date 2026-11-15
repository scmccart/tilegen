using Tilegen.Core.Seeding;
using Xunit;

namespace Tilegen.Core.Tests.Seeding;

public class RunSeedTests
{
    [Fact]
    public void The_next_seed_is_one_more()
    {
        Assert.Equal(new RunSeed(43), new RunSeed(42).Next());
    }

    [Fact]
    public void The_next_seed_after_minus_one_is_zero()
    {
        Assert.Equal(new RunSeed(0), new RunSeed(-1).Next());
    }

    [Fact]
    public void The_next_seed_after_the_largest_wraps_to_the_smallest()
    {
        Assert.Equal(new RunSeed(int.MinValue), new RunSeed(int.MaxValue).Next());
    }
}
