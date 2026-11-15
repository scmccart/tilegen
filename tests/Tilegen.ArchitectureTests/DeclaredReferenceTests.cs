using System.Text.Json;
using Xunit;

namespace Tilegen.ArchitectureTests;

/// <summary>
/// The references each project declares, read from the <c>obj/project.assets.json</c> NuGet writes
/// on restore, with <c>Directory.Build.props</c> already applied. The IL-based tests see only
/// references the code uses; these also see one that nothing uses yet.
/// </summary>
public class DeclaredReferenceTests
{
    private sealed record AllowedPackage(string Id, bool MustBePrivate);

    /// <summary>
    /// <paramref name="RequiredProjects"/> must all be declared; any declared project reference must
    /// be in <paramref name="AllowedProjects"/>, which includes the required ones.
    /// </summary>
    private sealed record Expectation(
        string Directory,
        string[] RequiredProjects,
        string[] AllowedProjects,
        AllowedPackage[] Packages);

    private static readonly AllowedPackage BannedApiAnalyzers =
        new("Microsoft.CodeAnalysis.BannedApiAnalyzers", MustBePrivate: true);

    // This mirrors the table in docs/architecture/ports-and-adapters.md ("How it applies here"):
    // a project may reference what its row allows, and must reference the layers the dependency
    // rule wires it to. A new package updates both in the same change. TestKit is not in the table:
    // it holds fakes of Engine's ports and golden helpers over Core values (testing.md). Its two
    // PNG packages decode goldens and write failure images; they are not private because tests
    // run their code, though the comparer exposes none of their types.
    private static readonly IReadOnlyDictionary<string, Expectation> Expected = new Dictionary<string, Expectation>
    {
        ["Tilegen.Core"] = new("src/Tilegen.Core", [], [], [BannedApiAnalyzers]),
        ["Tilegen.Engine"] = new("src/Tilegen.Engine", ["Tilegen.Core"], ["Tilegen.Core"], []),
        ["Tilegen.Scripting.CSharp"] = new(
            "src/Tilegen.Scripting.CSharp",
            ["Tilegen.Engine"],
            ["Tilegen.Engine", "Tilegen.Core"],
            [new("Microsoft.CodeAnalysis.CSharp", MustBePrivate: false)]),
        ["Tilegen.Export"] = new("src/Tilegen.Export", ["Tilegen.Engine"], ["Tilegen.Engine", "Tilegen.Core"], []),
        ["Tilegen.Desktop"] = new(
            "src/Tilegen.Desktop",
            ["Tilegen.Engine", "Tilegen.Scripting.CSharp", "Tilegen.Export"],
            ["Tilegen.Core", "Tilegen.Engine", "Tilegen.Scripting.CSharp", "Tilegen.Export"],
            [new("MonoGame.Framework.DesktopGL", MustBePrivate: false), new("Apos.Gui", MustBePrivate: false)]),
        ["Tilegen.TestKit"] = new(
            "tests/Tilegen.TestKit",
            ["Tilegen.Engine"],
            ["Tilegen.Engine", "Tilegen.Core"],
            [
                BannedApiAnalyzers,
                new("StbImageSharp", MustBePrivate: false),
                new("StbImageWriteSharp", MustBePrivate: false),
            ]),
    };

    public static TheoryData<string> Projects => new(Expected.Keys);

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_declares_its_required_project_references_and_no_others(string project)
    {
        var expected = Expected[project];
        var declared = ReadFrameworks(project, "restore")
            .SelectMany(framework => Properties(framework, "projectReferences"))
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var problems = declared
            .Except(expected.AllowedProjects, StringComparer.OrdinalIgnoreCase)
            .Select(reference => $"{project} references project {reference}, which it may not.")
            .Concat(expected.RequiredProjects
                .Except(declared, StringComparer.OrdinalIgnoreCase)
                .Select(reference => $"{project} is missing its project reference to {reference}."))
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_declares_only_allowed_packages(string project)
    {
        var allowed = Expected[project].Packages;
        var problems = new List<string>();

        foreach (var package in ReadFrameworks(project).SelectMany(framework => Properties(framework, "dependencies")))
        {
            var entry = allowed.FirstOrDefault(
                candidate => string.Equals(candidate.Id, package.Name, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                var allowedIds = allowed.Length == 0 ? "none" : string.Join(", ", allowed.Select(a => a.Id));
                problems.Add($"{project} references package {package.Name}, which it may not (allowed: {allowedIds}).");
                continue;
            }

            var suppressParent = package.Value.TryGetProperty("suppressParent", out var value) ? value.GetString() : null;
            if (entry.MustBePrivate && !string.Equals(suppressParent, "All", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add(
                    $"{project} references package {package.Name} without PrivateAssets=\"all\" "
                        + $"(suppressParent is {suppressParent ?? "unset"}), so it would flow to the projects that reference {project}.");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_declares_no_framework_reference_beyond_the_base_library(string project)
    {
        // NuGet adds Microsoft.NETCore.App itself, and leaves the node out when it has nothing to say.
        var problems = ReadFrameworks(project)
            .SelectMany(framework => Properties(framework, "frameworkReferences"))
            .Select(reference => reference.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => !string.Equals(name, "Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase))
            .Select(name => $"{project} references framework {name}, which it may not (the base library only).")
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // project.frameworks.<tfm> holds the direct packages and the framework references;
    // project.restore.frameworks.<tfm> holds the project references.
    private static List<JsonElement> ReadFrameworks(string project, string? section = null)
    {
        var path = Path.GetFullPath(Path.Combine(RepositoryRoot(), Expected[project].Directory, "obj", "project.assets.json"));
        if (!File.Exists(path))
        {
            Assert.Fail($"{path} does not exist. Build the solution first (dotnet build), so NuGet writes it.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement.GetProperty("project");

        var name = root.GetProperty("restore").GetProperty("projectName").GetString();
        Assert.True(name == project, $"{path} is the assets file of {name}, not of {project}.");

        var owner = section is null ? root : root.GetProperty(section);
        var frameworks = owner.GetProperty("frameworks").EnumerateObject().Select(f => f.Value.Clone()).ToList();
        Assert.True(frameworks.Count > 0, $"{path} lists no target framework, so there is nothing to check.");
        return frameworks;
    }

    // NuGet leaves a node out when it would be empty, as Engine's dependencies are.
    private static IEnumerable<JsonProperty> Properties(JsonElement framework, string node) =>
        framework.TryGetProperty(node, out var value) ? value.EnumerateObject() : [];

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tilegen.slnx")))
            {
                return directory.FullName;
            }
        }

        Assert.Fail($"No tilegen.slnx above {AppContext.BaseDirectory}, so the repository root can't be found.");
        return null!;
    }
}
