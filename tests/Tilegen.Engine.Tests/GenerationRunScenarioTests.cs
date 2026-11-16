using Tilegen.Core.Pipelines;
using Tilegen.Core.Seeding;
using Tilegen.TestKit;
using Xunit;
using static Tilegen.Engine.Tests.TestPipelines;

namespace Tilegen.Engine.Tests;

/// <summary>
/// One test per scenario in specs/generation-run, in the spec's order. Frames are compared, pixel
/// for pixel, with each stage's technique run alone: through <c>Generation.Run</c> for a stage
/// with no input, and through <c>TerrainThreshold.Run</c> on a kept image for a threshold.
/// </summary>
public class GenerationRunScenarioTests
{
    // Requirement: Only a validated pipeline is run.

    [Fact]
    public void A_validated_pipeline_starts_a_running_run_with_an_empty_timeline()
    {
        var pipeline = Validated(Noise("a", octaves: 3));

        var run = GenerationRun.Start(pipeline, new RunSeed(1));

        Assert.IsType<Running>(run.State);
        Assert.Equal(0, run.Timeline.Count);
        Assert.Equal(new RunSeed(1), run.Seed);
    }

    // Requirement: Stages run in pipeline order.

    [Fact]
    public void Two_stages_run_one_after_the_other()
    {
        var run = GenerationRun.Start(Validated(Noise("a", octaves: 3), Noise("b", octaves: 2)), new RunSeed(1));

        RunToCompletion(run);

        Assert.Equal(["a", "a", "a", "b", "b"], Entries(run.Timeline).Select(entry => entry.Stage.Value));
    }

    // Requirement: Each stage draws from its own seeded randomness.

    [Fact]
    public void A_stages_frames_match_its_technique_run_alone()
    {
        var seed = new RunSeed(7);
        var pipeline = Validated(Noise("a", octaves: 3), Noise("b", octaves: 2));
        var run = GenerationRun.Start(pipeline, seed);

        RunToCompletion(run);

        var stageB = pipeline.Stages[1];
        var framesOfB = Entries(run.Timeline).Where(entry => entry.Stage == stageB.Id);
        Assert.Null(Difference(RunAlone(stageB, seed), framesOfB));
    }

    [Fact]
    public void Another_stage_does_not_change_a_stages_frames()
    {
        var seed = new RunSeed(7);
        var afterA = GenerationRun.Start(Validated(Noise("a", octaves: 3), Noise("b", octaves: 2)), seed);
        var afterC = GenerationRun.Start(Validated(Noise("c", octaves: 4, size: 24), Noise("b", octaves: 2)), seed);

        RunToCompletion(afterA);
        RunToCompletion(afterC);

        var framesOfB = Entries(afterA.Timeline).Where(entry => entry.Stage.Value == "b").ToList();
        var framesOfBAfterC = Entries(afterC.Timeline).Where(entry => entry.Stage.Value == "b");
        Assert.Equal(2, framesOfB.Count);
        Assert.Null(Difference(framesOfB.Select(entry => entry.Image.ToBuffer()).ToList(), framesOfBAfterC));
    }

    [Fact]
    public void A_stage_with_an_input_matches_its_technique_run_alone_on_that_input()
    {
        var seed = new RunSeed(7);
        var pipeline = NoiseThenThreshold(octaves: 3, size: 64);
        var run = GenerationRun.Start(pipeline, seed);

        RunToCompletion(run);

        var stageA = pipeline.Stages[0];
        var stageT = Assert.IsType<Threshold>(pipeline.Stages[1]);
        var entries = Entries(run.Timeline);
        var lastOfA = entries.Last(entry => entry.Stage == stageA.Id);
        var framesOfT = entries.Where(entry => entry.Stage == stageT.Id);
        Assert.Null(Difference(RunAlone(stageT, lastOfA.Image, seed), framesOfT));
    }

    // Requirement: A run advances one step at a time.

    [Fact]
    public void Each_advance_adds_one_frame()
    {
        var run = GenerationRun.Start(Validated(Noise("a", octaves: 3)), new RunSeed(1));

        var counts = new List<int>();
        for (var advance = 0; advance < 3; advance++)
        {
            run.Advance();
            counts.Add(run.Timeline.Count);
        }

        Assert.Equal([1, 2, 3], counts);
        Assert.IsType<Running>(run.State);
    }

    [Fact]
    public void The_advance_after_the_last_frame_completes_the_run()
    {
        var run = GenerationRun.Start(Validated(Noise("a", octaves: 3)), new RunSeed(1));
        run.Advance();
        run.Advance();
        run.Advance();

        run.Advance();

        Assert.IsType<Completed>(run.State);
        Assert.Equal(3, run.Timeline.Count);
    }

    [Fact]
    public void Advancing_a_completed_run_does_nothing()
    {
        var run = GenerationRun.Start(Validated(Noise("a", octaves: 3)), new RunSeed(1));
        RunToCompletion(run);
        var before = Entries(run.Timeline);

        run.Advance();

        Assert.IsType<Completed>(run.State);
        var after = Entries(run.Timeline);
        Assert.Equal(before.Count, after.Count);
        Assert.All(before.Zip(after), pair => Assert.Same(pair.First, pair.Second));
    }

    // Requirement: A run can be stopped.

    [Fact]
    public void Stopping_partway_through_keeps_the_frames_and_adds_no_more()
    {
        var run = GenerationRun.Start(Validated(Noise("a", octaves: 4)), new RunSeed(1));
        run.Advance();
        run.Advance();

        run.Stop();
        var countWhenStopped = run.Timeline.Count;
        run.Advance();

        Assert.IsType<Stopped>(run.State);
        Assert.Equal(2, countWhenStopped);
        Assert.Equal(2, run.Timeline.Count);
    }

    [Fact]
    public void Stopping_a_completed_run_leaves_it_completed()
    {
        var run = GenerationRun.Start(Validated(Noise("a", octaves: 3)), new RunSeed(1));
        RunToCompletion(run);

        run.Stop();

        Assert.IsType<Completed>(run.State);
        Assert.Equal(3, run.Timeline.Count);
    }

    // Requirement: The timeline keeps every frame with its place in the run.

    [Fact]
    public void Entries_name_their_stage_and_step()
    {
        var run = GenerationRun.Start(Validated(Noise("a", octaves: 2), Noise("b", octaves: 2)), new RunSeed(1));

        RunToCompletion(run);

        Assert.Equal(
            [("a", 1), ("a", 2), ("b", 1), ("b", 2)],
            Entries(run.Timeline).Select(entry => (entry.Stage.Value, entry.Step)));
    }

    [Fact]
    public void Kept_frames_survive_later_steps()
    {
        var seed = new RunSeed(1);
        var pipeline = Validated(Noise("a", octaves: 3));
        var run = GenerationRun.Start(pipeline, seed);

        // Compared only once the run has completed: a frame kept without a copy would by then show
        // the last step's pixels.
        RunToCompletion(run);

        Assert.Null(Difference(RunAlone(pipeline.Stages[0], seed), Entries(run.Timeline)));
    }

    // Requirement: A run is deterministic.

    [Fact]
    public void The_same_pipeline_and_seed_give_the_same_timeline()
    {
        var pipeline = Validated(Noise("a", octaves: 3), Noise("b", octaves: 2));
        var first = GenerationRun.Start(pipeline, new RunSeed(42));
        var second = GenerationRun.Start(pipeline, new RunSeed(42));

        RunToCompletion(first);
        RunToCompletion(second);

        var firstEntries = Entries(first.Timeline);
        var secondEntries = Entries(second.Timeline);
        Assert.Equal(
            firstEntries.Select(entry => (entry.Stage, entry.Step)),
            secondEntries.Select(entry => (entry.Stage, entry.Step)));
        Assert.Null(Difference(firstEntries.Select(entry => entry.Image.ToBuffer()).ToList(), secondEntries));
    }

    [Fact]
    public void A_different_run_seed_changes_the_frames()
    {
        var pipeline = Validated(Noise("a", octaves: 3, size: 64));
        var withOne = GenerationRun.Start(pipeline, new RunSeed(1));
        var withTwo = GenerationRun.Start(pipeline, new RunSeed(2));

        RunToCompletion(withOne);
        RunToCompletion(withTwo);

        var lastWithOne = withOne.Timeline[withOne.Timeline.Count - 1].Image.ToBuffer();
        var lastWithTwo = withTwo.Timeline[withTwo.Timeline.Count - 1].Image.ToBuffer();
        Assert.NotNull(Pixels.Difference(lastWithOne, lastWithTwo));
    }

    // Requirement: A stage receives its sources' last kept frames.
    //
    // The stage set is closed, so no stand-in technique can record what it was given. What the
    // threshold was given is observed through its frame: the threshold of each of a's entries is
    // computed, and only the last one's matches.

    [Fact]
    public void A_threshold_reads_the_last_octave()
    {
        var seed = new RunSeed(7);
        var pipeline = NoiseThenThreshold(octaves: 3, size: 64);
        var run = GenerationRun.Start(pipeline, seed);

        RunToCompletion(run);

        var stageT = Assert.IsType<Threshold>(pipeline.Stages[1]);
        var entries = Entries(run.Timeline);
        var entriesOfA = entries.Where(entry => entry.Stage == pipeline.Stages[0].Id).ToList();
        var framesOfT = entries.Where(entry => entry.Stage == stageT.Id).ToList();
        Assert.Equal(3, entriesOfA.Count);
        Assert.Equal(3, entriesOfA[2].Step);

        // The first two octaves would give a different map, so a match with the third shows which
        // entry the threshold was given.
        Assert.Null(Difference(RunAlone(stageT, entriesOfA[2].Image, seed), framesOfT));
        Assert.NotNull(Difference(RunAlone(stageT, entriesOfA[0].Image, seed), framesOfT));
        Assert.NotNull(Difference(RunAlone(stageT, entriesOfA[1].Image, seed), framesOfT));
    }

    // Requirement: A stage leaves its sources' kept frames unchanged.

    [Fact]
    public void The_sources_entries_survive_the_reader()
    {
        var run = GenerationRun.Start(NoiseThenThreshold(octaves: 3, size: 64), new RunSeed(7));

        // Three advances keep a's three frames, and the threshold hasn't started: it starts on the
        // fourth advance, when a has no frame left.
        run.Advance();
        run.Advance();
        run.Advance();
        var entriesBeforeT = Entries(run.Timeline);
        Assert.Equal(["a", "a", "a"], entriesBeforeT.Select(entry => entry.Stage.Value));
        var snapshots = entriesBeforeT.Select(entry => entry.Image.ToBuffer()).ToList();

        RunToCompletion(run);

        var entries = Entries(run.Timeline);
        Assert.Equal(["a", "a", "a", "t"], entries.Select(entry => entry.Stage.Value));
        Assert.Null(Difference(snapshots, entries.Take(3)));
    }
}
