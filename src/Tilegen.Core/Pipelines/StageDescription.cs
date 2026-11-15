using System.Collections.Immutable;

namespace Tilegen.Core.Pipelines;

/// <summary>
/// One stage as a definition wrote it: plain values, no behaviour (illegal-states R2). Validation
/// parses it into a <see cref="Stage"/>.
/// </summary>
public closed record class StageDescription;

public sealed record class NoiseDescription(
    string Id, int Width, int Height, int Octaves, int CellSize, double Persistence) : StageDescription;

/// <summary>A pipeline as a definition wrote it. Nothing here has been checked.</summary>
public sealed record class PipelineDescription(ImmutableArray<StageDescription> Stages);
