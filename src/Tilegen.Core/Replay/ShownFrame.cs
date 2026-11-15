namespace Tilegen.Core.Replay;

/// <summary>Which frame to draw. No frame is a case of its own, not a null or a −1 (illegal-states R10).</summary>
public closed record class ShownFrame;

/// <summary>The timeline is empty, so there is nothing to draw.</summary>
public sealed record class NoFrame : ShownFrame;

/// <summary>Draw the frame at this 0-based position on the timeline.</summary>
public sealed record class FrameAt(int Index) : ShownFrame;
