using Tilegen.Core.Imaging;

namespace Tilegen.Core.Techniques;

/// <summary>
/// Value noise summed over octaves (fractal Brownian motion), written as opaque grey. How it
/// works, and why, is in <c>docs/design/value-noise.md</c>.
/// </summary>
public static class ValueNoise
{
    /// <summary>
    /// Runs value noise one octave per step, coarsest first. Each step draws its octave's lattice
    /// from <paramref name="random"/>, adds the octave into a running sum, and yields that sum,
    /// normalised by the sum of the amplitudes so far, as a frame. Nothing is drawn or computed
    /// until the first frame is requested.
    /// <para>
    /// Each frame is a view over the run's working buffer, valid until the next step
    /// (functional-core R4): call <see cref="Frame.ToBuffer"/> to keep one. The run holds nothing
    /// that needs disposing, so it can be abandoned after any frame.
    /// </para>
    /// </summary>
    public static IEnumerable<Frame> Run(NoiseParameters parameters, Random random)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(random);
        return Steps(parameters, random);
    }

    /// <summary>
    /// The iterator. The accumulator and the working buffer are created here, once per enumeration,
    /// and never escape except through the frame's read-only view (functional-core R12).
    /// </summary>
    private static IEnumerable<Frame> Steps(NoiseParameters parameters, Random random)
    {
        var width = parameters.Width.Value;
        var height = parameters.Height.Value;
        var persistence = parameters.Persistence.Value;

        var accumulator = new double[width * height];
        var buffer = new PixelBuffer(width, height);
        var frame = new Frame(buffer);

        // Octave k has spacing CellSize / 2^k and amplitude persistence^k. Halving and repeated
        // multiplication give both without Math.Pow; halving a double is exact.
        var spacing = (double)parameters.CellSize.Value;
        var amplitude = 1.0;
        var amplitudeSum = 0.0;
        for (var octave = 0; octave < parameters.Octaves.Value; octave++)
        {
            AddOctave(accumulator, width, height, spacing, amplitude, random);
            amplitudeSum += amplitude;
            WriteGrey(accumulator, amplitudeSum, buffer);
            yield return frame;

            spacing /= 2;
            amplitude *= persistence;
        }
    }

    /// <summary>
    /// One octave. Draws its lattice, row-major, at the start of the step, then adds
    /// <paramref name="amplitude"/> times the blended lattice value at every pixel into the
    /// accumulator. The lattice is a local here rather than in the iterator, so it isn't kept alive
    /// while the run waits between frames.
    /// </summary>
    private static void AddOctave(
        double[] accumulator, int width, int height, double spacing, double amplitude, Random random)
    {
        // Pixel x lies in cell ⌊x / s⌋ and reads that cell's corner and the next, so the last pixel
        // needs ⌊(W − 1) / s⌋ + 2 lattice columns. The same expression sizes the lattice and finds
        // each pixel's cell, so the two always agree.
        var latticeWidth = CellOf(width - 1, spacing) + 2;
        var latticeHeight = CellOf(height - 1, spacing) + 2;
        var lattice = new double[latticeWidth * latticeHeight];
        for (var index = 0; index < lattice.Length; index++)
        {
            lattice[index] = random.NextDouble();
        }

        for (var y = 0; y < height; y++)
        {
            var row = CellOf(y, spacing);
            var weightY = Smoothstep((y / spacing) - row);
            var above = row * latticeWidth;
            var below = above + latticeWidth;
            for (var x = 0; x < width; x++)
            {
                var column = CellOf(x, spacing);
                var weightX = Smoothstep((x / spacing) - column);
                var top = Lerp(lattice[above + column], lattice[above + column + 1], weightX);
                var bottom = Lerp(lattice[below + column], lattice[below + column + 1], weightX);
                accumulator[(y * width) + x] += amplitude * Lerp(top, bottom, weightY);
            }
        }
    }

    /// <summary>
    /// Normalises each accumulated value by the sum of the amplitudes so far and writes it as opaque
    /// grey, rounded to the nearest of 0 to 255.
    /// <para>
    /// No clamp is needed: the value is always in [0, 1]. Each lattice value is in [0, 1)
    /// (<see cref="Random.NextDouble"/>), each blend of values in [0, 1] stays in [0, 1] (see
    /// <see cref="Lerp"/>), and the accumulator and <paramref name="amplitudeSum"/> are summed in the
    /// same order, so each rounded partial sum of amplitude × value is at most the rounded partial sum
    /// of amplitudes. IEEE rounding is monotone, so the quotient is at most 1.
    /// </para>
    /// </summary>
    private static void WriteGrey(double[] accumulator, double amplitudeSum, PixelBuffer buffer)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var value = accumulator[(y * buffer.Width) + x] / amplitudeSum;
                var grey = (byte)Math.Round(value * 255, MidpointRounding.AwayFromZero);
                buffer[x, y] = Rgba32.Opaque(grey, grey, grey);
            }
        }
    }

    /// <summary>The lattice cell that coordinate <paramref name="pixel"/> falls in: ⌊pixel / spacing⌋.</summary>
    private static int CellOf(int pixel, double spacing) => (int)Math.Floor(pixel / spacing);

    /// <summary>
    /// The smoothstep weight t²(3 − 2t). For t in [0, 1] the result stays in [0, 1]: its distance
    /// below 1 is (1 − t)²(1 + 2t), and in the run t is at most 1 − 1/CellSize, far above rounding
    /// error.
    /// </summary>
    internal static double Smoothstep(double t) => t * t * (3 - (2 * t));

    /// <summary>
    /// Linear interpolation in the form a + (b − a)·w. With a and b in [0, 1] and w in [0, 1] the
    /// rounded result stays in [0, 1], because rounding is monotone and the result is bounded by
    /// a and by a + round(b − a). The form a·(1 − w) + b·w has no such bound and can pass the larger
    /// end by an ulp.
    /// </summary>
    internal static double Lerp(double a, double b, double weight) => a + ((b - a) * weight);
}
