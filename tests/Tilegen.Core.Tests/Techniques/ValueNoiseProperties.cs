using CsCheck;
using Tilegen.Core.Imaging;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

/// <summary>
/// Properties of <see cref="ValueNoise.Run"/> over generated valid parameters, kept small (sides
/// up to 64) so the file runs quickly.
/// <para>
/// The inputs-unchanged property (testing R9, functional-core R4) has nothing to check here:
/// value noise reads no other stage's output. It is in <see cref="ThresholdProperties"/>, the
/// first technique that does.
/// </para>
/// </summary>
public class ValueNoiseProperties
{
    private const int Iterations = 100;

    /// <summary>Picks the range's edges often, because uniform draws rarely land on them.</summary>
    private static Gen<int> IntIn(int min, int max) =>
        Gen.Frequency<int>((4, Gen.Int[min, max]), (1, Gen.Const(min)), (1, Gen.Const(max)));

    /// <summary>
    /// A cell size that satisfies the finest-octave rule, mostly near the smallest valid one so the
    /// finer octaves are finer than the image, and sometimes anywhere up to the maximum.
    /// </summary>
    private static Gen<int> CellSizeFor(int octaves)
    {
        var smallest = NoiseParameters.SmallestValidCellSize(octaves);
        return Gen.Frequency<int>(
            (2, Gen.Const(smallest)),
            (3, Gen.Int[smallest, Math.Max(smallest, 64)]),
            (1, Gen.Int[smallest, CellSize.Max]));
    }

    /// <summary>(0, 1], with the edge 1 and the smallest positive double picked often.</summary>
    private static readonly Gen<double> InRangePersistence = Gen.Frequency<double>(
        (4, Gen.Double.Unit.Select(x => 1.0 - x)), (1, Gen.Const(1.0)), (1, Gen.Const(double.Epsilon)));

    private static readonly Gen<NoiseParameters> SmallParameters =
        from width in IntIn(1, 64)
        from height in IntIn(1, 64)
        from octaves in IntIn(Octaves.Min, Octaves.Max)
        from cellSize in CellSizeFor(octaves)
        from persistence in InRangePersistence
        select NoiseParameters.Create(
            PixelSize.Create(width),
            PixelSize.Create(height),
            Octaves.Create(octaves),
            CellSize.Create(cellSize),
            Persistence.Create(persistence));

    /// <summary>Stage seeds are never negative (stage-seeds.md).</summary>
    private static readonly Gen<int> Seeds = Gen.Int[0, int.MaxValue];

    /// <summary>[0, 1], with both ends and the largest value below 1 picked often.</summary>
    private static readonly Gen<double> UnitValue = Gen.Frequency<double>(
        (4, Gen.Double.Unit), (1, Gen.Const(0.0)), (1, Gen.Const(Math.BitDecrement(1.0))), (1, Gen.Const(1.0)));

    [Fact]
    public void Two_runs_with_the_same_seed_give_equal_frames()
    {
        Gen.Select(SmallParameters, Seeds).Sample(
            (parameters, seed) =>
            {
                var first = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(seed)));
                var second = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(seed)));

                Assert.Equal(first.Count, second.Count);
                Assert.All(first.Zip(second), pair => Assert.Null(Pixels.Difference(pair.First, pair.Second)));
            },
            iter: Iterations);
    }

    [Fact]
    public void A_run_yields_one_frame_per_octave_each_the_size_of_the_stage()
    {
        Gen.Select(SmallParameters, Seeds).Sample(
            (parameters, seed) =>
            {
                var frames = 0;
                foreach (var frame in ValueNoise.Run(parameters, new Random(seed)))
                {
                    frames++;
                    Assert.Equal((parameters.Width.Value, parameters.Height.Value), (frame.Width, frame.Height));
                }

                Assert.Equal(parameters.Octaves.Value, frames);
            },
            iter: Iterations);
    }

    [Fact]
    public void Every_pixel_of_every_frame_is_opaque_grey()
    {
        Gen.Select(SmallParameters, Seeds).Sample(
            (parameters, seed) =>
            {
                foreach (var frame in ValueNoise.Run(parameters, new Random(seed)))
                {
                    for (var y = 0; y < frame.Height; y++)
                    {
                        for (var x = 0; x < frame.Width; x++)
                        {
                            var pixel = frame[x, y];
                            Assert.Equal(Rgba32.Opaque(pixel.R, pixel.R, pixel.R), pixel);
                        }
                    }
                }
            },
            iter: Iterations);
    }

    /// <summary>The first step of the argument that a normalised value never passes 1, so no clamp is needed.</summary>
    [Fact]
    public void Smoothstep_keeps_a_weight_in_the_unit_interval()
    {
        UnitValue.Sample(
            t =>
            {
                var weight = ValueNoise.Smoothstep(t);
                Assert.InRange(weight, 0.0, 1.0);
            },
            iter: 10_000);
    }

    /// <summary>The second step: a blend of values in [0, 1] never leaves [0, 1], even by an ulp.</summary>
    [Fact]
    public void Lerp_of_values_in_the_unit_interval_stays_in_it()
    {
        Gen.Select(UnitValue, UnitValue, UnitValue).Sample(
            (a, b, weight) =>
            {
                Assert.InRange(ValueNoise.Lerp(a, b, weight), 0.0, 1.0);
            },
            iter: 10_000);
    }
}
