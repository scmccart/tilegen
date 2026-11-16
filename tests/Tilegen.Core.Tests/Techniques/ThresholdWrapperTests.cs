using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

public class ThresholdWrapperTests
{
    [Fact]
    public void The_level_range_is_published_as_constants()
    {
        Assert.Equal(0, ThresholdLevel.Min);
        Assert.Equal(255, ThresholdLevel.Max);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(255)]
    public void A_level_inside_its_range_is_accepted(int value)
    {
        Assert.True(ThresholdLevel.IsInRange(value));
        Assert.Equal(value, ThresholdLevel.Create(value).Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void A_level_outside_its_range_is_refused(int value)
    {
        Assert.False(ThresholdLevel.IsInRange(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThresholdLevel.Create(value));
    }

    [Fact]
    public void Threshold_parameters_keep_the_source_and_level_they_were_built_from()
    {
        var parameters = ThresholdParameters.Create(StageId.Create("height"), ThresholdLevel.Create(128));

        Assert.Equal(StageId.Create("height"), parameters.Source);
        Assert.Equal(128, parameters.Level.Value);
    }

    [Fact]
    public void Threshold_parameters_refuse_a_missing_source()
    {
        Assert.Throws<ArgumentNullException>(() => ThresholdParameters.Create(null!, ThresholdLevel.Create(128)));
    }

    [Fact]
    public void Threshold_parameters_refuse_a_missing_level()
    {
        Assert.Throws<ArgumentNullException>(() => ThresholdParameters.Create(StageId.Create("height"), null!));
    }

    [Fact]
    public void Threshold_parameters_with_the_same_source_and_level_are_equal()
    {
        var first = ThresholdParameters.Create(StageId.Create("height"), ThresholdLevel.Create(128));
        var second = ThresholdParameters.Create(StageId.Create("height"), ThresholdLevel.Create(128));

        Assert.Equal(first, second);
        Assert.NotEqual(first, ThresholdParameters.Create(StageId.Create("height"), ThresholdLevel.Create(127)));
        Assert.NotEqual(first, ThresholdParameters.Create(StageId.Create("other"), ThresholdLevel.Create(128)));
    }
}
