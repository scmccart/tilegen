namespace Tilegen.Engine;

/// <summary>
/// Where a <see cref="GenerationRun"/> is: still running, or ended by completing or by being
/// stopped. One closed type, so a run is always in exactly one of the three, and switches over it
/// name every case (railway R2, illegal-states R4). Stopping is not an error (railway R8).
/// </summary>
public closed record class RunState;

/// <summary>The run has stages left to step. Each advance adds a frame, or completes the run.</summary>
public sealed record class Running : RunState;

/// <summary>Every stage ran out of frames. The timeline holds them all, and advancing does nothing.</summary>
public sealed record class Completed : RunState;

/// <summary>The run was stopped before it completed. The frames already kept stay, and advancing does nothing.</summary>
public sealed record class Stopped : RunState;
