namespace Tilegen.TestKit.Goldens;

/// <summary>
/// Thrown by <see cref="Golden.AssertMatches(GoldenComparison, GoldenProvenance?)"/> for anything
/// but a match. The test kit doesn't reference xUnit, and xUnit reports any exception as a failure.
/// </summary>
public sealed class GoldenMismatchException(string message, GoldenComparison comparison) : Exception(message)
{
    /// <summary>The non-matching comparison the message describes.</summary>
    public GoldenComparison Comparison { get; } = comparison;
}
