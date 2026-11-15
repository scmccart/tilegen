using Tilegen.Core.Imaging;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Core.Tests.Techniques;

public class ValueNoiseTests
{
    private const int Seed = 20261114;

    [Fact]
    public void Four_octaves_at_64_by_32_give_four_frames_of_64_by_32()
    {
        var parameters = Parameters(64, 32, octaves: 4, cellSize: 16);

        var frames = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(Seed)));

        Assert.Equal(4, frames.Count);
        Assert.All(frames, frame => Assert.Equal((64, 32), (frame.Width, frame.Height)));
    }

    [Fact]
    public void The_first_frame_of_a_three_octave_run_equals_a_one_octave_run_with_the_same_seed()
    {
        var threeOctaves = Pixels.Snapshots(ValueNoise.Run(Parameters(64, 32, octaves: 3, cellSize: 16), new Random(Seed)));
        var oneOctave = Pixels.Snapshots(ValueNoise.Run(Parameters(64, 32, octaves: 1, cellSize: 16), new Random(Seed)));

        Assert.Null(Pixels.Difference(Assert.Single(oneOctave), threeOctaves[0]));
    }

    [Fact]
    public void Every_pixel_of_every_frame_is_opaque_grey()
    {
        var frames = 0;
        foreach (var frame in ValueNoise.Run(Parameters(64, 32, octaves: 4, cellSize: 16), new Random(Seed)))
        {
            frames++;
            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var pixel = frame[x, y];
                    Assert.Equal(Rgba32.Opaque(pixel.R, pixel.R, pixel.R), pixel);
                }
            }
        }

        Assert.Equal(4, frames);
    }

    [Fact]
    public void The_same_seed_gives_the_same_pixels()
    {
        var parameters = Parameters(64, 32, octaves: 4, cellSize: 16);

        var first = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(Seed)));
        var second = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(Seed)));

        Assert.Equal(first.Count, second.Count);
        Assert.All(first.Zip(second), pair => Assert.Null(Pixels.Difference(pair.First, pair.Second)));
    }

    [Fact]
    public void Different_seeds_give_different_images_at_64_by_64()
    {
        var parameters = Parameters(64, 64, octaves: 4, cellSize: 16);

        var one = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(1)))[^1];
        var two = Pixels.Snapshots(ValueNoise.Run(parameters, new Random(2)))[^1];

        Assert.NotNull(Pixels.Difference(one, two));
    }

    [Fact]
    public void A_kept_copy_of_the_first_frame_is_unaffected_by_continuing_the_run()
    {
        var parameters = Parameters(64, 32, octaves: 3, cellSize: 16);
        var asYielded = ValueNoise.Run(parameters, new Random(Seed)).First().ToBuffer();
        using var steps = ValueNoise.Run(parameters, new Random(Seed)).GetEnumerator();
        Assert.True(steps.MoveNext());
        var kept = steps.Current.ToBuffer();

        var last = kept;
        while (steps.MoveNext())
        {
            last = steps.Current.ToBuffer();
        }

        Assert.Null(Pixels.Difference(asYielded, kept));
        Assert.NotNull(Pixels.Difference(kept, last));
    }

    [Fact]
    public void Starting_a_run_draws_nothing_until_the_first_frame_is_requested()
    {
        var random = new Random(Seed);

        _ = ValueNoise.Run(Parameters(64, 32, octaves: 3, cellSize: 16), random);

        Assert.Equal(new Random(Seed).NextDouble(), random.NextDouble());
    }

    /// <summary>
    /// At 64 by 32 with cell size 16, octave 0 has spacing 16 and a lattice of
    /// (⌊63/16⌋ + 2) by (⌊31/16⌋ + 2) = 5 by 3 = 15 values; octave 1 has spacing 8 and
    /// 9 by 5 = 45. Each step draws exactly its own lattice: peeking at the stage's
    /// <see cref="Random"/> after a step gives the value a reference reaches after that many draws.
    /// </summary>
    [Fact]
    public void Each_step_draws_its_own_octaves_lattice_and_nothing_more()
    {
        var random = new Random(Seed);
        var reference = new Random(Seed);
        using var steps = ValueNoise.Run(Parameters(64, 32, octaves: 3, cellSize: 16), random).GetEnumerator();

        Assert.True(steps.MoveNext());
        Skip(reference, 15);
        Assert.Equal(reference.NextDouble(), random.NextDouble());

        Assert.True(steps.MoveNext());
        Skip(reference, 45);
        Assert.Equal(reference.NextDouble(), random.NextDouble());
    }

    /// <summary>
    /// Spacing 1 puts every pixel on a lattice point, so each pixel is its own draw from a
    /// row-major lattice W + 1 wide and H + 1 high, scaled to 0 to 255 and rounded.
    /// </summary>
    [Fact]
    public void With_a_cell_size_of_one_each_pixel_is_its_lattice_value()
    {
        const int width = 5;
        const int height = 3;
        var reference = new Random(Seed);
        var lattice = new double[(width + 1) * (height + 1)];
        for (var index = 0; index < lattice.Length; index++)
        {
            lattice[index] = reference.NextDouble();
        }

        var frame = Assert.Single(Pixels.Snapshots(ValueNoise.Run(Parameters(width, height, octaves: 1, cellSize: 1), new Random(Seed))));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var grey = (byte)Math.Round(lattice[(y * (width + 1)) + x] * 255, MidpointRounding.AwayFromZero);
                Assert.Equal(Rgba32.Opaque(grey, grey, grey), frame[x, y]);
            }
        }
    }

    private static NoiseParameters Parameters(int width, int height, int octaves, int cellSize) =>
        NoiseParameters.Create(
            PixelSize.Create(width),
            PixelSize.Create(height),
            Octaves.Create(octaves),
            CellSize.Create(cellSize),
            Persistence.Create(0.5));

    private static void Skip(Random random, int draws)
    {
        for (var draw = 0; draw < draws; draw++)
        {
            random.NextDouble();
        }
    }
}
