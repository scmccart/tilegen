using Xunit;

namespace Tilegen.Core.Tests;

public class PlaceholderTests
{
    // Placeholder: Microsoft.Testing.Platform fails a test project that runs no tests
    // (exit code 8). The first real test in this project replaces it.
    [Fact]
    public void Core_tests_run_against_Tilegen_Core()
    {
        Assert.Equal("Tilegen.Core", typeof(CoreMarker).Assembly.GetName().Name);
    }
}
