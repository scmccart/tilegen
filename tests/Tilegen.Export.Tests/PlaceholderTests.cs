using Xunit;

namespace Tilegen.Export.Tests;

public class PlaceholderTests
{
    // Placeholder: Microsoft.Testing.Platform fails a test project that runs no tests
    // (exit code 8). The first real test in this project replaces it.
    [Fact]
    public void Export_tests_run_against_Tilegen_Export()
    {
        Assert.Equal("Tilegen.Export", typeof(ExportMarker).Assembly.GetName().Name);
    }
}
