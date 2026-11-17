namespace Tilegen.Core.Geometry;

/// <summary>A pixel position, which may be negative: the layout's origins are negative for tiles left of the first.</summary>
public readonly record struct PixelPoint(int X, int Y);
