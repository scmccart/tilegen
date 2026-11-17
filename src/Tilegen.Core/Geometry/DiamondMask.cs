namespace Tilegen.Core.Geometry;

/// <summary>
/// Which pixels of a tile's W by H box are inside its diamond. A pixel is inside when its centre is,
/// which in whole numbers is |2x + 1 - W| + 2|2y + 1 - H| &lt;= W. The sum is always odd and W is a
/// multiple of 4, so no pixel centre lies on an edge and no tie-break exists. The mask is a function
/// of the size alone and is computed, never stored.
/// </summary>
public sealed class DiamondMask
{
    public DiamondMask(IsoTileSize size)
    {
        ArgumentNullException.ThrowIfNull(size);
        Size = size;
    }

    public IsoTileSize Size { get; }

    /// <summary>True when pixel (x, y) of the box is inside the diamond. Throws outside the box.</summary>
    public bool Contains(int x, int y)
    {
        CheckInBox(x, y);

        var width = Size.Width;
        var height = Size.Height;
        return Math.Abs((2 * x) + 1 - width) + (2 * Math.Abs((2 * y) + 1 - height)) <= width;
    }

    /// <summary>The row's single inside run: 4 * min(y, H - 1 - y) + 2 pixels wide, centred. Throws outside the box.</summary>
    public RowSpan Row(int y)
    {
        CheckInBox(0, y);

        var width = (4 * Math.Min(y, Size.Height - 1 - y)) + 2;
        return new RowSpan((Size.Width / 2) - (width / 2), width);
    }

    private void CheckInBox(int x, int y)
    {
        if (x < 0 || x >= Size.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, $"The box is {Size.Width} pixels wide.");
        }

        if (y < 0 || y >= Size.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, $"The box is {Size.Height} pixels high.");
        }
    }
}
