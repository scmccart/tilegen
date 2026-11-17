using System.Collections.Immutable;
using Tilegen.Core.Geometry;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

/// <summary>
/// One test per scenario in specs/pipeline-validation, the range and finest-octave scenarios in
/// specs/value-noise and the validation scenarios in specs/threshold and specs/cut-tile, plus the
/// order rules: stages in description order, and within a stage the id, then each parameter in
/// declaration order, inputs included, then the cross-field rule; for a cut, the fit inside its
/// source last. Every <see cref="Finding"/> case and both <see cref="ValidationOutcome"/> cases are
/// produced here by <see cref="Pipeline.Validate"/>. Later and self inputs use handles from a second
/// builder whose stages have matching ids.
/// </summary>
public class PipelineValidationTests
{
    private static readonly ParameterRange PixelSizeRange = new(PixelSize.Min, true, PixelSize.Max);
    private static readonly ParameterRange OctavesRange = new(Octaves.Min, true, Octaves.Max);
    private static readonly ParameterRange CellSizeRange = new(CellSize.Min, true, CellSize.Max);
    private static readonly ParameterRange PersistenceRange = new(Persistence.ExclusiveMin, false, Persistence.Max);
    private static readonly ParameterRange LevelRange = new(ThresholdLevel.Min, true, ThresholdLevel.Max);
    private static readonly ParameterRange TileWidthRange = new(IsoTileSize.Min, true, IsoTileSize.Max);
    private static readonly ParameterRange PositionRange =
        new(CutTileParameters.MinPosition, true, CutTileParameters.MaxPosition);

    private static NoiseDescription Stage(
        string id = "height", int width = 64, int height = 64, int octaves = 4, int cellSize = 16,
        double persistence = 0.5) =>
        new(id, width, height, octaves, cellSize, persistence);

    private static ThresholdDescription ThresholdStage(string id, StageHandle source, int level = 128) =>
        new(id, source, level);

    private static CutTileDescription CutStage(
        string id, StageHandle source, int tileWidth = 64, int left = 0, int top = 0) =>
        new(id, source, tileWidth, left, top);

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
        Parameter.Level => LevelRange,
        Parameter.TileWidth => TileWidthRange,
        Parameter.Left => PositionRange,
        Parameter.Top => PositionRange,
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

    // Requirement: A builder hands out a handle for each stage added.

    [Fact]
    public void A_stage_reads_an_earlier_stage_through_its_handle()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(ThresholdStage("terrain", height));

        var description = builder.Build();

        Assert.Equal(["height", "terrain"], description.Stages.Select(stage => stage.Id));
        var threshold = Assert.IsType<ThresholdDescription>(description.Stages[1]);
        Assert.Equal((0, "height"), (threshold.Source.Index, threshold.Source.Id));
    }

    // Requirement: A missing input is reported.

    [Fact]
    public void An_input_left_empty_is_reported_as_missing()
    {
        var findings = FindingsOf(Describe(Stage("height"), ThresholdStage("terrain", null!)));

        Assert.Equal([new MissingInput(new StageRef(1, "terrain"), Input.Source)], findings);
    }

    // Requirement: An input from elsewhere is reported.

    [Fact]
    public void A_handle_from_another_builder_is_reported_as_unknown()
    {
        var other = new PipelineBuilder().Add(Stage("other"));

        var findings = FindingsOf(Describe(Stage("height"), ThresholdStage("terrain", other)));

        Assert.Equal([new UnknownInput(new StageRef(1, "terrain"), Input.Source, 0, "other")], findings);
    }

    [Fact]
    public void A_handle_to_a_position_past_the_end_is_reported_as_unknown()
    {
        var other = new PipelineBuilder();
        other.Add(Stage("a"));
        other.Add(Stage("b"));
        var third = other.Add(Stage("height"));

        var findings = FindingsOf(Describe(Stage("height"), ThresholdStage("terrain", third)));

        Assert.Equal([new UnknownInput(new StageRef(1, "terrain"), Input.Source, 2, "height")], findings);
    }

    [Fact]
    public void A_handle_whose_id_differs_only_in_case_is_reported_as_unknown()
    {
        var other = new PipelineBuilder().Add(Stage("Height"));

        var findings = FindingsOf(Describe(Stage("height"), ThresholdStage("terrain", other)));

        Assert.Equal([new UnknownInput(new StageRef(1, "terrain"), Input.Source, 0, "Height")], findings);
    }

    // Requirement: An input names an earlier stage.

    [Fact]
    public void A_stage_that_reads_itself_is_reported()
    {
        var other = new PipelineBuilder().Add(Stage("t"));

        var findings = FindingsOf(Describe(ThresholdStage("t", other)));

        Assert.Equal([new SelfInput(new StageRef(0, "t"), Input.Source)], findings);
    }

    [Fact]
    public void A_stage_that_reads_a_later_stage_is_reported_with_the_later_position()
    {
        var other = new PipelineBuilder();
        other.Add(Stage("x"));
        var later = other.Add(Stage("height"));

        var findings = FindingsOf(Describe(ThresholdStage("terrain", later), Stage("height")));

        Assert.Equal([new LaterInput(new StageRef(0, "terrain"), Input.Source, 1)], findings);
    }

    // Requirement: An input is not reported for its source's own findings.

    [Fact]
    public void Reading_a_stage_that_has_a_bad_parameter_reports_only_that_stages_finding()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 0));
        builder.Add(ThresholdStage("terrain", height));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(0, "height"), Parameter.Width, 0, PixelSizeRange)], findings);
    }

    [Theory]
    [InlineData("two words")]
    [InlineData("")]
    public void Reading_a_stage_with_an_invalid_id_reports_only_that_stages_finding(string id)
    {
        var builder = new PipelineBuilder();
        var source = builder.Add(Stage(id));
        builder.Add(ThresholdStage("terrain", source));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new InvalidStageId(new StageRef(0, id))], findings);
    }

    [Fact]
    public void Reading_a_stage_with_a_null_id_reports_only_that_stages_finding()
    {
        var builder = new PipelineBuilder();
        var source = builder.Add(Stage(null!));
        builder.Add(ThresholdStage("terrain", source));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new InvalidStageId(new StageRef(0, ""))], findings);
    }

    [Fact]
    public void Reading_a_stage_with_a_repeated_id_reports_only_the_repeat()
    {
        var builder = new PipelineBuilder();
        builder.Add(Stage("a"));
        var repeat = builder.Add(Stage("a"));
        builder.Add(ThresholdStage("terrain", repeat));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new DuplicateStageId(new StageRef(1, "a"), 0)], findings);
    }

    [Fact]
    public void Reading_a_threshold_whose_own_source_is_bad_reports_only_the_bad_stage()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 0));
        var terrain = builder.Add(ThresholdStage("terrain", height));
        builder.Add(ThresholdStage("again", terrain));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(0, "height"), Parameter.Width, 0, PixelSizeRange)], findings);
    }

    // threshold requirement: A threshold stage has a source and a level.

    [Fact]
    public void A_level_out_of_range_is_reported()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(ThresholdStage("terrain", height, level: 256));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(1, "terrain"), Parameter.Level, 256, LevelRange)], findings);
        Assert.Equal(new ParameterRange(0, true, 255), LevelRange);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void A_level_just_outside_its_range_is_reported(int level)
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(ThresholdStage("terrain", height, level));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(1, "terrain"), Parameter.Level, level, LevelRange)], findings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void A_level_at_the_edge_of_its_range_is_accepted(int level)
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(ThresholdStage("terrain", height, level));

        var pipeline = PipelineOf(builder.Build());

        Assert.Equal(level, Assert.IsType<Threshold>(pipeline.Stages[1]).Parameters.Level.Value);
    }

    [Fact]
    public void A_valid_threshold_stage_validates_reading_the_noise_stage()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(ThresholdStage("terrain", height, level: 128));

        var pipeline = PipelineOf(builder.Build());

        Assert.Equal(2, pipeline.Stages.Length);
        var noise = Assert.IsType<Noise>(pipeline.Stages[0]);
        var threshold = Assert.IsType<Threshold>(pipeline.Stages[1]);
        Assert.Equal(StageId.Create("height"), noise.Id);
        Assert.Equal(StageId.Create("terrain"), threshold.Id);
        Assert.Equal(noise.Id, threshold.Parameters.Source);
        Assert.Equal(128, threshold.Parameters.Level.Value);
    }

    [Fact]
    public void A_threshold_may_read_another_threshold()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        var terrain = builder.Add(ThresholdStage("terrain", height));
        builder.Add(ThresholdStage("again", terrain));

        var pipeline = PipelineOf(builder.Build());

        Assert.Equal(StageId.Create("terrain"), Assert.IsType<Threshold>(pipeline.Stages[2]).Parameters.Source);
    }

    [Fact]
    public void Within_a_threshold_stage_the_id_comes_first_then_the_source_then_the_level()
    {
        var findings = FindingsOf(Describe(Stage("height"), ThresholdStage("", null!, level: 300)));

        var at = new StageRef(1, "");
        Assert.Equal(
            [
                new InvalidStageId(at),
                new MissingInput(at, Input.Source),
                new OutOfRange(at, Parameter.Level, 300, LevelRange),
            ],
            findings);
    }

    // cut-tile requirement: A cut-tile stage has a source, a tile width and a position.

    [Fact]
    public void A_negative_position_is_reported()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(CutStage("tile", height, top: -1));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(1, "tile"), Parameter.Top, -1, PositionRange)], findings);
        Assert.Equal(new ParameterRange(0, true, 4095), PositionRange);
    }

    [Fact]
    public void A_valid_cut_tile_stage_validates_reading_the_noise_stage()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 128, height: 128));
        builder.Add(CutStage("tile", height, tileWidth: 64, left: 0, top: 16));

        var pipeline = PipelineOf(builder.Build());

        Assert.Equal(2, pipeline.Stages.Length);
        var noise = Assert.IsType<Noise>(pipeline.Stages[0]);
        var cut = Assert.IsType<CutTile>(pipeline.Stages[1]);
        Assert.Equal(StageId.Create("height"), noise.Id);
        Assert.Equal(StageId.Create("tile"), cut.Id);
        Assert.Equal(noise.Id, cut.Parameters.Source);
        Assert.Equal(IsoTileSize.Create(64), cut.Parameters.Size);
        Assert.Equal(new PixelPoint(0, 16), cut.Parameters.Position);
    }

    /// <summary>A value out of range is a finding, so the fit isn't checked, even for a box far outside the source.</summary>
    [Theory]
    [InlineData(-1, 0, Parameter.Left, -1)]
    [InlineData(4096, 0, Parameter.Left, 4096)]
    [InlineData(0, -1, Parameter.Top, -1)]
    [InlineData(0, 4096, Parameter.Top, 4096)]
    public void A_position_just_outside_its_range_is_reported(int left, int top, Parameter parameter, int value)
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(CutStage("tile", height, tileWidth: 16, left, top));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(1, "tile"), parameter, value, RangeOf(parameter))], findings);
    }

    // cut-tile requirement: A tile width is a multiple of 4.

    [Fact]
    public void A_width_out_of_range_is_reported_once()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(CutStage("tile", height, tileWidth: 2));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(1, "tile"), Parameter.TileWidth, 2, TileWidthRange)], findings);
        Assert.Equal(new ParameterRange(4, true, 4096), TileWidthRange);
    }

    [Fact]
    public void A_width_that_is_not_a_multiple_of_four_is_reported()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(CutStage("tile", height, tileWidth: 30));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new NotMultipleOf(new StageRef(1, "tile"), Parameter.TileWidth, 30, 4)], findings);
    }

    /// <summary>
    /// Out of range is reported alone, whether or not the width is a multiple of 4: -4 and 4100 are
    /// multiples of 4, and 3 and 4097 are not.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4097)]
    [InlineData(-4)]
    [InlineData(4100)]
    public void A_width_just_outside_its_range_is_reported_only_as_out_of_range(int tileWidth)
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(CutStage("tile", height, tileWidth));

        var findings = FindingsOf(builder.Build());

        Assert.Equal(
            [new OutOfRange(new StageRef(1, "tile"), Parameter.TileWidth, tileWidth, TileWidthRange)], findings);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(4096)]
    public void A_width_at_the_edge_of_its_range_is_accepted(int tileWidth)
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 4096, height: 2048));
        builder.Add(CutStage("tile", height, tileWidth));

        var pipeline = PipelineOf(builder.Build());

        Assert.Equal(tileWidth, Assert.IsType<CutTile>(pipeline.Stages[1]).Parameters.Size.Width);
    }

    // cut-tile requirement: The tile fits inside its source.

    [Fact]
    public void A_tile_that_runs_off_the_bottom_of_its_source_is_reported()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 64, height: 64));
        builder.Add(CutStage("tile", height, tileWidth: 64, left: 0, top: 40));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new TileOutsideSource(new StageRef(1, "tile"), 0, 40, 64, 32, 64, 64)], findings);
    }

    [Fact]
    public void A_tile_larger_than_its_source_is_reported()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 32, height: 32));
        builder.Add(CutStage("tile", height, tileWidth: 64, left: 0, top: 0));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new TileOutsideSource(new StageRef(1, "tile"), 0, 0, 64, 32, 32, 32)], findings);
    }

    [Fact]
    public void A_tile_that_runs_off_the_right_of_its_source_is_reported()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 64, height: 64));
        builder.Add(CutStage("tile", height, tileWidth: 16, left: 49, top: 0));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new TileOutsideSource(new StageRef(1, "tile"), 49, 0, 16, 8, 64, 64)], findings);
    }

    [Fact]
    public void A_tile_touching_the_far_edges_of_its_source_fits()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 64, height: 64));
        builder.Add(CutStage("tile", height, tileWidth: 16, left: 48, top: 56));

        var pipeline = PipelineOf(builder.Build());

        Assert.Equal(new PixelPoint(48, 56), Assert.IsType<CutTile>(pipeline.Stages[1]).Parameters.Position);
    }

    // cut-tile requirement: The fit is checked against a valid source's output size.

    [Fact]
    public void The_size_comes_through_a_threshold()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 64, height: 64));
        var terrain = builder.Add(ThresholdStage("terrain", height));
        builder.Add(CutStage("tile", terrain, tileWidth: 128));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new TileOutsideSource(new StageRef(2, "tile"), 0, 0, 128, 64, 64, 64)], findings);
    }

    [Fact]
    public void A_bad_source_is_not_reported_again()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 0));
        builder.Add(CutStage("tile", height, tileWidth: 64));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new OutOfRange(new StageRef(0, "height"), Parameter.Width, 0, PixelSizeRange)], findings);
    }

    /// <summary>
    /// The second box fits inside the 128 by 128 noise but not inside the first cut's 64 by 32 box,
    /// so a size taken from the wrong stage would let it through.
    /// </summary>
    [Fact]
    public void A_cut_reading_a_cut_is_sized_by_the_first_cuts_box()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 128, height: 128));
        var tile = builder.Add(CutStage("tile", height, tileWidth: 64));
        builder.Add(CutStage("again", tile, tileWidth: 64, left: 0, top: 16));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new TileOutsideSource(new StageRef(2, "again"), 0, 16, 64, 32, 64, 32)], findings);
    }

    [Fact]
    public void A_cut_may_read_a_cut_whose_box_it_fits_inside()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 128, height: 128));
        var tile = builder.Add(CutStage("tile", height, tileWidth: 64, left: 32, top: 48));
        builder.Add(CutStage("again", tile, tileWidth: 32, left: 16, top: 8));

        var pipeline = PipelineOf(builder.Build());

        var again = Assert.IsType<CutTile>(pipeline.Stages[2]);
        Assert.Equal(StageId.Create("tile"), again.Parameters.Source);
        Assert.Equal(new PixelPoint(16, 8), again.Parameters.Position);
    }

    [Fact]
    public void Within_a_cut_tile_stage_the_id_comes_first_then_the_source_the_tile_width_left_and_top()
    {
        var findings = FindingsOf(Describe(Stage("height"), CutStage("", null!, tileWidth: 2, left: -1, top: 4096)));

        var at = new StageRef(1, "");
        Assert.Equal(
            [
                new InvalidStageId(at),
                new MissingInput(at, Input.Source),
                new OutOfRange(at, Parameter.TileWidth, 2, TileWidthRange),
                new OutOfRange(at, Parameter.Left, -1, PositionRange),
                new OutOfRange(at, Parameter.Top, 4096, PositionRange),
            ],
            findings);
    }

    [Fact]
    public void Within_a_cut_tile_stage_a_width_that_is_not_a_multiple_of_four_comes_before_left_and_top()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height"));
        builder.Add(CutStage("tile", height, tileWidth: 30, left: -1, top: -1));

        var findings = FindingsOf(builder.Build());

        var at = new StageRef(1, "tile");
        Assert.Equal(
            [
                new NotMultipleOf(at, Parameter.TileWidth, 30, 4),
                new OutOfRange(at, Parameter.Left, -1, PositionRange),
                new OutOfRange(at, Parameter.Top, -1, PositionRange),
            ],
            findings);
    }

    /// <summary>
    /// The fit is checked last. A bad id doesn't stop it, because the id and the box are separate
    /// mistakes: here the box is larger than its source, and both are reported, the id first.
    /// </summary>
    [Fact]
    public void A_tile_outside_its_source_is_reported_after_an_invalid_id()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 32, height: 32));
        builder.Add(CutStage("", height, tileWidth: 64));

        var findings = FindingsOf(builder.Build());

        var at = new StageRef(1, "");
        Assert.Equal([new InvalidStageId(at), new TileOutsideSource(at, 0, 0, 64, 32, 32, 32)], findings);
    }

    [Fact]
    public void A_tile_outside_its_source_is_reported_after_a_repeated_id()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 32, height: 32));
        builder.Add(CutStage("height", height, tileWidth: 64));

        var findings = FindingsOf(builder.Build());

        var at = new StageRef(1, "height");
        Assert.Equal([new DuplicateStageId(at, 0), new TileOutsideSource(at, 0, 0, 64, 32, 32, 32)], findings);
    }

    /// <summary>
    /// A bad value does stop the fit check, so the value isn't reported twice: a 66-wide tile is
    /// larger than its 32 by 32 source, and only the width is reported.
    /// </summary>
    [Fact]
    public void A_tile_outside_its_source_is_not_reported_when_the_tile_width_has_a_finding()
    {
        var builder = new PipelineBuilder();
        var height = builder.Add(Stage("height", width: 32, height: 32));
        builder.Add(CutStage("tile", height, tileWidth: 66));

        var findings = FindingsOf(builder.Build());

        Assert.Equal([new NotMultipleOf(new StageRef(1, "tile"), Parameter.TileWidth, 66, 4)], findings);
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
