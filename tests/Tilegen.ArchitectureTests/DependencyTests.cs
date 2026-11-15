using NetArchTest.Rules;
using Xunit;

namespace Tilegen.ArchitectureTests;

/// <summary>
/// The dependency rule, checked in IL. NetArchTest matches a namespace whole segment by segment,
/// so <c>Tilegen.Scripting</c> covers <c>Tilegen.Scripting.CSharp</c>, and <c>System.IO</c> does not
/// cover <c>System.IObservable</c>. It sees only references the code uses, so an unused reference
/// passes here; <see cref="DeclaredReferenceTests"/> catches that.
/// </summary>
public class DependencyTests
{
    // functional-core.md and ports-and-adapters.md, Enforcement. MonoGame is listed beside
    // Microsoft.Xna.Framework because Apos.Gui brings MonoGame.Extended.
    public static TheoryData<string, string> Forbidden => new()
    {
        { Layers.Core, "Tilegen.Engine" },
        { Layers.Core, "Tilegen.Scripting" },
        { Layers.Core, "Tilegen.Export" },
        { Layers.Core, "Tilegen.Desktop" },
        { Layers.Core, "Microsoft.Xna.Framework" },
        { Layers.Core, "MonoGame" },
        { Layers.Core, "Apos" },
        { Layers.Core, "Microsoft.CodeAnalysis" },
        { Layers.Core, "System.IO" },

        { Layers.Engine, "Tilegen.Scripting" },
        { Layers.Engine, "Tilegen.Export" },
        { Layers.Engine, "Tilegen.Desktop" },
        { Layers.Engine, "Microsoft.Xna.Framework" },
        { Layers.Engine, "MonoGame" },
        { Layers.Engine, "Apos" },
        { Layers.Engine, "Microsoft.CodeAnalysis" },

        { Layers.ScriptingCSharp, "Tilegen.Export" },
        { Layers.ScriptingCSharp, "Tilegen.Desktop" },
        { Layers.ScriptingCSharp, "Microsoft.Xna.Framework" },
        { Layers.ScriptingCSharp, "MonoGame" },
        { Layers.ScriptingCSharp, "Apos" },

        { Layers.Export, "Tilegen.Scripting" },
        { Layers.Export, "Tilegen.Desktop" },
        { Layers.Export, "Microsoft.Xna.Framework" },
        { Layers.Export, "MonoGame" },
        { Layers.Export, "Apos" },
        { Layers.Export, "Microsoft.CodeAnalysis" },
    };

    // testing R17: each dependency the rule allows is also asserted, so the forbidden rows can't
    // pass because a type was renamed or moved away.
    public static TheoryData<string, string> Required => new()
    {
        { Layers.Engine, "Tilegen.Core" },
        { Layers.ScriptingCSharp, "Tilegen.Engine" },
        { Layers.Export, "Tilegen.Engine" },
        { Layers.Desktop, "Tilegen.Engine" },
        { Layers.Desktop, "Tilegen.Scripting.CSharp" },
        { Layers.Desktop, "Tilegen.Export" },
        { Layers.Desktop, "Microsoft.Xna.Framework" },
        { Layers.Desktop, "Apos" },
    };

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void Assembly_does_not_depend_on_the_forbidden_namespace(string assembly, string forbidden)
    {
        AssertApartFromOwnNamespace(assembly, forbidden);

        var result = Types.InAssembly(Layers.Get(assembly))
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly} must not depend on {forbidden}, but these types do: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(Required))]
    public void Assembly_depends_on_the_required_namespace(string assembly, string required)
    {
        AssertApartFromOwnNamespace(assembly, required);

        var dependents = Types.InAssembly(Layers.Get(assembly))
            .That().HaveDependencyOn(required)
            .GetTypes();

        Assert.True(
            dependents.Any(),
            $"No type in {assembly} depends on {required}, which the dependency rule expects (testing R17).");
    }

    // NetArchTest counts a type's use of itself as a dependency. A row whose namespace overlaps the
    // assembly's own would fail on every type if forbidden, and pass on nothing if required.
    private static void AssertApartFromOwnNamespace(string assembly, string ns)
    {
        var overlaps = ns == assembly
            || assembly.StartsWith(ns + ".", StringComparison.Ordinal)
            || ns.StartsWith(assembly + ".", StringComparison.Ordinal);

        Assert.False(overlaps, $"{ns} overlaps {assembly}'s own namespace, so this row checks nothing.");
    }
}
