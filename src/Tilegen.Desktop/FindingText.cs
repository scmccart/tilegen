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
    };

    /// <summary>The maximum is always inclusive; the minimum may not be.</summary>
    private static string RangeText(ParameterRange range) => range.MinInclusive switch
    {
        true => string.Create(Invariant, $"from {range.Min} to {range.Max}"),
        false => string.Create(Invariant, $"more than {range.Min} and at most {range.Max}"),
    };
}
