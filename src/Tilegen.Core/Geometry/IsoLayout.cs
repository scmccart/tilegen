namespace Tilegen.Core.Geometry;

/// <summary>
/// Where tiles sit in the isometric layout, so that neighbours fit with no gap and no overlap: the
/// diamond layout meant to match Tiled's "isometric" orientation. Tiled's documentation doesn't pin
/// the placement down, so the match is checked by hand when the tileset is written
/// (<c>docs/design/isometric-geometry.md</c>).
/// </summary>
public static class IsoLayout
{
    /// <summary>
    /// The top-left corner of the box of the tile at (i, j): ((i - j) * W / 2, (i + j) * H / 2).
    /// Checked, so a coordinate far enough out to overflow throws rather than wraps.
    /// </summary>
    public static PixelPoint Origin(IsoTileSize size, TileCoordinate tile)
    {
        ArgumentNullException.ThrowIfNull(size);

        checked
        {
            return new PixelPoint((tile.I - tile.J) * (size.Width / 2), (tile.I + tile.J) * (size.Height / 2));
        }
    }
}
