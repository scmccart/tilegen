using Tilegen.Core.Geometry;
using Tilegen.Core.Pipelines;

namespace Tilegen.Core.Techniques;

/// <summary>
/// The checked parameters of a cut-tile stage: the stage it reads, the tile's size, and the position
/// in the source of the top-left corner of the tile's box. The position is a whole number from
/// <see cref="MinPosition"/> to <see cref="MaxPosition"/> on each axis.
/// <para>
/// That the box lies inside the source is a check against the source's size, which these parameters
/// don't know: validation makes it, and <see cref="DiamondCut.Run"/> checks it again at the call.
/// </para>
/// </summary>
public sealed record class CutTileParameters
{
    public const int MinPosition = 0;
    public const int MaxPosition = 4095;

    internal CutTileParameters(StageId source, IsoTileSize size, PixelPoint position)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentOutOfRangeException.ThrowIfLessThan(position.X, MinPosition, nameof(position));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position.X, MaxPosition, nameof(position));
        ArgumentOutOfRangeException.ThrowIfLessThan(position.Y, MinPosition, nameof(position));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position.Y, MaxPosition, nameof(position));
        Source = source;
        Size = size;
        Position = position;
    }

    /// <summary>The stage whose last image this stage cuts from.</summary>
    public StageId Source { get; }

    public IsoTileSize Size { get; }

    /// <summary>The top-left corner of the tile's box in the source.</summary>
    public PixelPoint Position { get; }

    /// <summary>For constants and tests, where a bad value is a bug. Values from a definition go through validation.</summary>
    public static CutTileParameters Create(StageId source, IsoTileSize size, PixelPoint position) =>
        new(source, size, position);
}
