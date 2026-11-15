using Tilegen.Core.Imaging;

namespace Tilegen.TestKit;

/// <summary>
/// Exact comparison of buffers in memory, for tests that compare runs with each other rather than
/// with a golden, and a way to keep every frame of a run.
/// </summary>
public static class Pixels
{
    /// <summary>
    /// Copies each frame as it is yielded. A frame is a view over the run's working buffer and is
    /// valid only until the next step (functional-core R4), so collecting the frames themselves would
    /// give the last image many times over.
    /// </summary>
    public static IReadOnlyList<PixelBuffer> Snapshots(IEnumerable<Frame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        return frames.Select(frame => frame.ToBuffer()).ToList();
    }

    /// <summary>
    /// Null when the two buffers have the same size and the same pixels. Otherwise a description of
    /// the difference, for an assertion message: the two sizes, or how many pixels differ and the
    /// first, in row-major order, with both its values.
    /// </summary>
    public static string? Difference(PixelBuffer expected, PixelBuffer actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return $"Sizes differ: expected {expected.Width}x{expected.Height}, actual {actual.Width}x{actual.Height}.";
        }

        var count = 0;
        var first = "";
        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                if (expected[x, y] == actual[x, y])
                {
                    continue;
                }

                if (count == 0)
                {
                    first = $"({x}, {y}): expected {expected[x, y]}, actual {actual[x, y]}";
                }

                count++;
            }
        }

        return count == 0 ? null : $"{count} pixel(s) differ; the first is at {first}.";
    }
}
