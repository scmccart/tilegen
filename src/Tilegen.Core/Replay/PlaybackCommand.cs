namespace Tilegen.Core.Replay;

/// <summary>A playback control the user pressed. <see cref="PlaybackRules.Apply"/> gives its effect.</summary>
public enum PlaybackCommand
{
    Play,
    Pause,
    StepBack,
    StepForward,
    First,
    Latest,
}
