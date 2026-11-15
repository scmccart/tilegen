using Tilegen.Core.Imaging;
using Tilegen.Core.Techniques;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

public class NoiseWrapperTests
{
    [Fact]
    public void The_ranges_are_published_as_constants()
    {
        Assert.Equal(1, PixelSize.Min);
        Assert.Equal(4096, PixelSize.Max);
        Assert.Equal(1, Octaves.Min);
        Assert.Equal(12, Octaves.Max);
        Assert.Equal(1, CellSize.Min);
        Assert.Equal(4096, CellSize.Max);
        Assert.Equal(0.0, Persistence.ExclusiveMin);
        Assert.Equal(1.0, Persistence.Max);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4096)]
    public void A_pixel_size_inside_its_range_is_accepted(int value)
    {
        Assert.Equal(value, PixelSize.Create(value).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4097)]
    [InlineData(int.MaxValue)]
    public void A_pixel_size_outside_its_range_is_refused(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelSize.Create(value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void Octaves_inside_the_range_are_accepted(int value)
    {
        Assert.Equal(value, Octaves.Create(value).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    public void Octaves_outside_the_range_are_refused(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Octaves.Create(value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    public void A_cell_size_inside_its_range_is_accepted(int value)
    {
        Assert.Equal(value, CellSize.Create(value).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4097)]
    public void A_cell_size_outside_its_range_is_refused(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CellSize.Create(value));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(double.Epsilon)]
    public void A_persistence_above_zero_and_at_most_one_is_accepted(double value)
    {
        Assert.Equal(value, Persistence.Create(value).Value);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.0000000000000002)]
    [InlineData(2.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_persistence_outside_its_range_or_not_finite_is_refused(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Persistence.Create(value));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 8)]
    [InlineData(12, 2048)]
    public void The_smallest_valid_cell_size_is_two_to_the_octaves_minus_one(int octaves, int expected)
    {
        Assert.Equal(expected, NoiseParameters.SmallestValidCellSize(octaves));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void The_smallest_valid_cell_size_is_defined_only_for_octaves_in_range(int octaves)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NoiseParameters.SmallestValidCellSize(octaves));
    }

    [Fact]
    public void Noise_parameters_keep_the_wrappers_they_were_built_from()
    {
        var parameters = NoiseParameters.Create(
            PixelSize.Create(64), PixelSize.Create(32), Octaves.Create(4), CellSize.Create(16), Persistence.Create(0.5));

        Assert.Equal(64, parameters.Width.Value);
        Assert.Equal(32, parameters.Height.Value);
        Assert.Equal(4, parameters.Octaves.Value);
        Assert.Equal(16, parameters.CellSize.Value);
        Assert.Equal(0.5, parameters.Persistence.Value);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 8)]
    [InlineData(4, 4096)]
    [InlineData(12, 2048)]
    [InlineData(12, 4096)]
    public void Noise_parameters_accept_a_cell_size_at_or_above_the_smallest_valid(int octaves, int cellSize)
    {
        var parameters = NoiseParameters.Create(
            PixelSize.Create(8), PixelSize.Create(8), Octaves.Create(octaves), CellSize.Create(cellSize), Persistence.Create(1.0));

        Assert.Equal(cellSize, parameters.CellSize.Value);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 7)]
    [InlineData(12, 2047)]
    public void Noise_parameters_refuse_a_cell_size_below_the_smallest_valid(int octaves, int cellSize)
    {
        Assert.Throws<ArgumentException>(() => NoiseParameters.Create(
            PixelSize.Create(8), PixelSize.Create(8), Octaves.Create(octaves), CellSize.Create(cellSize), Persistence.Create(1.0)));
    }

    [Fact]
    public void Noise_parameters_refuse_a_missing_wrapper()
    {
        Assert.Throws<ArgumentNullException>(() => NoiseParameters.Create(
            null!, PixelSize.Create(8), Octaves.Create(1), CellSize.Create(1), Persistence.Create(1.0)));
    }
}
