namespace Tilegen.Core.Geometry;

/// <summary>A tile's place in the isometric layout: <c>I</c> runs down and to the right, <c>J</c> down and to the left.</summary>
public readonly record struct TileCoordinate(int I, int J);
