namespace Tilegen.Core.Pipelines;

/// <summary>
/// Which stage a finding is about: its position in the description, and its id as written. The
/// given id is raw text because the id may be what is wrong; an empty id is <c>""</c>, never null.
/// </summary>
public readonly record struct StageRef(int Index, string GivenId);

/// <summary>The parameters a finding can name. Shared by all techniques.</summary>
public enum Parameter
{
    Width,
    Height,
    Octaves,
    CellSize,
    Persistence,
    Level,
    TileWidth,
    Left,
    Top,
}

/// <summary>
/// The input slots a finding can name, the way <see cref="Parameter"/> names a parameter. Each slot
/// is a field of a technique's parameters that names an earlier stage. Shared by all techniques.
/// </summary>
public enum Input
{
    Source,
}

/// <summary>An allowed range. <see cref="Max"/> is always inclusive.</summary>
public readonly record struct ParameterRange(double Min, bool MinInclusive, double Max);

/// <summary>One thing wrong with a description. Carries data, not text; Desktop renders it.</summary>
public closed record class Finding;

public sealed record class EmptyPipeline : Finding;

/// <summary>The id is empty or contains whitespace.</summary>
public sealed record class InvalidStageId(StageRef Stage) : Finding;

public sealed record class DuplicateStageId(StageRef Stage, int FirstIndex) : Finding;

public sealed record class OutOfRange(StageRef Stage, Parameter Parameter, double Value, ParameterRange Range)
    : Finding;

public sealed record class FinestOctaveBelowOnePixel(
    StageRef Stage, int CellSize, int Octaves, int SmallestValidCellSize) : Finding;

/// <summary>
/// The value is in range but isn't a multiple of <paramref name="Divisor"/>. A value out of range is
/// reported as <see cref="OutOfRange"/> only, never as this too.
/// </summary>
public sealed record class NotMultipleOf(StageRef Stage, Parameter Parameter, int Value, int Divisor) : Finding;

/// <summary>
/// The tile's box, at (<paramref name="Left"/>, <paramref name="Top"/>) and
/// <paramref name="TileWidth"/> × <paramref name="TileHeight"/>, doesn't lie inside its source's
/// <paramref name="SourceWidth"/> × <paramref name="SourceHeight"/> image.
/// </summary>
public sealed record class TileOutsideSource(
    StageRef Stage, int Left, int Top, int TileWidth, int TileHeight, int SourceWidth, int SourceHeight) : Finding;

/// <summary>The input names no stage at all: its handle is null.</summary>
public sealed record class MissingInput(StageRef Stage, Input Input) : Finding;

/// <summary>
/// The input's handle names a position outside this description, or a position whose stage was
/// added with a different id, such as a handle from another builder. Carries the position and id
/// the handle gave.
/// </summary>
public sealed record class UnknownInput(StageRef Stage, Input Input, int GivenIndex, string GivenId) : Finding;

/// <summary>The input names the stage that holds it.</summary>
public sealed record class SelfInput(StageRef Stage, Input Input) : Finding;

/// <summary>The input names a stage that comes later in the description, at <paramref name="SourceIndex"/>.</summary>
public sealed record class LaterInput(StageRef Stage, Input Input, int SourceIndex) : Finding;
