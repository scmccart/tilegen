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
/// <para>
/// Descriptions are built with a <see cref="PipelineBuilder"/>, so a threshold stage reads an
/// earlier stage through the handle that adding it returned. The first stage is always noise, and
/// each later one is noise or a threshold reading any stage before it, threshold or not.
/// </para>
/// </summary>
public class PipelineValidationProperties
{
    private const int Iterations = 500;

    /// <summary>Makes one stage, given the handles of the stages added before it.</summary>
    private delegate StageDescription StageAt(IReadOnlyList<StageHandle> earlier);

    /// <summary>Picks the range's edges often, because uniform draws over 1 to 4096 rarely land on them.</summary>
    private static Gen<int> IntIn(int min, int max) =>
        Gen.Frequency<int>((4, Gen.Int[min, max]), (1, Gen.Const(min)), (1, Gen.Const(max)));

    /// <summary>(0, 1], with the edge 1 and the smallest positive double picked often.</summary>
    private static readonly Gen<double> InRangePersistence = Gen.Frequency<double>(
        (4, Gen.Double.Unit.Select(x => 1.0 - x)), (1, Gen.Const(1.0)), (1, Gen.Const(double.Epsilon)));

    /// <summary>A valid id that ends in its stage index, so ids in one description never repeat.</summary>
    private static Gen<string> IdEndingIn(int index) =>
        Gen.Char["abcXYZ09_-.é"].Array[0, 6].Select(chars => new string(chars) + index);

    /// <summary>A noise stage with every parameter in range and the finest octave at least one pixel.</summary>
    private static Gen<NoiseDescription> InRangeNoise(int index) =>
        from id in IdEndingIn(index)
        from width in IntIn(PixelSize.Min, PixelSize.Max)
        from height in IntIn(PixelSize.Min, PixelSize.Max)
        from octaves in IntIn(Octaves.Min, Octaves.Max)
        from cellSize in IntIn(NoiseParameters.SmallestValidCellSize(octaves), CellSize.Max)
        from persistence in InRangePersistence
        select new NoiseDescription(id, width, height, octaves, cellSize, persistence);

    /// <summary>An in-range stage at <paramref name="index"/>: noise, or from index 1 a threshold reading an earlier stage.</summary>
    private static Gen<StageAt> InRangeStage(int index)
    {
        var noise = InRangeNoise(index).Select(stage => (StageAt)(_ => stage));
        if (index == 0)
        {
            return noise;
        }

        var threshold =
            from id in IdEndingIn(index)
            from source in Gen.Int[0, index - 1]
            from level in IntIn(ThresholdLevel.Min, ThresholdLevel.Max)
            select (StageAt)(earlier => new ThresholdDescription(id, earlier[source], level));
        return Gen.OneOf(noise, threshold);
    }

    private static readonly Gen<PipelineDescription> InRangeDescription =
        from count in Gen.Int[1, 4]
        from stages in Gen.Select(InRangeStage(0), InRangeStage(1), InRangeStage(2), InRangeStage(3))
        select Build(new[] { stages.Item1, stages.Item2, stages.Item3, stages.Item4 }.Take(count));

    /// <summary>Any value outside a whole-number range, near the edges and far from them.</summary>
    private static Gen<int> OutOfRangeInt(int min, int max) =>
        Gen.OneOf<int>(Gen.Int[int.MinValue, min - 1], Gen.Int[max + 1, int.MaxValue], Gen.OneOf(min - 1, max + 1));

    private static readonly Gen<double> OutOfRangePersistence = Gen.OneOf<double>(
        Gen.Double[-1e9, Persistence.ExclusiveMin],
        Gen.Double[Persistence.Max, 1e9].Where(p => p > Persistence.Max),
        Gen.OneOf(
            Persistence.ExclusiveMin, -0.0, Math.BitIncrement(Persistence.Max),
            double.NaN, double.PositiveInfinity, double.NegativeInfinity));

    /// <summary>
    /// <paramref name="stage"/> with one of its parameters pushed out of range, and which parameter
    /// and value. The switch has no discard arm, so a new stage kind doesn't compile until it can be
    /// pushed here too.
    /// </summary>
    private static Gen<(StageDescription Broken, Parameter Parameter, double Value)> PushedOutOfRange(
        StageDescription stage) => stage switch
    {
        NoiseDescription noise => Gen.OneOf(
            OutOfRangeInt(PixelSize.Min, PixelSize.Max).Select(v => Pushed(noise with { Width = v }, Parameter.Width, v)),
            OutOfRangeInt(PixelSize.Min, PixelSize.Max).Select(v => Pushed(noise with { Height = v }, Parameter.Height, v)),
            OutOfRangeInt(Octaves.Min, Octaves.Max).Select(v => Pushed(noise with { Octaves = v }, Parameter.Octaves, v)),
            OutOfRangeInt(CellSize.Min, CellSize.Max).Select(v => Pushed(noise with { CellSize = v }, Parameter.CellSize, v)),
            OutOfRangePersistence.Select(p => Pushed(noise with { Persistence = p }, Parameter.Persistence, p))),
        ThresholdDescription threshold => OutOfRangeInt(ThresholdLevel.Min, ThresholdLevel.Max)
            .Select(v => Pushed(threshold with { Level = v }, Parameter.Level, v)),
    };

    private static (StageDescription Broken, Parameter Parameter, double Value) Pushed(
        StageDescription broken, Parameter parameter, double value) => (broken, parameter, value);

    [Fact]
    public void An_in_range_description_always_validates_keeping_its_ids_values_and_sources()
    {
        InRangeDescription.Sample(
            description =>
            {
                var valid = Assert.IsType<Valid>(Pipeline.Validate(description));

                Assert.Equal(description.Stages.Length, valid.Pipeline.Stages.Length);
                foreach (var (given, stage) in description.Stages.Zip(valid.Pipeline.Stages))
                {
                    Action check = given switch
                    {
                        NoiseDescription noise => () => AssertKept(noise, stage),
                        ThresholdDescription threshold => () => AssertKept(threshold, stage, valid.Pipeline),
                    };
                    check();
                }
            },
            iter: Iterations);
    }

    /// <summary>
    /// The base stages satisfy the finest-octave rule, and pushing octaves or cell size out of
    /// range switches that rule off. A pushed stage may be a threshold's source, which still
    /// resolves and adds no finding, because a source's own findings are reported once, against
    /// it. So the one finding is always the pushed parameter's.
    /// </summary>
    [Fact]
    public void One_parameter_pushed_out_of_range_gives_exactly_that_out_of_range_finding()
    {
        var gen =
            from description in InRangeDescription
            from index in Gen.Int[0, description.Stages.Length - 1]
            from pushed in PushedOutOfRange(description.Stages[index])
            select (description with { Stages = description.Stages.SetItem(index, pushed.Broken) },
                index, pushed.Parameter, pushed.Value);

        gen.Sample(
            (mutated, index, parameter, value) =>
            {
                var invalid = Assert.IsType<Invalid>(Pipeline.Validate(mutated));

                // Record equality compares the value with double.Equals, so NaN equals NaN.
                var expected = new OutOfRange(
                    new StageRef(index, mutated.Stages[index].Id), parameter, value, RangeOf(parameter));
                Assert.Equal(expected, Assert.Single(invalid.Findings));
            },
            iter: Iterations);
    }

    [Fact]
    public void Validating_twice_gives_equal_results_and_leaves_the_description_unchanged()
    {
        var anyId = Gen.OneOf("", " ", "a", "A", "b", "two words", "height");
        var anyNoise =
            from id in anyId
            from width in Gen.Int[-2, 4098]
            from height in Gen.Int[-2, 4098]
            from octaves in Gen.Int[-1, 14]
            from cellSize in Gen.Int[-2, 4098]
            from persistence in Gen.OneOf<double>(
                Gen.Double[-0.5, 1.5], Gen.OneOf(0.0, 1.0, double.NaN, double.PositiveInfinity))
            select (StageAt)(_ => new NoiseDescription(id, width, height, octaves, cellSize, persistence));
        var anyThreshold =
            from id in anyId
            from source in Gen.Int[0, 3]
            from level in Gen.Int[-2, 258]
            select (StageAt)(earlier => new ThresholdDescription(id, earlier[source % earlier.Count], level));
        var anyLaterStage = Gen.OneOf(anyNoise, anyThreshold);
        var anyDescription =
            from count in Gen.Int[0, 4]
            from later in anyLaterStage.Array[3]
            from first in anyNoise
            select Build(new[] { first }.Concat(later).Take(count));

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

    /// <summary>Adds each stage in order, giving it the handles of the stages added before it.</summary>
    private static PipelineDescription Build(IEnumerable<StageAt> stages)
    {
        var builder = new PipelineBuilder();
        var handles = new List<StageHandle>();
        foreach (var stage in stages)
        {
            handles.Add(builder.Add(stage(handles)));
        }

        return builder.Build();
    }

    private static void AssertKept(NoiseDescription expected, Stage stage)
    {
        var noise = Assert.IsType<Noise>(stage);
        Assert.Equal(expected.Id, noise.Id.Value);
        Assert.Equal(expected.Width, noise.Parameters.Width.Value);
        Assert.Equal(expected.Height, noise.Parameters.Height.Value);
        Assert.Equal(expected.Octaves, noise.Parameters.Octaves.Value);
        Assert.Equal(expected.CellSize, noise.Parameters.CellSize.Value);
        Assert.Equal(expected.Persistence, noise.Parameters.Persistence.Value);
    }

    /// <summary>The threshold reads the validated stage at the position its handle named.</summary>
    private static void AssertKept(ThresholdDescription expected, Stage stage, ValidatedPipeline pipeline)
    {
        var threshold = Assert.IsType<Threshold>(stage);
        Assert.Equal(expected.Id, threshold.Id.Value);
        Assert.Equal(pipeline.Stages[expected.Source.Index].Id, threshold.Parameters.Source);
        Assert.Equal(expected.Level, threshold.Parameters.Level.Value);
    }

    private static ParameterRange RangeOf(Parameter parameter) => parameter switch
    {
        Parameter.Width => new(PixelSize.Min, true, PixelSize.Max),
        Parameter.Height => new(PixelSize.Min, true, PixelSize.Max),
        Parameter.Octaves => new(Octaves.Min, true, Octaves.Max),
        Parameter.CellSize => new(CellSize.Min, true, CellSize.Max),
        Parameter.Persistence => new(Persistence.ExclusiveMin, false, Persistence.Max),
        Parameter.Level => new(ThresholdLevel.Min, true, ThresholdLevel.Max),
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
