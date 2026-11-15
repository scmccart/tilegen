using System.Collections.Immutable;
using CsCheck;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

/// <summary>
/// Properties of <see cref="Pipeline.Validate"/> over generated descriptions. Validation allocates
/// nothing per pixel, so the full parameter ranges are cheap to generate.
/// </summary>
public class PipelineValidationProperties
{
    private const int Iterations = 500;

    /// <summary>Picks the range's edges often, because uniform draws over 1 to 4096 rarely land on them.</summary>
    private static Gen<int> IntIn(int min, int max) =>
        Gen.Frequency<int>((4, Gen.Int[min, max]), (1, Gen.Const(min)), (1, Gen.Const(max)));

    /// <summary>(0, 1], with the edge 1 and the smallest positive double picked often.</summary>
    private static readonly Gen<double> InRangePersistence = Gen.Frequency<double>(
        (4, Gen.Double.Unit.Select(x => 1.0 - x)), (1, Gen.Const(1.0)), (1, Gen.Const(double.Epsilon)));

    /// <summary>
    /// A noise stage with every parameter in range and the finest octave at least one pixel, and
    /// an id that ends in its stage index, so ids in one description never repeat.
    /// </summary>
    private static Gen<NoiseDescription> InRangeStage(int index) =>
        from prefix in Gen.Char["abcXYZ09_-.é"].Array[0, 6].Select(chars => new string(chars))
        from width in IntIn(PixelSize.Min, PixelSize.Max)
        from height in IntIn(PixelSize.Min, PixelSize.Max)
        from octaves in IntIn(Octaves.Min, Octaves.Max)
        from cellSize in IntIn(NoiseParameters.SmallestValidCellSize(octaves), CellSize.Max)
        from persistence in InRangePersistence
        select new NoiseDescription(prefix + index, width, height, octaves, cellSize, persistence);

    private static readonly Gen<PipelineDescription> InRangeDescription =
        from count in Gen.Int[1, 4]
        from stages in Gen.Select(InRangeStage(0), InRangeStage(1), InRangeStage(2), InRangeStage(3))
        select new PipelineDescription(
            ImmutableArray.Create<StageDescription>(stages.Item1, stages.Item2, stages.Item3, stages.Item4)
                .RemoveRange(count, 4 - count));

    /// <summary>A parameter and a value outside its range, near the edges and far from them.</summary>
    private static readonly Gen<(Parameter Parameter, double Value)> OutOfRangeValue = Gen.OneOf<(Parameter, double)>(
        OutOfRangeInt(Parameter.Width, PixelSize.Min, PixelSize.Max),
        OutOfRangeInt(Parameter.Height, PixelSize.Min, PixelSize.Max),
        OutOfRangeInt(Parameter.Octaves, Octaves.Min, Octaves.Max),
        OutOfRangeInt(Parameter.CellSize, CellSize.Min, CellSize.Max),
        Gen.OneOf<double>(
                Gen.Double[-1e9, Persistence.ExclusiveMin],
                Gen.Double[Persistence.Max, 1e9].Where(p => p > Persistence.Max),
                Gen.OneOf(
                    Persistence.ExclusiveMin, -0.0, Math.BitIncrement(Persistence.Max),
                    double.NaN, double.PositiveInfinity, double.NegativeInfinity))
            .Select(p => (Parameter.Persistence, p)));

    private static Gen<(Parameter, double)> OutOfRangeInt(Parameter parameter, int min, int max) =>
        Gen.OneOf<int>(
                Gen.Int[int.MinValue, min - 1],
                Gen.Int[max + 1, int.MaxValue],
                Gen.OneOf(min - 1, max + 1))
            .Select(value => (parameter, (double)value));

    [Fact]
    public void An_in_range_description_always_validates_keeping_its_ids_and_values()
    {
        InRangeDescription.Sample(
            description =>
            {
                var valid = Assert.IsType<Valid>(Pipeline.Validate(description));

                Assert.Equal(description.Stages.Length, valid.Pipeline.Stages.Length);
                foreach (var (given, stage) in description.Stages.Zip(valid.Pipeline.Stages))
                {
                    var expected = Assert.IsType<NoiseDescription>(given);
                    var noise = Assert.IsType<Noise>(stage);
                    Assert.Equal(expected.Id, noise.Id.Value);
                    Assert.Equal(expected.Width, noise.Parameters.Width.Value);
                    Assert.Equal(expected.Height, noise.Parameters.Height.Value);
                    Assert.Equal(expected.Octaves, noise.Parameters.Octaves.Value);
                    Assert.Equal(expected.CellSize, noise.Parameters.CellSize.Value);
                    Assert.Equal(expected.Persistence, noise.Parameters.Persistence.Value);
                }
            },
            iter: Iterations);
    }

    /// <summary>
    /// The base stages satisfy the finest-octave rule, and pushing octaves or cell size out of
    /// range switches that rule off, so the one finding is always the pushed parameter's.
    /// </summary>
    [Fact]
    public void One_parameter_pushed_out_of_range_gives_exactly_that_out_of_range_finding()
    {
        var gen =
            from description in InRangeDescription
            from index in Gen.Int[0, description.Stages.Length - 1]
            from pushed in OutOfRangeValue
            select (description, index, pushed.Parameter, pushed.Value);

        gen.Sample(
            (description, index, parameter, value) =>
            {
                var stage = Assert.IsType<NoiseDescription>(description.Stages[index]);
                var broken = Push(stage, parameter, value);
                var mutated = description with { Stages = description.Stages.SetItem(index, broken) };

                var invalid = Assert.IsType<Invalid>(Pipeline.Validate(mutated));

                // Record equality compares the value with double.Equals, so NaN equals NaN.
                var expected = new OutOfRange(new StageRef(index, stage.Id), parameter, value, RangeOf(parameter));
                Assert.Equal(expected, Assert.Single(invalid.Findings));
            },
            iter: Iterations);
    }

    [Fact]
    public void Validating_twice_gives_equal_results_and_leaves_the_description_unchanged()
    {
        var anyStage =
            from id in Gen.OneOf("", " ", "a", "A", "b", "two words", "height")
            from width in Gen.Int[-2, 4098]
            from height in Gen.Int[-2, 4098]
            from octaves in Gen.Int[-1, 14]
            from cellSize in Gen.Int[-2, 4098]
            from persistence in Gen.OneOf<double>(
                Gen.Double[-0.5, 1.5], Gen.OneOf(0.0, 1.0, double.NaN, double.PositiveInfinity))
            select (StageDescription)new NoiseDescription(id, width, height, octaves, cellSize, persistence);
        var anyDescription = anyStage.Array[0, 4].Select(stages => new PipelineDescription([.. stages]));

        anyDescription.Sample(
            description =>
            {
                var before = description.Stages.ToArray();

                var first = Pipeline.Validate(description);
                var second = Pipeline.Validate(description);

                AssertEquivalent(first, second);
                Assert.Equal(before, description.Stages);
            },
            iter: Iterations);
    }

    private static NoiseDescription Push(NoiseDescription stage, Parameter parameter, double value) => parameter switch
    {
        Parameter.Width => stage with { Width = (int)value },
        Parameter.Height => stage with { Height = (int)value },
        Parameter.Octaves => stage with { Octaves = (int)value },
        Parameter.CellSize => stage with { CellSize = (int)value },
        Parameter.Persistence => stage with { Persistence = value },
    };

    private static ParameterRange RangeOf(Parameter parameter) => parameter switch
    {
        Parameter.Width => new(PixelSize.Min, true, PixelSize.Max),
        Parameter.Height => new(PixelSize.Min, true, PixelSize.Max),
        Parameter.Octaves => new(Octaves.Min, true, Octaves.Max),
        Parameter.CellSize => new(CellSize.Min, true, CellSize.Max),
        Parameter.Persistence => new(Persistence.ExclusiveMin, false, Persistence.Max),
    };

    /// <summary>
    /// <see cref="ValidatedPipeline"/> and the <see cref="ImmutableArray{T}"/> fields compare by
    /// reference, so the outcomes are compared element by element.
    /// </summary>
    private static void AssertEquivalent(ValidationOutcome first, ValidationOutcome second)
    {
        var same = (first, second) switch
        {
            (Valid a, Valid b) => a.Pipeline.Stages.SequenceEqual(b.Pipeline.Stages),
            (Invalid a, Invalid b) => a.Findings.SequenceEqual(b.Findings),
            (Valid, Invalid) => false,
            (Invalid, Valid) => false,
        };

        Assert.True(same, $"The two outcomes differ: {first} and {second}.");
    }
}
