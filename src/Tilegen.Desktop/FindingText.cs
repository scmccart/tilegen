using System.Globalization;
using Tilegen.Core.Pipelines;

namespace Tilegen.Desktop;

/// <summary>
/// The one place a validation finding becomes text for a person (railway R5): one arm per
/// <see cref="Finding"/> case and no discard arm, so a new case fails the build until it has text.
/// Numbers are written the same way on every machine.
/// </summary>
internal static class FindingText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Describe(Finding finding) => finding switch
    {
        EmptyPipeline => "The pipeline has no stages.",
        InvalidStageId invalid => string.Create(
            Invariant,
            $"{StageName(invalid.Stage)}: the id is empty or contains whitespace."),
        DuplicateStageId duplicate => string.Create(
            Invariant,
            $"{StageName(duplicate.Stage)}: the id is already used by the stage at index {duplicate.FirstIndex}."),
        OutOfRange outOfRange => string.Create(
            Invariant,
            $"{StageName(outOfRange.Stage)}: {ParameterName(outOfRange.Parameter)} is {outOfRange.Value}, "
                + $"but must be {RangeText(outOfRange.Range)}."),
        FinestOctaveBelowOnePixel finest => string.Create(
            Invariant,
            $"{StageName(finest.Stage)}: cell size is {finest.CellSize}, but {finest.Octaves} octaves need at least "
                + $"{finest.SmallestValidCellSize}."),
        NotMultipleOf notMultiple => string.Create(
            Invariant,
            $"{StageName(notMultiple.Stage)}: {ParameterName(notMultiple.Parameter)} is {notMultiple.Value}, "
                + $"which is not a multiple of {notMultiple.Divisor}."),
        TileOutsideSource outside => string.Create(
            Invariant,
            $"{StageName(outside.Stage)}: the {outside.TileWidth} × {outside.TileHeight} tile at "
                + $"({outside.Left}, {outside.Top}) doesn't fit inside its {outside.SourceWidth} × "
                + $"{outside.SourceHeight} source."),
        MissingInput missing => string.Create(
            Invariant,
            $"{StageName(missing.Stage)}: {InputName(missing.Input)} names no stage."),
        UnknownInput unknown => string.Create(
            Invariant,
            $"{StageName(unknown.Stage)}: {InputName(unknown.Input)} names a stage at index {unknown.GivenIndex} "
                + $"(\"{unknown.GivenId}\"), which this pipeline doesn't have."),
        SelfInput self => string.Create(
            Invariant,
            $"{StageName(self.Stage)}: {InputName(self.Input)} names this stage itself, "
                + $"but a stage can only read a stage before it."),
        LaterInput later => string.Create(
            Invariant,
            $"{StageName(later.Stage)}: {InputName(later.Input)} names the stage at index {later.SourceIndex}, "
                + $"which comes later."),
    };

    /// <summary>Names a stage by its position in the description and its id as written, which may be what is wrong.</summary>
    private static string StageName(StageRef stage) =>
        string.Create(Invariant, $"Stage at index {stage.Index} (\"{stage.GivenId}\")");

    private static string ParameterName(Parameter parameter) => parameter switch
    {
        Parameter.Width => "width",
        Parameter.Height => "height",
        Parameter.Octaves => "octaves",
        Parameter.CellSize => "cell size",
        Parameter.Persistence => "persistence",
        Parameter.Level => "level",
        Parameter.TileWidth => "tile width",
        Parameter.Left => "left",
        Parameter.Top => "top",
    };

    private static string InputName(Input input) => input switch
    {
        Input.Source => "source",
    };

    /// <summary>The maximum is always inclusive; the minimum may not be.</summary>
    private static string RangeText(ParameterRange range) => range.MinInclusive switch
    {
        true => string.Create(Invariant, $"from {range.Min} to {range.Max}"),
        false => string.Create(Invariant, $"more than {range.Min} and at most {range.Max}"),
    };
}
