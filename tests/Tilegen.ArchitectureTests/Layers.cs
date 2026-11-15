using System.Reflection;
using Tilegen.Core;
using Tilegen.Engine;
using Tilegen.Export;
using Tilegen.Scripting.CSharp;

namespace Tilegen.ArchitectureTests;

/// <summary>
/// The five layer assemblies under test. Each is found through a type it declares, so a rename
/// breaks the build rather than emptying a test.
/// </summary>
internal static class Layers
{
    public const string Core = "Tilegen.Core";
    public const string Engine = "Tilegen.Engine";
    public const string ScriptingCSharp = "Tilegen.Scripting.CSharp";
    public const string Export = "Tilegen.Export";
    public const string Desktop = "Tilegen.Desktop";

    private static readonly IReadOnlyDictionary<string, Assembly> ByName = new Dictionary<string, Assembly>
    {
        [Core] = typeof(CoreMarker).Assembly,
        [Engine] = typeof(EngineMarker).Assembly,
        [ScriptingCSharp] = typeof(ScriptingMarker).Assembly,
        [Export] = typeof(ExportMarker).Assembly,
        [Desktop] = typeof(Tilegen.Desktop.Program).Assembly,
    };

    public static IEnumerable<string> All => ByName.Keys;

    /// <summary>The assembly whose name, and root namespace, is <paramref name="name"/>.</summary>
    public static Assembly Get(string name) => ByName[name];
}
