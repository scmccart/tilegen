using Tilegen.Core.Replay;
using Xunit;

namespace Tilegen.Core.Tests.Replay;

/// <summary>
/// The switches below have no discard arm, so they compile only while each closed type's cases
/// are all named (CS8509 is an error). A new case breaks this file's build until it is handled.
/// The constructor checks rule out a negative index and a carry outside [0, interval).
/// </summary>
public class PlaybackTypeTests
{
    private static string Name(Playback playback) => playback switch
    {
        FollowingLatest => nameof(FollowingLatest),
        Paused => nameof(Paused),
        Playing => nameof(Playing),
    };

    private static string Name(ShownFrame shown) => shown switch
    {
        NoFrame => nameof(NoFrame),
        FrameAt => nameof(FrameAt),
    };

    private static string Name(PlaybackCommand command) => command switch
    {
        PlaybackCommand.Play => "play",
        PlaybackCommand.Pause => "pause",
        PlaybackCommand.StepBack => "back",
        PlaybackCommand.StepForward => "forward",
        PlaybackCommand.First => "first",
        PlaybackCommand.Latest => "latest",
    };

    [Fact]
    public void A_switch_over_playback_needs_no_discard_arm()
    {
        Assert.Equal("FollowingLatest", Name(new FollowingLatest()));
        Assert.Equal("Paused", Name(new Paused(0)));
        Assert.Equal("Playing", Name(new Playing(0, TimeSpan.Zero)));
    }

    [Fact]
    public void A_switch_over_shown_frames_needs_no_discard_arm()
    {
        Assert.Equal("NoFrame", Name(new NoFrame()));
        Assert.Equal("FrameAt", Name(new FrameAt(0)));
    }

    [Fact]
    public void A_switch_over_playback_commands_needs_no_discard_arm()
    {
        Assert.Equal(
            ["play", "pause", "back", "forward", "first", "latest"], Enum.GetValues<PlaybackCommand>().Select(Name));
    }

    [Fact]
    public void The_frame_interval_is_a_quarter_of_a_second()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(250), Playback.FrameInterval);
    }

    [Fact]
    public void A_paused_index_cannot_be_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Paused(-1));
    }

    [Fact]
    public void A_playing_index_cannot_be_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Playing(-1, TimeSpan.Zero));
    }

    [Fact]
    public void A_playing_carry_cannot_be_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Playing(0, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void A_playing_carry_is_less_than_one_frame_interval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Playing(0, Playback.FrameInterval));
    }

    [Fact]
    public void The_first_index_and_a_carry_just_under_the_interval_are_allowed()
    {
        var justUnder = Playback.FrameInterval - TimeSpan.FromTicks(1);

        var paused = new Paused(0);
        var playing = new Playing(0, justUnder);

        Assert.Equal(0, paused.Index);
        Assert.Equal((0, justUnder), (playing.Index, playing.Carry));
    }

    [Fact]
    public void Playback_values_with_the_same_data_are_equal()
    {
        Assert.Equal(new Paused(3), new Paused(3));
        Assert.Equal(new Playing(3, TimeSpan.FromTicks(5)), new Playing(3, TimeSpan.FromTicks(5)));
        Assert.Equal(new FollowingLatest(), new FollowingLatest());
        Assert.NotEqual<Playback>(new Paused(3), new Playing(3, TimeSpan.Zero));
    }
}
