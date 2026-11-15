using CsCheck;
using Tilegen.Core.Replay;
using Xunit;

namespace Tilegen.Core.Tests.Replay;

/// <summary>
/// Properties of <see cref="PlaybackRules"/>. No playback value is built directly: each one is
/// reached the way a run reaches it, by playing a generated run of events from a new run's state
/// (following the latest frame, no frames). Frames are added, controls are pressed and time passes,
/// and the frame count only grows, from 0 to <see cref="MaxFrames"/> (design D4, precedence).
/// </summary>
public class PlaybackProperties
{
    private const int Iterations = 1000;
    private const int MaxFrames = 50;

    private static readonly long IntervalTicks = Playback.FrameInterval.Ticks;

    /// <summary>One thing that happens during a run.</summary>
    private closed record class Event;

    /// <summary>The run yields this many frames, stopping at <see cref="MaxFrames"/>.</summary>
    private sealed record class Grow(int Frames) : Event;

    private sealed record class Press(PlaybackCommand Command) : Event;

    private sealed record class Wait(TimeSpan Elapsed) : Event;

    /// <summary>
    /// Elapsed times up to a few intervals, with whole intervals and one tick either side of them
    /// picked often, and now and then a long pause that plays past the newest frame.
    /// </summary>
    private static readonly Gen<TimeSpan> Elapsed = Gen.Frequency<long>(
            (4, Gen.Long[0, 3 * IntervalTicks]),
            (1, Gen.Int[0, 4].Select(n => n * IntervalTicks)),
            (1, Gen.Int[1, 4].Select(n => (n * IntervalTicks) - 1)),
            (1, Gen.Int[0, 4].Select(n => (n * IntervalTicks) + 1)),
            (1, Gen.Long[0, 60 * TimeSpan.TicksPerSecond]))
        .Select(TimeSpan.FromTicks);

    /// <summary>Play is picked more often than the other commands, so that many reached states are playing.</summary>
    private static readonly Gen<PlaybackCommand> Commands = Gen.Frequency<PlaybackCommand>(
        (3, Gen.Const(PlaybackCommand.Play)), (5, Gen.Enum<PlaybackCommand>()));

    private static readonly Gen<Event> Events = Gen.Frequency<Event>(
        (2, Gen.Int[1, 5].Select(frames => (Event)new Grow(frames))),
        (3, Commands.Select(command => (Event)new Press(command))),
        (4, Elapsed.Select(elapsed => (Event)new Wait(elapsed))));

    private static readonly Gen<Event[]> Runs = Events.Array[0, 40];

    /// <summary>Runs in which no frame is ever added, so every state is on an empty timeline.</summary>
    private static readonly Gen<Event[]> RunsWithNoFrames =
        Runs.Select(events => events.Where(e => e is not Grow).ToArray());

    /// <summary>Every state along the run, starting with a new run's.</summary>
    private static IEnumerable<(Playback Playback, int FrameCount)> StatesAlong(Event[] events)
    {
        Playback playback = new FollowingLatest();
        var frameCount = 0;
        yield return (playback, frameCount);

        foreach (var e in events)
        {
            (playback, frameCount) = e switch
            {
                Grow grow => (playback, Math.Min(frameCount + grow.Frames, MaxFrames)),
                Press press => (PlaybackRules.Apply(playback, press.Command, frameCount), frameCount),
                Wait wait => (PlaybackRules.Advance(playback, wait.Elapsed, frameCount), frameCount),
            };
            yield return (playback, frameCount);
        }
    }

    [Fact]
    public void The_shown_frame_is_no_frame_exactly_when_the_timeline_is_empty_and_is_otherwise_on_it()
    {
        Runs.Sample(
            events =>
            {
                foreach (var (playback, frameCount) in StatesAlong(events))
                {
                    var shown = PlaybackRules.Shown(playback, frameCount);

                    var correct = shown switch
                    {
                        NoFrame => frameCount == 0,
                        FrameAt frame => frame.Index >= 0 && frame.Index < frameCount,
                    };
                    Assert.True(correct, $"{playback} on {frameCount} frames shows {shown}.");
                }
            },
            iter: Iterations);
    }

    [Fact]
    public void Splitting_elapsed_time_over_updates_gives_the_same_playback_as_one_update()
    {
        Gen.Select(Runs, Elapsed.Array[1, 10]).Sample(
            (events, parts) =>
            {
                var total = parts.Aggregate(TimeSpan.Zero, (sum, part) => sum + part);

                foreach (var (playback, frameCount) in StatesAlong(events))
                {
                    var once = PlaybackRules.Advance(playback, total, frameCount);
                    var split = parts.Aggregate(
                        playback, (current, part) => PlaybackRules.Advance(current, part, frameCount));

                    Assert.Equal(once, split);
                }
            },
            iter: Iterations);
    }

    [Fact]
    public void Any_command_on_an_empty_timeline_gives_following_the_latest_frame()
    {
        Gen.Select(RunsWithNoFrames, Gen.Enum<PlaybackCommand>()).Sample(
            (events, command) =>
            {
                foreach (var (playback, frameCount) in StatesAlong(events))
                {
                    var after = PlaybackRules.Apply(playback, command, frameCount);

                    Assert.Equal(new FollowingLatest(), after);
                }
            },
            iter: Iterations);
    }

    [Fact]
    public void Advancing_by_zero_elapsed_time_changes_nothing()
    {
        Runs.Sample(
            events =>
            {
                foreach (var (playback, frameCount) in StatesAlong(events))
                {
                    var after = PlaybackRules.Advance(playback, TimeSpan.Zero, frameCount);

                    Assert.Equal(playback, after);
                }
            },
            iter: Iterations);
    }

    /// <summary>
    /// D4's precedence: no transition produces playing at the newest frame, and growth only moves
    /// the newest frame further on.
    /// </summary>
    [Fact]
    public void A_reached_playing_frame_is_always_before_the_newest()
    {
        Runs.Sample(
            events =>
            {
                foreach (var (playback, frameCount) in StatesAlong(events))
                {
                    var beforeNewest = playback switch
                    {
                        Playing playing => playing.Index < frameCount - 1,
                        FollowingLatest or Paused => true,
                    };

                    Assert.True(beforeNewest, $"{playback} on {frameCount} frames.");
                }
            },
            iter: Iterations);
    }
}
