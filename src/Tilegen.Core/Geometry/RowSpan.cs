namespace Tilegen.Core.Geometry;

/// <summary>One row's run of inside pixels: the x of its first pixel, and how many pixels it holds.</summary>
public readonly record struct RowSpan(int First, int Width);
