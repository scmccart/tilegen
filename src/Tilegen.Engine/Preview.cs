using Tilegen.Core.Pipelines;
using Tilegen.Core.Replay;
using Tilegen.Core.Seeding;

namespace Tilegen.Engine;

/// <summary>
/// The current run of a validated pipeline and the playback over its timeline: the engine state
/// the window reads each frame, so the window keeps none of its own (ports-and-adapters R11).
/// Every method forwards to the run or to <see cref="PlaybackRules"/>; it decides nothing itself.
/// </summary>
public sealed class Preview
{
    private readonly ValidatedPipeline _pipeline;

    /// <summary>Time since the run's last step, from zero up to, but not including, one frame interval.</summary>
    private TimeSpan _paceCarry = TimeSpan.Zero;

    /// <summary>Starts a run of <paramref name="pipeline"/> with <paramref name="seed"/>, following the latest frame.</summary>
    public Preview(ValidatedPipeline pipeline, RunSeed seed)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
        Run = GenerationRun.Start(pipeline, seed);
    }

    public GenerationRun Run { get; private set; }

    public Playback Playback { get; private set; } = new FollowingLatest();

    /// <summary>The frame to draw, as playback decides it for the timeline as it is now.</summary>
    public ShownFrame Shown => PlaybackRules.Shown(Playback, Run.Timeline.Count);

    /// <summary>
    /// Advances the run once for each step <see cref="Pacing.Steps"/> says <paramref name="elapsed"/>
    /// is worth, keeping the leftover time for the next update, and then advances playback by
    /// <paramref name="elapsed"/> over the timeline those steps left. So generating moves at the
    /// replay's rate, one frame per <see cref="Playback.FrameInterval"/>. A completed or stopped run
    /// doesn't change, so then only playback moves.
    /// </summary>
    public void Update(TimeSpan elapsed)
    {
        var paced = Pacing.Steps(_paceCarry, elapsed);
        _paceCarry = paced.Carry;
        for (var step = 0; step < paced.Steps; step++)
        {
            Run.Advance();
        }

        Playback = PlaybackRules.Advance(Playback, elapsed, Run.Timeline.Count);
    }

    /// <summary>Applies a playback control to the timeline as it is now.</summary>
    public void Apply(PlaybackCommand command) =>
        Playback = PlaybackRules.Apply(Playback, command, Run.Timeline.Count);

    /// <summary>Stops the run and starts a new one with the same seed, following the latest frame.</summary>
    public void Restart() => Begin(Run.Seed);

    /// <summary>Stops the run and starts a new one with the next seed (<see cref="RunSeed.Next"/>), following the latest frame.</summary>
    public void NextSeed() => Begin(Run.Seed.Next());

    /// <summary>A new run starts with a new, empty timeline, so playback and the pacing carry start again too.</summary>
    private void Begin(RunSeed seed)
    {
        Run.Stop();
        Run = GenerationRun.Start(_pipeline, seed);
        Playback = new FollowingLatest();
        _paceCarry = TimeSpan.Zero;
    }
}
