using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using Xunit;

namespace Tilegen.ArchitectureTests;

/// <summary>
/// Guards the dependency tests against passing on nothing: each assembly has types the rules can
/// see, and every type sits under the assembly's root namespace, which is what the rules match by.
/// </summary>
public class AntiVacuityTests
{
    public static TheoryData<string> Assemblies => new(Layers.All);

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void Rules_see_at_least_one_type_in_the_assembly(string assembly)
    {
        var types = Types.InAssembly(Layers.Get(assembly)).GetTypes();

        Assert.True(
            types.Any(),
            $"NetArchTest sees no type in {assembly}, so every dependency rule over it would pass vacuously.");
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void Every_type_lives_under_the_assembly_root_namespace(string assembly)
    {
        var strays = Layers.Get(assembly).GetTypes()
            .Where(type => !IsCompilerGenerated(type))
            .Where(type => !IsUnder(type.Namespace, assembly))
            .Select(type => type.FullName ?? type.Name)
            .ToList();

        Assert.True(
            strays.Count == 0,
            $"These types in {assembly} are outside the {assembly} namespace, so a rule naming "
                + $"{assembly} would not see a dependency on them: {string.Join(", ", strays)}");
    }

    private static bool IsUnder(string? ns, string root) =>
        ns is not null && (ns == root || ns.StartsWith(root + ".", StringComparison.Ordinal));

    // <Module>, <PrivateImplementationDetails>, closures and state machines, and attribute types the
    // compiler embeds when the framework lacks them. On net11.0 none is emitted today: the nullable
    // attributes come from the framework.
    private static bool IsCompilerGenerated(Type type) =>
        type.Name.StartsWith('<')
        || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || type.CustomAttributes.Any(
            attribute => attribute.AttributeType.FullName == "Microsoft.CodeAnalysis.EmbeddedAttribute");
}
