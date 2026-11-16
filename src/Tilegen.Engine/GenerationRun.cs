using System.Collections.Immutable;
using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Seeding;

namespace Tilegen.Engine;

/// <summary>
/// Runs a validated pipeline with a run seed, one technique step per <see cref="Advance"/>, and
/// keeps a copy of every frame on its <see cref="Timeline"/>. Stages run in the pipeline's order,
/// and each is given its own <see cref="Random"/>, seeded from the stage seed Core derives
/// (functional-core R3, R9), and the last image of every stage that finished before it. Which of
/// those a stage reads is Core's choice (functional-core R2). The caller decides when to advance
/// and when to stop; the run does no work in between.
/// </summary>
public sealed class GenerationRun
{
    private readonly ImmutableArray<Stage> _stages;

    // The stage being stepped, its enumerator once the stage has started, and how many frames it
    // has yielded. The enumerator is null before a stage starts and after the run ends.
    private int _stageIndex;
    private IEnumerator<Frame>? _frames;
    private int _step;

    // The last image of every finished stage, passed to each stage as it starts.
    private StageOutputs _outputs = StageOutputs.Empty;

    private GenerationRun(ImmutableArray<Stage> stages, RunSeed seed)
    {
        _stages = stages;
        Seed = seed;
    }

    public RunSeed Seed { get; }

    public RunState State { get; private set; } = new Running();

    /// <summary>The frames kept so far. Readers can't change it; only this run appends.</summary>
    public Timeline Timeline { get; } = new();

    /// <summary>
    /// A running run with an empty timeline. Only a validated pipeline can be run
    /// (illegal-states R3). Nothing is generated until the first <see cref="Advance"/>.
    /// </summary>
    public static GenerationRun Start(ValidatedPipeline pipeline, RunSeed seed)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return new GenerationRun(pipeline.Stages, seed);
    }

    /// <summary>
    /// While running, adds exactly one frame to the timeline, or completes the run when no stage
    /// has a frame left, adding none. A completed or stopped run doesn't change.
    /// </summary>
    public void Advance()
    {
        State = State switch
        {
            Running running => Step(running),
            Completed completed => completed,
            Stopped stopped => stopped,
        };
    }

    /// <summary>
    /// Stops a running run: it no longer enumerates (functional-core R10), and the frames already
    /// kept stay. Stopping is not an error (railway R8). A completed or stopped run doesn't change.
    /// </summary>
    public void Stop()
    {
        State = State switch
        {
            Running => Halt(),
            Completed completed => completed,
            Stopped stopped => stopped,
        };
    }

    /// <summary>
    /// One step. A stage that has no frame left is finished in the same call: its last image joins
    /// the outputs, and the next stage is started, so an advance adds a frame whenever any stage
    /// still has one.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A stage ended without yielding a frame. Every technique yields at least one, so this is a bug.
    /// </exception>
    private RunState Step(Running running)
    {
        while (_stageIndex < _stages.Length)
        {
            var stage = _stages[_stageIndex];

            // The stage's Random is built when the stage starts, from the seed Core derives. Each
            // stage's stream is independent, so building it late changes nothing.
            _frames ??= Generation.Run(stage, _outputs, new Random(StageSeed.Derive(Seed, stage.Id).Value))
                .GetEnumerator();

            if (_frames.MoveNext())
            {
                // The yielded frame is a view valid only until the next step: keep an immutable
                // copy (functional-core R4).
                _step++;
                Timeline.Append(new TimelineEntry(stage.Id, _step, _frames.Current.ToImage()));
                return running;
            }

            ReleaseFrames();

            // The stage's last entry is the timeline's last, checked first so a stage with no
            // frame can't pass on the previous stage's image. The same image the timeline holds is
            // kept, with no further copy.
            if (_step == 0)
            {
                throw new InvalidOperationException($"Stage \"{stage.Id}\" ended without yielding a frame.");
            }

            _outputs = _outputs.With(stage.Id, Timeline[Timeline.Count - 1].Image);
            _stageIndex++;
            _step = 0;
        }

        return new Completed();
    }

    private Stopped Halt()
    {
        ReleaseFrames();
        return new Stopped();
    }

    /// <summary>
    /// Techniques hold nothing that needs releasing (functional-core R4), so disposing the
    /// enumerator is hygiene: it ends the iterator as soon as the run is done with it.
    /// </summary>
    private void ReleaseFrames()
    {
        _frames?.Dispose();
        _frames = null;
    }
}
