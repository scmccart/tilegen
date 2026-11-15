namespace Tilegen.Engine;

/// <summary>
/// Exists so that the Engine assembly and its dependency on Core are real for the architecture
/// tests. It may be deleted once real types make that dependency.
/// </summary>
public static class EngineMarker
{
    public static Type Inner => typeof(Tilegen.Core.Imaging.PixelBuffer);
}
