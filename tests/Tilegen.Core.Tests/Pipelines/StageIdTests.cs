using Tilegen.Core.Pipelines;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

public class StageIdTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("grass-noise")]
    [InlineData("terrain_2")]
    [InlineData("héllo")]
    public void A_non_empty_id_without_whitespace_is_accepted(string text)
    {
        Assert.Equal(text, StageId.Create(text).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a b")]
    [InlineData(" a")]
    [InlineData("a ")]
    [InlineData("a\tb")]
    [InlineData("a\nb")]
    [InlineData("a b")]
    public void An_empty_id_or_one_with_whitespace_is_refused(string text)
    {
        Assert.Throws<ArgumentException>(() => StageId.Create(text));
    }

    [Fact]
    public void A_null_id_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => StageId.Create(null!));
    }

    [Fact]
    public void Ids_with_the_same_text_are_equal()
    {
        Assert.Equal(StageId.Create("x"), StageId.Create("x"));
        Assert.NotEqual(StageId.Create("x"), StageId.Create("y"));
    }
}
