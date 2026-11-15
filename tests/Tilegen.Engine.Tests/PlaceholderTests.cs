using Xunit;

namespace Tilegen.Engine.Tests;

public class PlaceholderTests
{
    // Placeholder: Microsoft.Testing.Platform fails a test project that runs no tests
    // (exit code 8). The first real test in this project replaces it.
    [Fact]
    public void Engine_tests_run_against_Tilegen_Engine()
    {
        Assert.Equal("Tilegen.Engine", typeof(EngineMarker).Assembly.GetName().Name);
    }
}
