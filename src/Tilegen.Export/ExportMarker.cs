namespace Tilegen.Export;

/// <summary>
/// Exists so that the Export assembly and its dependency on Engine are real for the architecture
/// tests. It may be deleted once real types make that dependency.
/// </summary>
public static class ExportMarker
{
    public static Type Inner => typeof(Tilegen.Engine.EngineMarker);
}
