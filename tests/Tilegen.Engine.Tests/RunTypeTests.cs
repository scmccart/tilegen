using System.Reflection;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Xunit;

namespace Tilegen.Engine.Tests;

/// <summary>
/// The run's types. The switch below has no discard arm, so it compiles only while every
/// <see cref="RunState"/> case is named (CS8509 is an error): a new case breaks this file's build
/// until it is handled. The timeline is checked for any public way to change its entries.
/// </summary>
public class RunTypeTests
{
    private static string Name(RunState state) => state switch
    {
        Running => nameof(Running),
        Completed => nameof(Completed),
        Stopped => nameof(Stopped),
    };

    [Fact]
    public void A_switch_over_run_states_needs_no_discard_arm()
    {
        Assert.Equal("Running", Name(new Running()));
        Assert.Equal("Completed", Name(new Completed()));
        Assert.Equal("Stopped", Name(new Stopped()));
    }

    [Fact]
    public void A_timeline_can_be_counted_and_indexed_but_has_no_public_way_to_change_its_entries()
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var members = typeof(Timeline).GetMembers(Declared).Select(member => member.Name).Order(StringComparer.Ordinal);
        var indexer = typeof(Timeline).GetProperty("Item");

        // No public constructor, no method beyond the two getters, and no collection interface
        // whose explicit implementation could add, remove or replace an entry.
        Assert.Equal(["Count", "Item", "get_Count", "get_Item"], members);
        Assert.NotNull(indexer);
        Assert.False(indexer.CanWrite);
        Assert.Empty(typeof(Timeline).GetInterfaces());
    }

    [Fact]
    public void A_timeline_entry_counts_its_step_from_1()
    {
        var stage = StageId.Create("a");
        var frame = new Frame(new PixelBuffer(1, 1));

        var first = new TimelineEntry(stage, 1, frame);

        Assert.Equal(1, first.Step);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineEntry(stage, 0, frame));
    }
}
