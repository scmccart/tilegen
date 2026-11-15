namespace Tilegen.Scripting.CSharp;

/// <summary>
/// Exists so that the Scripting.CSharp assembly and its dependency on Engine are real for the
/// architecture tests. It may be deleted once real types make that dependency.
/// </summary>
public static class ScriptingMarker
{
    public static Type Inner => typeof(Tilegen.Engine.GenerationRun);
}
