namespace Tilegen.Core.Replay;

/// <summary>
/// Which frame of a timeline is shown, and whether time moves it. Always exactly one of three
/// modes, so no pair of flags can disagree (illegal-states R4). Indexes are 0-based positions on
/// the timeline. <see cref="PlaybackRules"/> holds every transition.
/// </summary>
public closed record class Playback
{
    /// <summary>How long each frame is shown while playing. A Core constant, because it decides what is drawn when.</summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(250);
}

/// <summary>Showing the newest frame, which changes as frames are added. A new run starts here.</summary>
public sealed record class FollowingLatest : Playback;

/// <summary>Holding one frame. Frames added later don't change what is shown.</summary>
public sealed record class Paused(int Index) : Playback
{
    /// <summary>Declared get-only, so a <c>with</c> expression can't skip the check.</summary>
    public int Index { get; } = Index >= 0
        ? Index
        : throw new ArgumentOutOfRangeException(nameof(Index), Index, "A paused index is not negative.");
}

/// <summary>
/// Moving forward one frame per <see cref="Playback.FrameInterval"/> of elapsed time.
/// <see cref="Carry"/> is the time since the last move, from zero up to, but not including, one interval.
/// </summary>
public sealed record class Playing(int Index, TimeSpan Carry) : Playback
{
    /// <summary>Declared get-only, so a <c>with</c> expression can't skip the check.</summary>
    public int Index { get; } = Index >= 0
        ? Index
        : throw new ArgumentOutOfRangeException(nameof(Index), Index, "A playing index is not negative.");

    /// <summary>Declared get-only, so a <c>with</c> expression can't skip the check.</summary>
    public TimeSpan Carry { get; } = Carry >= TimeSpan.Zero && Carry < FrameInterval
        ? Carry
        : throw new ArgumentOutOfRangeException(
            nameof(Carry), Carry, "A carry is at least zero and less than one frame interval.");
}
