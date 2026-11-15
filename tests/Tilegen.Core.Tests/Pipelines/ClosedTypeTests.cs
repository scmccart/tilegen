using System.Collections.Immutable;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Techniques;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

/// <summary>
/// The switches below have no discard arm, so they compile only while each closed type's cases
/// are all named (CS8509 is an error). A new case breaks this file's build until it is handled.
/// </summary>
public class ClosedTypeTests
{
    private static readonly NoiseParameters Parameters = NoiseParameters.Create(
        PixelSize.Create(8), PixelSize.Create(8), Octaves.Create(2), CellSize.Create(4), Persistence.Create(0.5));

    private static readonly StageRef Ref = new(0, "a");

    private static string Name(StageDescription description) => description switch
    {
        NoiseDescription => nameof(NoiseDescription),
    };

    private static string Name(Stage stage) => stage switch
    {
        Noise => nameof(Noise),
    };

    private static string Name(ValidationOutcome outcome) => outcome switch
    {
        Valid => nameof(Valid),
        Invalid => nameof(Invalid),
    };

    private static string Name(Finding finding) => finding switch
    {
        EmptyPipeline => nameof(EmptyPipeline),
        InvalidStageId => nameof(InvalidStageId),
        DuplicateStageId => nameof(DuplicateStageId),
        OutOfRange => nameof(OutOfRange),
        FinestOctaveBelowOnePixel => nameof(FinestOctaveBelowOnePixel),
    };

    private static string Name(Parameter parameter) => parameter switch
    {
        Parameter.Width => "w",
        Parameter.Height => "h",
        Parameter.Octaves => "o",
        Parameter.CellSize => "c",
        Parameter.Persistence => "p",
    };

    private static ValidatedPipeline OneStagePipeline() =>
        new(ImmutableArray.Create<Stage>(new Noise(StageId.Create("a"), Parameters)));

    [Fact]
    public void A_switch_over_stage_descriptions_needs_no_discard_arm()
    {
        Assert.Equal("NoiseDescription", Name(new NoiseDescription("a", 8, 8, 2, 4, 0.5)));
    }

    [Fact]
    public void A_switch_over_stages_needs_no_discard_arm()
    {
        Assert.Equal("Noise", Name(new Noise(StageId.Create("a"), Parameters)));
    }

    [Fact]
    public void A_switch_over_validation_outcomes_needs_no_discard_arm()
    {
        var valid = new Valid(OneStagePipeline());
        var invalid = new Invalid(ImmutableArray.Create<Finding>(new EmptyPipeline()));

        Assert.Equal("Valid", Name(valid));
        Assert.Equal("Invalid", Name(invalid));
    }

    [Fact]
    public void A_switch_over_findings_needs_no_discard_arm()
    {
        var range = new ParameterRange(1, true, 4096);

        Assert.Equal("EmptyPipeline", Name(new EmptyPipeline()));
        Assert.Equal("InvalidStageId", Name(new InvalidStageId(Ref)));
        Assert.Equal("DuplicateStageId", Name(new DuplicateStageId(Ref, 0)));
        Assert.Equal("OutOfRange", Name(new OutOfRange(Ref, Parameter.Width, 0, range)));
        Assert.Equal("FinestOctaveBelowOnePixel", Name(new FinestOctaveBelowOnePixel(Ref, 1, 4, 8)));
    }

    [Fact]
    public void A_switch_over_parameters_needs_no_discard_arm()
    {
        Assert.Equal(["w", "h", "o", "c", "p"], Enum.GetValues<Parameter>().Select(Name));
    }

    [Fact]
    public void An_invalid_outcome_refuses_an_empty_list_of_findings()
    {
        Assert.Throws<ArgumentException>(() => new Invalid(ImmutableArray<Finding>.Empty));
    }

    [Fact]
    public void An_invalid_outcome_refuses_a_default_list_of_findings()
    {
        Assert.Throws<ArgumentException>(() => new Invalid(default));
    }

    [Fact]
    public void An_invalid_outcome_keeps_its_findings_in_order()
    {
        var first = new EmptyPipeline();
        var second = new InvalidStageId(Ref);

        var invalid = new Invalid(ImmutableArray.Create<Finding>(first, second));

        Assert.Equal(new Finding[] { first, second }, invalid.Findings);
    }

    [Fact]
    public void A_validated_pipeline_keeps_its_stages()
    {
        var pipeline = OneStagePipeline();

        var stage = Assert.Single(pipeline.Stages);
        Assert.Equal(new Noise(StageId.Create("a"), Parameters), stage);
    }

    [Fact]
    public void A_validated_pipeline_refuses_no_stages()
    {
        Assert.Throws<ArgumentException>(() => new ValidatedPipeline(ImmutableArray<Stage>.Empty));
    }

    [Fact]
    public void Findings_with_the_same_data_are_equal()
    {
        Assert.Equal(new InvalidStageId(Ref), new InvalidStageId(new StageRef(0, "a")));
        Assert.NotEqual<Finding>(new InvalidStageId(Ref), new DuplicateStageId(Ref, 0));
    }
}
