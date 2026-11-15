using System.Text.Json;
using Tilegen.TestKit.Goldens;
using Xunit;

namespace Tilegen.Core.Tests.TestKit;

public sealed class ProvenanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tilegen-provenance-tests-" + Guid.NewGuid().ToString("N"));

    public ProvenanceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Provenance_is_read_from_its_json_file()
    {
        var path = Write("""
            {
              "technique": "value-noise",
              "parameters": { "width": 32, "octaves": 3, "scale": 0.25 },
              "stageSeed": 12345,
              "dotnet": "11.0",
              "approvedIn": "pipeline-spine"
            }
            """);

        var provenance = Golden.ReadProvenance(path);

        Assert.Equal("value-noise", provenance.Technique);
        Assert.Equal(12345, provenance.StageSeed);
        Assert.Equal("11.0", provenance.Dotnet);
        Assert.Equal("pipeline-spine", provenance.ApprovedIn);
        Assert.Equal(JsonValueKind.Object, provenance.Parameters.ValueKind);
        Assert.Equal(3, provenance.Parameters.GetProperty("octaves").GetInt32());
        Assert.Equal(0.25, provenance.Parameters.GetProperty("scale").GetDouble());
    }

    [Fact]
    public void Provenance_missing_a_field_is_rejected_naming_the_file()
    {
        var path = Write("""{ "technique": "value-noise", "stageSeed": 1, "dotnet": "11.0", "approvedIn": "x" }""");

        var exception = Assert.Throws<InvalidDataException>(() => Golden.ReadProvenance(path));

        Assert.Contains(path, exception.Message);
    }

    private string Write(string json)
    {
        var path = Path.Combine(_root, "sample.golden.json");
        File.WriteAllText(path, json);
        return path;
    }
}
