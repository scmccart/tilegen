using Xunit;

namespace Tilegen.Scripting.CSharp.Tests;

public class PlaceholderTests
{
    // Placeholder: Microsoft.Testing.Platform fails a test project that runs no tests
    // (exit code 8). The first real test in this project replaces it.
    [Fact]
    public void Scripting_CSharp_tests_run_against_Tilegen_Scripting_CSharp()
    {
        Assert.Equal("Tilegen.Scripting.CSharp", typeof(ScriptingMarker).Assembly.GetName().Name);
    }
}
