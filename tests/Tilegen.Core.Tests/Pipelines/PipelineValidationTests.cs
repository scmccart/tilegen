using System.Collections.Immutable;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

/// <summary>
/// One test per scenario in specs/pipeline-validation and the range and finest-octave scenarios
/// in specs/value-noise, plus the order rules of design D3. Every <see cref="Finding"/> case and
/// both <see cref="ValidationOutcome"/> cases are produced here by <see cref="Pipeline.Validate"/>.
/// </summary>
public class PipelineValidationTests
{
    private static readonly ParameterRange PixelSizeRange = new(PixelSize.Min, true, PixelSize.Max);
    private static readonly ParameterRange OctavesRange = new(Octaves.Min, true, Octaves.Max);
    private static readonly ParameterRange CellSizeRange = new(CellSize.Min, true, CellSize.Max);
    private static readonly ParameterRange PersistenceRange = new(Persistence.ExclusiveMin, false, Persistence.Max);

    private static NoiseDescription Stage(
        string id = "height", int width = 64, int height = 64, int octaves = 4, int cellSize = 16,
        double persistence = 0.5) =>
        new(id, width, height, octaves, cellSize, persistence);

    private static PipelineDescription Describe(params StageDescription[] stages) => new([.. stages]);

    private static ImmutableArray<Finding> FindingsOf(PipelineDescription description) =>
        Assert.IsType<Invalid>(Pipeline.Validate(description)).Findings;

    private static ValidatedPipeline PipelineOf(PipelineDescription description) =>
        Assert.IsType<Valid>(Pipeline.Validate(description)).Pipeline;

    private static ParameterRange RangeOf(Parameter parameter) => parameter switch
    {
        Parameter.Width => PixelSizeRange,
        Parameter.Height => PixelSizeRange,
        Parameter.Octaves => OctavesRange,
        Parameter.CellSize => CellSizeRange,
        Parameter.Persistence => PersistenceRange,
    };

    // Requirement: A description holds plain values.

    [Fact]
    public void A_noise_stage_is_described_with_plain_values()
    {
        var description = Describe(new NoiseDescription("height", 64, 32, 4, 16, 0.5));

        var stage = Assert.IsType<NoiseDescription>(Assert.Single(description.Stages));
        Assert.Equal("height", stage.Id);
        Assert.Equal(64, stage.Width);
        Assert.Equal(32, stage.Height);
        Assert.Equal(4, stage.Octaves);
        Assert.Equal(16, stage.CellSize);
        Assert.Equal(0.5, stage.Persistence);
    }

    [Fact]
    public void A_described_stage_gives_its_id_without_a_switch_over_its_technique()
    {
        StageDescription stage = Stage("height");

        var id = stage.Id;

        Assert.Equal("height", id);
    }

    // Requirement: Validation yields a runnable pipeline or findings.

    [Fact]
    public void A_valid_description_validates_to_a_pipeline_with_the_same_id_and_values()
    {
        var pipeline = PipelineOf(Describe(new NoiseDescription("height", 64, 32, 4, 16, 0.5)));

        var noise = Assert.IsType<Noise>(Assert.Single(pipeline.Stages));
        Assert.Equal("height", noise.Id.Value);
        Assert.Equal(64, noise.Parameters.Width.Value);
        Assert.Equal(32, noise.Parameters.Height.Value);
        Assert.Equal(4, noise.Parameters.Octaves.Value);
        Assert.Equal(16, noise.Parameters.CellSize.Value);
        Assert.Equal(0.5, noise.Parameters.Persistence.Value);
    }

    [Fact]
    public void A_valid_description_keeps_its_stages_in_description_order()
    {
        var pipeline = PipelineOf(Describe(Stage("b"), Stage("a"), Stage("c")));

        Assert.Equal(["b", "a", "c"], pipeline.Stages.Select(s => Assert.IsType<Noise>(s).Id.Value));
    }

    [Fact]
    public void A_validated_stage_gives_its_id_without_a_switch_over_its_technique()
    {
        var pipeline = PipelineOf(Describe(Stage("height")));

        Stage stage = Assert.Single(pipeline.Stages);

        Assert.Equal(StageId.Create("height"), stage.Id);
    }

    [Fact]
    public void A_zero_octave_count_gives_findings_and_no_pipeline()
    {
        var outcome = Pipeline.Validate(Describe(Stage(octaves: 0)));

        var invalid = Assert.IsType<Invalid>(outcome);
        Assert.Equal(
            [new OutOfRange(new StageRef(0, "height"), Parameter.Octaves, 0, OctavesRange)],
            invalid.Findings);
    }

    // Requirement: Validation reports every finding in one pass.

    [Fact]
    public void Several_mistakes_are_reported_together_each_naming_its_stage()
    {
        var findings = FindingsOf(Describe(Stage("height", width: 0, octaves: 0), Stage("height")));

        Assert.Equal(
            [
                new OutOfRange(new StageRef(0, "height"), Parameter.Width, 0, PixelSizeRange),
                new OutOfRange(new StageRef(0, "height"), Parameter.Octaves, 0, OctavesRange),
                new DuplicateStageId(new StageRef(1, "height"), 0),
            ],
            findings);
    }

    [Fact]
    public void Within_a_stage_the_id_comes_before_the_parameters_and_the_cross_field_rule_comes_last()
    {
        var findings = FindingsOf(Describe(Stage("", width: 0, octaves: 5, cellSize: 8)));

        Assert.Equal(
            [
                new InvalidStageId(new StageRef(0, "")),
                new OutOfRange(new StageRef(0, ""), Parameter.Width, 0, PixelSizeRange),
                new FinestOctaveBelowOnePixel(new StageRef(0, ""), 8, 5, 16),
            ],
            findings);
    }

    [Fact]
    public void Parameters_are_reported_in_declaration_order()
    {
        var findings = FindingsOf(
            Describe(Stage(width: 0, height: 4097, octaves: 13, cellSize: 0, persistence: 1.5)));

        var at = new StageRef(0, "height");
        Assert.Equal(
            [
                new OutOfRange(at, Parameter.Width, 0, PixelSizeRange),
                new OutOfRange(at, Parameter.Height, 4097, PixelSizeRange),
                new OutOfRange(at, Parameter.Octaves, 13, OctavesRange),
                new OutOfRange(at, Parameter.CellSize, 0, CellSizeRange),
                new OutOfRange(at, Parameter.Persistence, 1.5, PersistenceRange),
            ],
            findings);
    }

    [Fact]
    public void Stages_are_reported_in_description_order()
    {
        var findings = FindingsOf(Describe(Stage("a", width: 0), Stage("b"), Stage("c", height: 0)));

        Assert.Equal(
            [
                new OutOfRange(new StageRef(0, "a"), Parameter.Width, 0, PixelSizeRange),
                new OutOfRange(new StageRef(2, "c"), Parameter.Height, 0, PixelSizeRange),
            ],
            findings);
    }

    [Fact]
    public void A_valid_stage_beside_an_invalid_one_gives_no_pipeline()
    {
        var outcome = Pipeline.Validate(Describe(Stage("a"), Stage("b", persistence: 0)));

        Assert.IsType<Invalid>(outcome);
    }

    // Requirement: A pipeline has at least one stage.

    [Fact]
    public void An_empty_description_gives_exactly_one_empty_pipeline_finding()
    {
        var findings = FindingsOf(Describe());

        Assert.Equal([new EmptyPipeline()], findings);
    }

    [Fact]
    public void A_description_with_a_default_stage_list_is_an_empty_pipeline()
    {
        var findings = FindingsOf(new PipelineDescription(default));

        Assert.Equal([new EmptyPipeline()], findings);
    }

    // Requirement: Stage ids are present and unique.

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    [InlineData("two words")]
    [InlineData("trailing ")]
    public void An_empty_blank_or_spaced_id_is_reported_as_invalid(string id)
    {
        var findings = FindingsOf(Describe(Stage(id)));

        Assert.Equal([new InvalidStageId(new StageRef(0, id))], findings);
    }

    [Fact]
    public void A_null_id_is_reported_as_invalid_with_an_empty_given_id()
    {
        var findings = FindingsOf(Describe(Stage(null!)));

        Assert.Equal([new InvalidStageId(new StageRef(0, ""))], findings);
    }

    [Fact]
    public void A_repeated_id_is_reported_against_its_first_use()
    {
        var findings = FindingsOf(Describe(Stage("height"), Stage("other"), Stage("height")));

        Assert.Equal([new DuplicateStageId(new StageRef(2, "height"), 0)], findings);
    }

    [Fact]
    public void Every_repeat_of_an_id_names_its_first_use()
    {
        var findings = FindingsOf(Describe(Stage("a"), Stage("a"), Stage("a")));

        Assert.Equal(
            [new DuplicateStageId(new StageRef(1, "a"), 0), new DuplicateStageId(new StageRef(2, "a"), 0)],
            findings);
    }

    [Fact]
    public void Ids_differing_only_in_case_are_distinct()
    {
        var pipeline = PipelineOf(Describe(Stage("height"), Stage("Height")));

        Assert.Equal(2, pipeline.Stages.Length);
    }

    [Fact]
    public void A_repeated_blank_id_is_reported_as_invalid_and_never_as_a_duplicate()
    {
        var findings = FindingsOf(Describe(Stage(""), Stage("")));

        Assert.Equal(
            [new InvalidStageId(new StageRef(0, "")), new InvalidStageId(new StageRef(1, ""))],
            findings);
    }

    [Fact]
    public void A_stage_with_a_bad_parameter_still_claims_its_id()
    {
        var findings = FindingsOf(Describe(Stage("a", width: 0), Stage("a")));

        Assert.Equal(
            [
                new OutOfRange(new StageRef(0, "a"), Parameter.Width, 0, PixelSizeRange),
                new DuplicateStageId(new StageRef(1, "a"), 0),
            ],
            findings);
    }

    // Requirement: Out-of-range parameters are reported.

    [Fact]
    public void An_out_of_range_value_names_its_parameter_value_and_range()
    {
        var findings = FindingsOf(Describe(Stage(persistence: 1.5)));

        Assert.Equal(
            [new OutOfRange(new StageRef(0, "height"), Parameter.Persistence, 1.5, PersistenceRange)],
            findings);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_non_finite_persistence_is_out_of_range(double persistence)
    {
        var findings = FindingsOf(Describe(Stage(persistence: persistence)));

        Assert.Equal(
            [new OutOfRange(new StageRef(0, "height"), Parameter.Persistence, persistence, PersistenceRange)],
            findings);
    }

    // value-noise requirement: Noise parameters and their ranges.

    [Theory]
    [InlineData(1, 64, 4, 16, 0.5)]
    [InlineData(4096, 64, 4, 16, 0.5)]
    [InlineData(64, 1, 4, 16, 0.5)]
    [InlineData(64, 4096, 4, 16, 0.5)]
    [InlineData(64, 64, 1, 16, 0.5)]
    [InlineData(64, 64, 12, 2048, 0.5)]
    [InlineData(64, 64, 1, 1, 0.5)]
    [InlineData(64, 64, 4, 4096, 0.5)]
    [InlineData(64, 64, 4, 16, 1.0)]
    [InlineData(64, 64, 4, 16, 0.0001)]
    public void Values_at_the_edges_of_their_ranges_are_accepted(
        int width, int height, int octaves, int cellSize, double persistence)
    {
        var pipeline = PipelineOf(Describe(Stage("edge", width, height, octaves, cellSize, persistence)));

        Assert.Single(pipeline.Stages);
    }

    [Theory]
    [InlineData(0, 64, 4, 16, 0.5, Parameter.Width, 0)]
    [InlineData(4097, 64, 4, 16, 0.5, Parameter.Width, 4097)]
    [InlineData(64, 0, 4, 16, 0.5, Parameter.Height, 0)]
    [InlineData(64, 4097, 4, 16, 0.5, Parameter.Height, 4097)]
    [InlineData(64, 64, 0, 16, 0.5, Parameter.Octaves, 0)]
    [InlineData(64, 64, 13, 4096, 0.5, Parameter.Octaves, 13)]
    [InlineData(64, 64, 1, 0, 0.5, Parameter.CellSize, 0)]
    [InlineData(64, 64, 4, 4097, 0.5, Parameter.CellSize, 4097)]
    [InlineData(64, 64, 4, 16, 0.0, Parameter.Persistence, 0.0)]
    [InlineData(64, 64, 4, 16, -0.5, Parameter.Persistence, -0.5)]
    [InlineData(64, 64, 4, 16, 1.0000001, Parameter.Persistence, 1.0000001)]
    public void Values_just_outside_their_ranges_are_reported(
        int width, int height, int octaves, int cellSize, double persistence, Parameter parameter, double value)
    {
        var findings = FindingsOf(Describe(Stage("edge", width, height, octaves, cellSize, persistence)));

        Assert.Equal([new OutOfRange(new StageRef(0, "edge"), parameter, value, RangeOf(parameter))], findings);
    }

    // value-noise requirement: The finest octave is at least one pixel.

    [Fact]
    public void Too_many_octaves_for_the_cell_size_names_both_and_the_smallest_valid_cell_size()
    {
        var findings = FindingsOf(Describe(Stage(octaves: 5, cellSize: 8)));

        Assert.Equal([new FinestOctaveBelowOnePixel(new StageRef(0, "height"), 8, 5, 16)], findings);
    }

    [Fact]
    public void A_finest_octave_of_exactly_one_pixel_is_accepted()
    {
        var pipeline = PipelineOf(Describe(Stage(octaves: 4, cellSize: 8)));

        Assert.Single(pipeline.Stages);
    }

    [Fact]
    public void The_finest_octave_rule_is_skipped_when_octaves_are_out_of_range()
    {
        var findings = FindingsOf(Describe(Stage(octaves: 13, cellSize: 8)));

        Assert.Equal([new OutOfRange(new StageRef(0, "height"), Parameter.Octaves, 13, OctavesRange)], findings);
    }

    [Fact]
    public void The_finest_octave_rule_is_skipped_when_cell_size_is_out_of_range()
    {
        var findings = FindingsOf(Describe(Stage(octaves: 5, cellSize: 0)));

        Assert.Equal([new OutOfRange(new StageRef(0, "height"), Parameter.CellSize, 0, CellSizeRange)], findings);
    }

    // Requirement: Validation is deterministic and leaves the description unchanged.

    [Fact]
    public void Validating_the_same_invalid_description_twice_gives_the_same_findings_and_leaves_it_unchanged()
    {
        var description = Describe(Stage("", width: 0), Stage("a", octaves: 5, cellSize: 8), Stage("a"));
        var before = description.Stages.ToArray();

        var first = FindingsOf(description);
        var second = FindingsOf(description);

        Assert.Equal(first, second);
        Assert.Equal(before, description.Stages);
    }
}
