using Tilegen.Core.Replay;
using Tilegen.Core.Seeding;
using Xunit;
using static Tilegen.Engine.Tests.TestPipelines;

namespace Tilegen.Engine.Tests;

/// <summary>
/// <see cref="Preview"/> forwards to the run and to <see cref="PlaybackRules"/>. These tests check
/// what it forwards, and in which order; the playback rules themselves are tested in Core.
/// </summary>
public class PreviewTests
{
    private static readonly TimeSpan Interval = Playback.FrameInterval;

    public static TheoryData<PlaybackCommand> Commands => new(Enum.GetValues<PlaybackCommand>());

    [Fact]
    public void A_new_preview_follows_the_empty_timeline_of_a_new_run()
    {
        var preview = new Preview(Validated(Noise("a", octaves: 3)), new RunSeed(7));

        Assert.IsType<Running>(preview.Run.State);
        Assert.Equal(new RunSeed(7), preview.Run.Seed);
        Assert.Equal(0, preview.Run.Timeline.Count);
        Assert.Equal(new FollowingLatest(), preview.Playback);
        Assert.Equal(new NoFrame(), preview.Shown);
    }

    [Fact]
    public void Update_advances_the_run_one_step_and_then_playback()
    {
        var preview = new Preview(Validated(Noise("a", octaves: 4)), new RunSeed(1));
        preview.Update(TimeSpan.Zero);
        preview.Update(TimeSpan.Zero);
        preview.Apply(PlaybackCommand.First);
        preview.Apply(PlaybackCommand.Play);

        // Playing(0) on two frames. Stepping the run first makes three, so one interval moves
        // playback to the middle frame. Advancing playback first would reach the last of two
        // frames and follow the latest instead.
        preview.Update(Interval);

        Assert.Equal(3, preview.Run.Timeline.Count);
        Assert.Equal(new Playing(1, TimeSpan.Zero), preview.Playback);
        Assert.Equal(new FrameAt(1), preview.Shown);
    }

    [Fact]
    public void Update_on_a_completed_run_only_advances_playback()
    {
        // Updates until the run has ended, within a limit so a bug can't hang the test. A 3-octave
        // stage completes on its fourth update.
        var preview = new Preview(Validated(Noise("a", octaves: 3)), new RunSeed(1));
        var updatesToEnd = 0;
        while (updatesToEnd < 1000 && preview.Run.State is Running)
        {
            preview.Update(TimeSpan.Zero);
            updatesToEnd++;
        }

        preview.Apply(PlaybackCommand.First);
        preview.Apply(PlaybackCommand.Play);

        preview.Update(Interval);

        Assert.Equal(4, updatesToEnd);
        Assert.IsType<Completed>(preview.Run.State);
        Assert.Equal(3, preview.Run.Timeline.Count);
        Assert.Equal(new Playing(1, TimeSpan.Zero), preview.Playback);
    }

    [Fact]
    public void Restart_stops_the_run_and_starts_an_empty_one_with_the_same_seed_following_the_latest_frame()
    {
        var preview = new Preview(Validated(Noise("a", octaves: 3)), new RunSeed(7));
        preview.Update(TimeSpan.Zero);
        preview.Update(TimeSpan.Zero);
        preview.Apply(PlaybackCommand.Pause);
        var before = preview.Run;

        preview.Restart();

        Assert.IsType<Stopped>(before.State);
        Assert.NotSame(before, preview.Run);
        Assert.IsType<Running>(preview.Run.State);
        Assert.Equal(new RunSeed(7), preview.Run.Seed);
        Assert.Equal(0, preview.Run.Timeline.Count);
        Assert.Equal(new FollowingLatest(), preview.Playback);
    }

    [Fact]
    public void Next_seed_stops_the_run_and_starts_an_empty_one_with_the_next_seed_following_the_latest_frame()
    {
        var seed = new RunSeed(int.MaxValue);
        var preview = new Preview(Validated(Noise("a", octaves: 3)), seed);
        preview.Update(TimeSpan.Zero);
        preview.Apply(PlaybackCommand.Pause);
        var before = preview.Run;

        preview.NextSeed();

        Assert.IsType<Stopped>(before.State);
        Assert.IsType<Running>(preview.Run.State);
        Assert.Equal(seed.Next(), preview.Run.Seed);
        Assert.Equal(0, preview.Run.Timeline.Count);
        Assert.Equal(new FollowingLatest(), preview.Playback);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void Apply_gives_the_same_playback_as_the_core_rule_for_the_same_frame_count(PlaybackCommand command)
    {
        // Paused in the middle of three frames, where every command gives a different playback.
        var preview = new Preview(Validated(Noise("a", octaves: 4)), new RunSeed(1));
        for (var update = 0; update < 3; update++)
        {
            preview.Update(TimeSpan.Zero);
        }

        preview.Apply(PlaybackCommand.StepBack);
        var expected = PlaybackRules.Apply(preview.Playback, command, preview.Run.Timeline.Count);

        preview.Apply(command);

        Assert.Equal(expected, preview.Playback);
    }
}
