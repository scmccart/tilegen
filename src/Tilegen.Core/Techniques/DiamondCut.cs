using Tilegen.Core.Geometry;
using Tilegen.Core.Imaging;

namespace Tilegen.Core.Techniques;

/// <summary>
/// Cuts one isometric tile out of an image: the pixels inside the tile's diamond are kept, and
/// everything else in the tile's box is transparent black.
/// </summary>
public static class DiamondCut
{
    /// <summary>Written at every pixel of the box outside the diamond: red, green, blue and alpha all 0.</summary>
    public static readonly Rgba32 Outside = new(0, 0, 0, 0);

    /// <summary>
    /// Runs the cut in one step. Calling it checks the arguments, including that the tile's box
    /// lies inside <paramref name="source"/>, and does nothing else; the step allocates a
    /// width × height buffer, copies each row's inside run from the source and yields the frame.
    /// <para>
    /// <paramref name="random"/> is taken so that every technique has the same shape, and nothing is
    /// drawn from it. The frame is a view over the run's working buffer, valid until the next step:
    /// call <see cref="Frame.ToImage"/> to keep it.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">The tile's box is not wholly inside <paramref name="source"/>.</exception>
    public static IEnumerable<Frame> Run(Image source, CutTileParameters parameters, Random random)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(random);

        var size = parameters.Size;
        var position = parameters.Position;
        if (position.X + size.Width > source.Width || position.Y + size.Height > source.Height)
        {
            throw new ArgumentException(
                $"A {size.Width} by {size.Height} tile at ({position.X}, {position.Y}) does not fit inside a "
                + $"{source.Width} by {source.Height} source.",
                nameof(parameters));
        }

        return Steps(source, size, position);
    }

    /// <summary>
    /// The iterator. The working buffer is created here, once per enumeration, and escapes only
    /// through the frame's read-only view (functional-core R12).
    /// </summary>
    private static IEnumerable<Frame> Steps(Image source, IsoTileSize size, PixelPoint position)
    {
        var mask = new DiamondMask(size);
        var buffer = new PixelBuffer(size.Width, size.Height);
        for (var y = 0; y < size.Height; y++)
        {
            var row = mask.Row(y);
            for (var x = 0; x < size.Width; x++)
            {
                var inside = x >= row.First && x < row.First + row.Width;
                buffer[x, y] = inside ? source[position.X + x, position.Y + y] : Outside;
            }
        }

        yield return new Frame(buffer);
    }
}
