using Tilegen.Core.Replay;
using Xunit;

namespace Tilegen.Core.Tests.Replay;

/// <summary>
/// Every cell of design D4's transition table, including the empty-timeline row, then the
/// <see cref="PlaybackRules.Advance"/> rules and the index check. Where a cell has a condition or a
/// clamp, both sides are tested. Most tests use 5 frames, so the last index is 4.
/// </summary>
public class PlaybackRulesTests
{
    private const int Frames = 5;
    private const int Last = Frames - 1;

    private static readonly TimeSpan Interval = Playback.FrameInterval;
    private static readonly TimeSpan SomeCarry = TimeSpan.FromMilliseconds(100);

    // From FollowingLatest.

    [Fact]
    public void Play_while_following_plays_from_the_first_frame()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Play, Frames);

        Assert.Equal(new Playing(0, TimeSpan.Zero), playback);
    }

    [Fact]
    public void Play_while_following_a_single_frame_changes_nothing()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Play, 1);

        Assert.Equal(new FollowingLatest(), playback);
    }

    [Fact]
    public void Pause_while_following_pauses_at_the_last_frame()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Pause, Frames);

        Assert.Equal(new Paused(Last), playback);
    }

    [Theory]
    [InlineData(Frames, Last - 1)]
    [InlineData(1, 0)]
    public void Step_back_while_following_pauses_before_the_last_frame_but_not_before_the_first(
        int frameCount, int expected)
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.StepBack, frameCount);

        Assert.Equal(new Paused(expected), playback);
    }

    [Fact]
    public void Step_forward_while_following_pauses_at_the_last_frame()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.StepForward, Frames);

        Assert.Equal(new Paused(Last), playback);
    }

    [Fact]
    public void First_while_following_pauses_at_the_first_frame()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.First, Frames);

        Assert.Equal(new Paused(0), playback);
    }

    [Fact]
    public void Latest_while_following_changes_nothing()
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Latest, Frames);

        Assert.Equal(new FollowingLatest(), playback);
    }

    // From Paused(i).

    [Fact]
    public void Play_while_paused_plays_from_the_paused_frame()
    {
        var playback = PlaybackRules.Apply(new Paused(2), PlaybackCommand.Play, Frames);

        Assert.Equal(new Playing(2, TimeSpan.Zero), playback);
    }

    [Fact]
    public void Play_while_paused_at_the_last_frame_plays_from_the_first()
    {
        var playback = PlaybackRules.Apply(new Paused(Last), PlaybackCommand.Play, Frames);

        Assert.Equal(new Playing(0, TimeSpan.Zero), playback);
    }

    [Fact]
    public void Play_while_paused_on_a_single_frame_follows_the_latest_frame()
    {
        var playback = PlaybackRules.Apply(new Paused(0), PlaybackCommand.Play, 1);

        Assert.Equal(new FollowingLatest(), playback);
    }

    [Fact]
    public void Pause_while_paused_changes_nothing()
    {
        var playback = PlaybackRules.Apply(new Paused(2), PlaybackCommand.Pause, Frames);

        Assert.Equal(new Paused(2), playback);
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(0, 0)]
    public void Step_back_while_paused_moves_back_one_frame_and_stops_at_the_first(int index, int expected)
    {
        var playback = PlaybackRules.Apply(new Paused(index), PlaybackCommand.StepBack, Frames);

        Assert.Equal(new Paused(expected), playback);
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(Last, Last)]
    public void Step_forward_while_paused_moves_on_one_frame_and_stops_at_the_last(int index, int expected)
    {
        var playback = PlaybackRules.Apply(new Paused(index), PlaybackCommand.StepForward, Frames);

        Assert.Equal(new Paused(expected), playback);
    }

    [Fact]
    public void First_while_paused_pauses_at_the_first_frame()
    {
        var playback = PlaybackRules.Apply(new Paused(3), PlaybackCommand.First, Frames);

        Assert.Equal(new Paused(0), playback);
    }

    [Fact]
    public void Latest_while_paused_follows_the_latest_frame()
    {
        var playback = PlaybackRules.Apply(new Paused(2), PlaybackCommand.Latest, Frames);

        Assert.Equal(new FollowingLatest(), playback);
    }

    // From Playing(i, c). A reachable Playing is always before the last frame (D4, precedence).

    [Fact]
    public void Play_while_playing_changes_nothing()
    {
        var playback = PlaybackRules.Apply(new Playing(2, SomeCarry), PlaybackCommand.Play, Frames);

        Assert.Equal(new Playing(2, SomeCarry), playback);
    }

    [Fact]
    public void Pause_while_playing_pauses_at_the_playing_frame_and_drops_the_carry()
    {
        var playback = PlaybackRules.Apply(new Playing(2, SomeCarry), PlaybackCommand.Pause, Frames);

        Assert.Equal(new Paused(2), playback);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(0, 0)]
    public void Step_back_while_playing_pauses_one_frame_back_and_stops_at_the_first(int index, int expected)
    {
        var playback = PlaybackRules.Apply(new Playing(index, SomeCarry), PlaybackCommand.StepBack, Frames);

        Assert.Equal(new Paused(expected), playback);
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(Last - 1, Last)]
    public void Step_forward_while_playing_pauses_one_frame_on(int index, int expected)
    {
        var playback = PlaybackRules.Apply(new Playing(index, SomeCarry), PlaybackCommand.StepForward, Frames);

        Assert.Equal(new Paused(expected), playback);
    }

    [Fact]
    public void First_while_playing_pauses_at_the_first_frame()
    {
        var playback = PlaybackRules.Apply(new Playing(2, SomeCarry), PlaybackCommand.First, Frames);

        Assert.Equal(new Paused(0), playback);
    }

    [Fact]
    public void Latest_while_playing_follows_the_latest_frame()
    {
        var playback = PlaybackRules.Apply(new Playing(2, SomeCarry), PlaybackCommand.Latest, Frames);

        Assert.Equal(new FollowingLatest(), playback);
    }

    // On an empty timeline: only FollowingLatest is a valid input, and every command keeps it.

    [Theory]
    [InlineData(PlaybackCommand.Play)]
    [InlineData(PlaybackCommand.Pause)]
    [InlineData(PlaybackCommand.StepBack)]
    [InlineData(PlaybackCommand.StepForward)]
    [InlineData(PlaybackCommand.First)]
    [InlineData(PlaybackCommand.Latest)]
    public void Every_command_on_an_empty_timeline_keeps_following_the_latest_frame(PlaybackCommand command)
    {
        var playback = PlaybackRules.Apply(new FollowingLatest(), command, 0);

        Assert.Equal(new FollowingLatest(), playback);
    }

    // Advance.

    [Fact]
    public void Time_does_not_move_playback_that_is_following()
    {
        var playback = PlaybackRules.Advance(new FollowingLatest(), Interval * 3, Frames);

        Assert.Equal(new FollowingLatest(), playback);
    }

    [Fact]
    public void Time_does_not_move_playback_on_an_empty_timeline()
    {
        var playback = PlaybackRules.Advance(new FollowingLatest(), Interval * 3, 0);

        Assert.Equal(new FollowingLatest(), playback);
    }

    [Fact]
    public void Time_does_not_move_playback_that_is_paused()
    {
        var playback = PlaybackRules.Advance(new Paused(2), Interval * 3, Frames);

        Assert.Equal(new Paused(2), playback);
    }

    [Fact]
    public void Time_short_of_an_interval_only_adds_to_the_carry()
    {
        var justShort = Interval - SomeCarry - TimeSpan.FromTicks(1);

        var playback = PlaybackRules.Advance(new Playing(1, SomeCarry), justShort, Frames);

        Assert.Equal(new Playing(1, Interval - TimeSpan.FromTicks(1)), playback);
    }

    [Fact]
    public void Time_that_completes_an_interval_moves_one_frame_and_keeps_the_rest()
    {
        var playback = PlaybackRules.Advance(new Playing(1, SomeCarry), Interval, Frames);

        Assert.Equal(new Playing(2, SomeCarry), playback);
    }

    [Fact]
    public void Time_of_several_whole_intervals_moves_that_many_frames_with_no_carry()
    {
        var playback = PlaybackRules.Advance(new Playing(0, TimeSpan.Zero), Interval * 3, 10);

        Assert.Equal(new Playing(3, TimeSpan.Zero), playback);
    }

    [Fact]
    public void Time_that_reaches_exactly_the_last_frame_follows_the_latest()
    {
        var playback = PlaybackRules.Advance(new Playing(Last - 2, TimeSpan.Zero), Interval * 2, Frames);

        Assert.Equal(new FollowingLatest(), playback);
    }

    [Fact]
    public void Time_far_past_the_last_frame_follows_the_latest_without_overflowing()
    {
        var playback = PlaybackRules.Advance(new Playing(0, Interval - TimeSpan.FromTicks(1)), TimeSpan.MaxValue, Frames);

        Assert.Equal(new FollowingLatest(), playback);
    }

    // An index at or past the frame count is a bug, checked before anything else.

    [Theory]
    [InlineData(Frames, Frames)]
    [InlineData(Frames + 1, Frames)]
    [InlineData(0, 0)]
    public void Showing_a_paused_index_off_the_timeline_throws(int index, int frameCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlaybackRules.Shown(new Paused(index), frameCount));
    }

    [Fact]
    public void Showing_a_playing_index_off_the_timeline_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Shown(new Playing(Frames, TimeSpan.Zero), Frames));
    }

    [Fact]
    public void A_command_on_an_index_off_the_timeline_throws_even_when_the_command_would_drop_the_index()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Apply(new Paused(Frames), PlaybackCommand.Latest, Frames));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Apply(new Playing(Frames, TimeSpan.Zero), PlaybackCommand.Latest, Frames));
    }

    [Fact]
    public void A_command_on_an_empty_timeline_throws_for_any_index()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Apply(new Paused(0), PlaybackCommand.Latest, 0));
    }

    [Fact]
    public void Advancing_an_index_off_the_timeline_throws_even_when_time_would_not_move_it()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Advance(new Paused(Frames), TimeSpan.Zero, Frames));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Advance(new Playing(Frames, TimeSpan.Zero), TimeSpan.Zero, Frames));
    }

    // Inputs the design leaves out, treated as bugs.

    [Fact]
    public void A_negative_frame_count_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlaybackRules.Shown(new FollowingLatest(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Apply(new FollowingLatest(), PlaybackCommand.Play, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Advance(new FollowingLatest(), TimeSpan.Zero, -1));
    }

    [Fact]
    public void Negative_elapsed_time_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackRules.Advance(new Playing(0, TimeSpan.Zero), TimeSpan.FromTicks(-1), Frames));
    }
}
