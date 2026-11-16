using Tilegen.Core.Imaging;

namespace Tilegen.Core.Techniques;

/// <summary>
/// Turns an image into a two-terrain map by brightness: opaque white (terrain 1) where a pixel is
/// at or above the level, opaque black (terrain 0) elsewhere. How it works, and why, is in
/// <c>docs/design/threshold.md</c>.
/// </summary>
public static class TerrainThreshold
{
    /// <summary>Terrain 0: opaque black, written where a pixel's brightness is below the level.</summary>
    public static readonly Rgba32 Below = Rgba32.Opaque(0, 0, 0);

    /// <summary>Terrain 1: opaque white, written where a pixel's brightness is at or above the level.</summary>
    public static readonly Rgba32 AtOrAbove = Rgba32.Opaque(255, 255, 255);

    /// <summary>
    /// Runs the threshold in one step. Calling it checks the arguments and does nothing else; the
    /// step allocates a working buffer of <paramref name="source"/>'s size, writes every pixel and
    /// yields the frame.
    /// <para>
    /// <paramref name="source"/> is another stage's output, and an <see cref="Image"/> can't be
    /// written to (functional-core R4). <paramref name="random"/> is taken so that every technique
    /// has the same shape, and nothing is drawn from it. The frame is a view over the run's working
    /// buffer, valid until the next step: call <see cref="Frame.ToImage"/> to keep it.
    /// </para>
    /// </summary>
    public static IEnumerable<Frame> Run(Image source, ThresholdParameters parameters, Random random)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(random);
        return Steps(source, parameters.Level.Value);
    }

    /// <summary>
    /// The iterator. The working buffer is created here, once per enumeration, and escapes only
    /// through the frame's read-only view (functional-core R12).
    /// </summary>
    private static IEnumerable<Frame> Steps(Image source, int level)
    {
        var buffer = new PixelBuffer(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                buffer[x, y] = Brightness(source[x, y]) >= level ? AtOrAbove : Below;
            }
        }

        yield return new Frame(buffer);
    }

    /// <summary>
    /// The mean of the red, green and blue channels, rounded down: integer division of a
    /// non-negative sum is a floor. Alpha is ignored. The result is 0 to 255.
    /// </summary>
    private static int Brightness(Rgba32 pixel) => (pixel.R + pixel.G + pixel.B) / 3;
}
