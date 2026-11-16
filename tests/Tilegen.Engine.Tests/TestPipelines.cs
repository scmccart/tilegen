using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Seeding;
using Tilegen.Core.Techniques;
using Tilegen.TestKit;
using Xunit;

namespace Tilegen.Engine.Tests;

/// <summary>
/// Builds the pipelines the Engine tests run, and the frames to compare them with. Pipelines are
/// built with a <see cref="PipelineBuilder"/>, as a definition builds them, and go through
/// <see cref="Pipeline.Validate"/>, the only way to get a <see cref="ValidatedPipeline"/>.
/// </summary>
internal static class TestPipelines
{
    /// <summary>A small noise stage: one frame per octave. Cell size 8 is valid up to 4 octaves.</summary>
    public static NoiseDescription Noise(string id, int octaves, int size = 16) =>
        new(id, size, size, octaves, CellSize: 8, Persistence: 0.5);

    /// <summary>The stages, added to a builder in order, built and validated.</summary>
    public static ValidatedPipeline Validated(params StageDescription[] stages)
    {
        var builder = new PipelineBuilder();
        foreach (var stage in stages)
        {
            builder.Add(stage);
        }

        return Validated(builder);
    }

    /// <summary>
    /// A noise stage <c>a</c> followed by a threshold stage <c>t</c> that reads it through the
    /// handle the builder gave for <c>a</c>.
    /// </summary>
    public static ValidatedPipeline NoiseThenThreshold(int octaves, int size = 16, int level = 128)
    {
        var builder = new PipelineBuilder();
        var a = builder.Add(Noise("a", octaves, size));
        builder.Add(new ThresholdDescription("t", a, level));
        return Validated(builder);
    }

    private static ValidatedPipeline Validated(PipelineBuilder builder) =>
        Assert.IsType<Valid>(Pipeline.Validate(builder.Build())).Pipeline;

    /// <summary>Advances until the run completes, within a limit so a bug can't hang the test.</summary>
    public static void RunToCompletion(GenerationRun run)
    {
        for (var advances = 0; advances < 1000 && run.State is Running; advances++)
        {
            run.Advance();
        }

        Assert.IsType<Completed>(run.State);
    }

    public static IReadOnlyList<TimelineEntry> Entries(Timeline timeline) =>
        Enumerable.Range(0, timeline.Count).Select(index => timeline[index]).ToList();

    /// <summary>
    /// The frames of <paramref name="stage"/>'s technique run alone, with no other stage's output
    /// and the stage seed derived from <paramref name="seed"/> and the stage's id, each copied as it
    /// was yielded. For a stage that reads no input.
    /// </summary>
    public static IReadOnlyList<PixelBuffer> RunAlone(Stage stage, RunSeed seed) =>
        Pixels.Snapshots(
            Generation.Run(stage, StageOutputs.Empty, new Random(StageSeed.Derive(seed, stage.Id).Value)));

    /// <summary>
    /// The frames of the threshold technique run alone on <paramref name="source"/>, with
    /// <paramref name="threshold"/>'s parameters and the stage seed derived from
    /// <paramref name="seed"/> and its id, each copied as it was yielded.
    /// </summary>
    public static IReadOnlyList<PixelBuffer> RunAlone(Threshold threshold, Image source, RunSeed seed) =>
        Pixels.Snapshots(
            TerrainThreshold.Run(
                source, threshold.Parameters, new Random(StageSeed.Derive(seed, threshold.Id).Value)));

    /// <summary>Null when the entries' frames equal the buffers pixel for pixel, in order; otherwise why not.</summary>
    public static string? Difference(IReadOnlyList<PixelBuffer> expected, IEnumerable<TimelineEntry> entries)
    {
        var actual = entries.Select(entry => entry.Image.ToBuffer()).ToList();
        if (expected.Count != actual.Count)
        {
            return $"Expected {expected.Count} frames, found {actual.Count}.";
        }

        return expected.Zip(actual)
            .Select((pair, index) => Pixels.Difference(pair.First, pair.Second) is { } difference
                ? $"Frame {index}: {difference}"
                : null)
            .FirstOrDefault(difference => difference is not null);
    }
}
