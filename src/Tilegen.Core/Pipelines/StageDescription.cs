using System.Collections.Immutable;

namespace Tilegen.Core.Pipelines;

/// <summary>
/// One stage as a definition wrote it: plain values, no behaviour (illegal-states R2). Validation
/// parses it into a <see cref="Stage"/>. Every stage has an id, so it is on the base record and is
/// read without a switch over the technique. It is the id as written: it may be invalid or null.
/// </summary>
public closed record class StageDescription(string Id);

public sealed record class NoiseDescription(
    string Id, int Width, int Height, int Octaves, int CellSize, double Persistence) : StageDescription(Id);

/// <summary>
/// A threshold stage as written: the stage it reads, through the handle a builder gave for it, and
/// its level. The source may be null, or a handle that doesn't fit this description; validation
/// reports both.
/// </summary>
public sealed record class ThresholdDescription(string Id, StageHandle Source, int Level) : StageDescription(Id);

/// <summary>A pipeline as a definition wrote it. Nothing here has been checked.</summary>
public sealed record class PipelineDescription(ImmutableArray<StageDescription> Stages);
