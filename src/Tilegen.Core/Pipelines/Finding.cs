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
