using Tilegen.Core.Replay;
using Xunit;

namespace Tilegen.Core.Tests.Replay;

/// <summary>
/// One test per scenario in specs/frame-playback, in the spec's order. The spec counts frames
/// from 1; indexes here count from 0, so "the 3rd frame" is <c>FrameAt(2)</c>.
/// </summary>
public class PlaybackScenarioTests
{
    private static readonly TimeSpan Interval = Playback.FrameInterval;

    // Requirement: Playback is in one of three modes.

    [Fact]
    public void Following_shows_the_newest_frame_as_the_timeline_grows()
    {
        var playback = new FollowingLatest();

        var withTwo = PlaybackRules.Shown(playback, 2);
        var withThree = PlaybackRules.Shown(playback, 3);

        Assert.Equal(new FrameAt(1), withTwo);
        Assert.Equal(new FrameAt(2), withThree);
    }

    [Fact]
    public void Nothing_is_shown_before_the_first_frame()
    {
        var shown = PlaybackRules.Shown(new FollowingLatest(), 0);

        Assert.Equal(new NoFrame(), shown);
    }

    // Requirement: Pausing holds a frame.

    [Fact]
    public void Pausing_while_following_holds_the_newest_frame_as_the_timeline_grows()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Pause, 4);

        var withFour = PlaybackRules.Shown(playback, 4);
        var withSix = PlaybackRules.Shown(playback, 6);

        Assert.Equal(new FrameAt(3), withFour);
        Assert.Equal(new FrameAt(3), withSix);
    }

    // Requirement: Stepping moves one frame and pauses.

    [Fact]
    public void Stepping_back_while_following_pauses_at_the_frame_before_the_newest()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.StepBack, 5);

        Assert.Equal(new Paused(3), playback);
    }

    [Fact]
    public void Stepping_forward_while_following_pauses_at_the_newest_frame()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.StepForward, 5);

        Assert.Equal(new Paused(4), playback);
    }

    [Fact]
    public void Stepping_past_either_end_leaves_the_shown_frame_unchanged()
    {
        var atFirst = PlaybackRules.Apply(new Paused(0), PlaybackCommand.StepBack, 5);
        var atLast = PlaybackRules.Apply(new Paused(4), PlaybackCommand.StepForward, 5);

        Assert.Equal(new FrameAt(0), PlaybackRules.Shown(atFirst, 5));
        Assert.Equal(new FrameAt(4), PlaybackRules.Shown(atLast, 5));
    }

    // Requirement: Jumping to the first or latest frame.

    [Fact]
    public void Jumping_to_the_first_frame_pauses_there_and_jumping_to_the_latest_follows_again()
    {
        var afterFirst = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.First, 5);
        var afterLatest = PlaybackRules.Apply(afterFirst, PlaybackCommand.Latest, 5);

        Assert.Equal(new Paused(0), afterFirst);
        Assert.Equal(new FollowingLatest(), afterLatest);
        Assert.Equal(new FrameAt(4), PlaybackRules.Shown(afterLatest, 5));
    }

    // Requirement: Playing advances at a fixed interval.

    [Fact]
    public void Elapsed_time_moves_the_frame_and_carries_the_remainder()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Play, 10);

        playback = PlaybackRules.Advance(playback, Interval, 10);
        playback = PlaybackRules.Advance(playback, Interval * 0.75, 10);
        playback = PlaybackRules.Advance(playback, Interval * 0.75, 10);

        Assert.Equal(new Playing(2, Interval / 2), playback);
        Assert.Equal(new FrameAt(2), PlaybackRules.Shown(playback, 10));
    }

    [Fact]
    public void The_update_rate_does_not_change_the_shown_frame()
    {
        Playback start = new Playing(0, TimeSpan.Zero);
        var sixtiethOfASecond = TimeSpan.FromTicks(166_667);
        const int updates = 37;

        var once = PlaybackRules.Advance(start, sixtiethOfASecond * updates, 10);
        var many = start;
        for (var i = 0; i < updates; i++)
        {
            many = PlaybackRules.Advance(many, sixtiethOfASecond, 10);
        }

        Assert.Equal(PlaybackRules.Shown(once, 10), PlaybackRules.Shown(many, 10));
        Assert.Equal(once, many);
    }

    [Fact]
    public void Playing_onto_the_newest_frame_switches_to_following()
    {
        var playback = PlaybackRules.Advance(new Playing(8, TimeSpan.Zero), Interval * 1.5, 10);

        Assert.Equal(new FollowingLatest(), playback);
        Assert.Equal(new FrameAt(9), PlaybackRules.Shown(playback, 10));
    }

    // Requirement: Play starts from where playback is.

    [Fact]
    public void Playing_a_completed_run_while_following_replays_from_the_first_frame()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Play, 10);

        Assert.Equal(new Playing(0, TimeSpan.Zero), playback);
    }

    [Fact]
    public void Playing_a_single_frame_follows_it_because_there_is_nothing_to_play()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Play, 1);

        Assert.Equal(new FollowingLatest(), playback);
        Assert.Equal(new FrameAt(0), PlaybackRules.Shown(playback, 1));
    }

    [Fact]
    public void Playing_while_paused_resumes_from_the_paused_frame()
    {
        var playback = PlaybackRules.Apply(new Paused(2), PlaybackCommand.Play, 6);

        Assert.Equal(new Playing(2, TimeSpan.Zero), playback);
    }

    // Requirement: Controls wait for the first frame.

    [Theory]
    [InlineData(PlaybackCommand.Play)]
    [InlineData(PlaybackCommand.Pause)]
    [InlineData(PlaybackCommand.StepBack)]
    [InlineData(PlaybackCommand.StepForward)]
    [InlineData(PlaybackCommand.First)]
    [InlineData(PlaybackCommand.Latest)]
    public void A_control_before_the_first_frame_keeps_following_so_the_first_frame_is_shown(PlaybackCommand command)
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), command, 0);

        Assert.Equal(new FollowingLatest(), playback);
        Assert.Equal(new FrameAt(0), PlaybackRules.Shown(playback, 1));
    }

    // Requirement: Time comes only from the caller.

    [Fact]
    public void No_elapsed_time_does_not_move_a_playing_frame()
    {
        var playing = new Playing(3, Interval / 2);

        var playback = PlaybackRules.Advance(playing, TimeSpan.Zero, 10);

        Assert.Equal(playing, playback);
        Assert.Equal(new FrameAt(3), PlaybackRules.Shown(playback, 10));
    }
}
