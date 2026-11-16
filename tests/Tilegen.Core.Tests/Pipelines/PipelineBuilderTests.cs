using Tilegen.Core.Pipelines;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

public class PipelineBuilderTests
{
    private static NoiseDescription Noise(string id) => new(id, 16, 16, 2, 8, 0.5);

    [Fact]
    public void Add_returns_handles_at_increasing_positions_with_the_ids_given()
    {
        var builder = new PipelineBuilder();

        var first = builder.Add(Noise("a"));
        var second = builder.Add(Noise("b"));
        var third = builder.Add(Noise("c"));

        Assert.Equal(
            [(0, "a"), (1, "b"), (2, "c")],
            [(first.Index, first.Id), (second.Index, second.Id), (third.Index, third.Id)]);
    }

    [Fact]
    public void A_stage_added_with_a_null_id_gets_a_handle_with_an_empty_id()
    {
        var builder = new PipelineBuilder();

        var handle = builder.Add(Noise(null!));

        Assert.Equal(0, handle.Index);
        Assert.Equal("", handle.Id);
    }

    [Fact]
    public void A_stage_added_with_an_invalid_id_gets_a_handle_with_that_id_as_written()
    {
        var builder = new PipelineBuilder();

        var handle = builder.Add(Noise("not valid"));

        Assert.Equal("not valid", handle.Id);
    }

    [Fact]
    public void Build_returns_the_stages_in_the_order_they_were_added()
    {
        var builder = new PipelineBuilder();
        var b = Noise("b");
        var a = Noise("a");
        var c = Noise("c");
        builder.Add(b);
        builder.Add(a);
        builder.Add(c);

        var description = builder.Build();

        Assert.Equal<StageDescription>([b, a, c], description.Stages);
    }

    [Fact]
    public void A_later_add_does_not_change_a_description_already_built()
    {
        var builder = new PipelineBuilder();
        var a = Noise("a");
        builder.Add(a);
        var built = builder.Build();

        builder.Add(Noise("b"));
        var rebuilt = builder.Build();

        Assert.Equal<StageDescription>([a], built.Stages);
        Assert.Equal(["a", "b"], rebuilt.Stages.Select(stage => stage.Id));
    }

    [Fact]
    public void Building_twice_gives_equal_stages_and_leaves_the_first_description_unchanged()
    {
        var builder = new PipelineBuilder();
        var a = Noise("a");
        builder.Add(a);

        var first = builder.Build();
        var second = builder.Build();

        Assert.Equal<StageDescription>([a], first.Stages);
        Assert.Equal<StageDescription>(first.Stages, second.Stages);
    }

    [Fact]
    public void An_empty_builder_builds_an_empty_description()
    {
        var description = new PipelineBuilder().Build();

        Assert.False(description.Stages.IsDefault);
        Assert.Empty(description.Stages);
    }

    [Fact]
    public void Handles_with_the_same_position_and_id_are_equal()
    {
        var first = new PipelineBuilder().Add(Noise("a"));
        var second = new PipelineBuilder().Add(Noise("a"));

        Assert.Equal(first, second);
        Assert.NotEqual(first, new PipelineBuilder().Add(Noise("b")));
    }
}
