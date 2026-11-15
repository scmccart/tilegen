namespace Tilegen.Core.Replay;

/// <summary>
/// Every playback decision, as pure functions of the playback, the timeline's frame count and, for
/// <see cref="Advance"/>, the elapsed time the caller passes in (functional-core R2, R3). The same
/// inputs always give the same result.
/// <para>
/// A paused or playing index at or past the frame count is a bug and throws, checked before
/// anything else. On an empty timeline, then, only <see cref="FollowingLatest"/> is a valid input.
/// No transition produces <see cref="Playing"/> at the last frame, and the timeline only grows
/// within a run, so a reachable <see cref="Playing"/> is always before the last frame.
/// </para>
/// </summary>
public static class PlaybackRules
{
    /// <summary>The frame to draw: the newest while following, or the held or playing frame.</summary>
    public static ShownFrame Shown(Playback playback, int frameCount)
    {
        CheckOnTimeline(playback, frameCount);

        return playback switch
        {
            FollowingLatest => frameCount == 0 ? new NoFrame() : new FrameAt(frameCount - 1),
            Paused paused => new FrameAt(paused.Index),
            Playing playing => new FrameAt(playing.Index),
        };
    }

    /// <summary>The playback after the user presses a control: design D4's transition table.</summary>
    public static Playback Apply(Playback playback, PlaybackCommand command, int frameCount)
    {
        CheckOnTimeline(playback, frameCount);

        // Only FollowingLatest gets past the check on an empty timeline, and no control moves it.
        if (frameCount == 0)
        {
            return playback;
        }

        var last = frameCount - 1;
        return playback switch
        {
            FollowingLatest following => command switch
            {
                PlaybackCommand.Play => last == 0 ? following : new Playing(0, TimeSpan.Zero),
                PlaybackCommand.Pause => new Paused(last),
                PlaybackCommand.StepBack => new Paused(Math.Max(last - 1, 0)),
                PlaybackCommand.StepForward => new Paused(last),
                PlaybackCommand.First => new Paused(0),
                PlaybackCommand.Latest => following,
            },
            Paused paused => command switch
            {
                PlaybackCommand.Play => last == 0
                    ? new FollowingLatest()
                    : new Playing(paused.Index == last ? 0 : paused.Index, TimeSpan.Zero),
                PlaybackCommand.Pause => paused,
                PlaybackCommand.StepBack => new Paused(Math.Max(paused.Index - 1, 0)),
                PlaybackCommand.StepForward => new Paused(Math.Min(paused.Index + 1, last)),
                PlaybackCommand.First => new Paused(0),
                PlaybackCommand.Latest => new FollowingLatest(),
            },
            Playing playing => command switch
            {
                PlaybackCommand.Play => playing,
                PlaybackCommand.Pause => new Paused(playing.Index),
                PlaybackCommand.StepBack => new Paused(Math.Max(playing.Index - 1, 0)),
                PlaybackCommand.StepForward => new Paused(Math.Min(playing.Index + 1, last)),
                PlaybackCommand.First => new Paused(0),
                PlaybackCommand.Latest => new FollowingLatest(),
            },
        };
    }

    /// <summary>
    /// The playback after <paramref name="elapsed"/> has passed. Only <see cref="Playing"/> moves:
    /// forward ⌊(carry + elapsed) / interval⌋ frames, keeping the remainder as the new carry, and
    /// following the latest frame once it reaches the last. Arithmetic is exact in ticks, so the
    /// same total elapsed time gives the same result however it is split into updates.
    /// </summary>
    public static Playback Advance(Playback playback, TimeSpan elapsed, int frameCount)
    {
        CheckOnTimeline(playback, frameCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        return playback switch
        {
            FollowingLatest following => following,
            Paused paused => paused,
            Playing playing => Move(playing, elapsed, last: frameCount - 1),
        };
    }

    private static Playback Move(Playing playing, TimeSpan elapsed, int last)
    {
        // Elapsed is divided by the interval before the carry is added, so a huge elapsed time
        // can't overflow: the carry plus the remainder is less than two intervals.
        var interval = Playback.FrameInterval.Ticks;
        var sinceLastMove = playing.Carry.Ticks + (elapsed.Ticks % interval);
        var frames = (elapsed.Ticks / interval) + (sinceLastMove / interval);
        var carry = sinceLastMove % interval;

        return frames >= last - playing.Index
            ? new FollowingLatest()
            : new Playing(playing.Index + (int)frames, TimeSpan.FromTicks(carry));
    }

    /// <summary>
    /// Within a run the timeline only grows and a new run resets playback, so an index at or past
    /// the frame count means the caller passed the wrong count or a stale playback.
    /// </summary>
    private static void CheckOnTimeline(Playback playback, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(playback);
        ArgumentOutOfRangeException.ThrowIfNegative(frameCount);

        var offTimeline = playback switch
        {
            FollowingLatest => false,
            Paused paused => paused.Index >= frameCount,
            Playing playing => playing.Index >= frameCount,
        };

        if (offTimeline)
        {
            throw new ArgumentOutOfRangeException(
                nameof(playback), playback, $"The index is not on a timeline of {frameCount} frames.");
        }
    }
}
