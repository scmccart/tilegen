namespace Tilegen.Core.Replay;

/// <summary>
/// How many steps a run owes for some elapsed time, and the time left over: the result of
/// <see cref="Pacing.Steps"/>. <see cref="Carry"/> is from zero up to, but not including, one
/// <see cref="Playback.FrameInterval"/>, and is what the caller passes in next time.
/// </summary>
public readonly record struct Paced(int Steps, TimeSpan Carry);

/// <summary>
/// The pace of live generation: one run step for each whole <see cref="Playback.FrameInterval"/> of
/// elapsed time, the same interval playback uses, so generating and replaying move at the same
/// rate. A pure function of its arguments (functional-core R2, R3); the caller keeps the carry.
/// </summary>
public static class Pacing
{
    /// <summary>
    /// The steps owed after <paramref name="elapsed"/> has passed on top of <paramref name="carry"/>:
    /// ⌊(carry + elapsed) / interval⌋, with the remainder as the new carry. Arithmetic is exact in
    /// ticks, so the same total elapsed time gives the same steps and carry however it is split into
    /// updates. An elapsed time worth more steps than an <see cref="int"/> holds (over 17 years) is
    /// a bug and throws.
    /// </summary>
    public static Paced Steps(TimeSpan carry, TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(carry, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(carry, Playback.FrameInterval);
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        // Elapsed is divided by the interval before the carry is added, so a huge elapsed time
        // can't overflow: the carry plus the remainder is less than two intervals.
        var interval = Playback.FrameInterval.Ticks;
        var sinceLastStep = carry.Ticks + (elapsed.Ticks % interval);
        var steps = (elapsed.Ticks / interval) + (sinceLastStep / interval);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(steps, int.MaxValue, nameof(elapsed));

        return new Paced((int)steps, TimeSpan.FromTicks(sinceLastStep % interval));
    }
}
