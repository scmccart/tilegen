using Tilegen.Core.Imaging;

namespace Tilegen.TestKit.Goldens;

/// <summary>
/// How a technique's output compared with its golden. A missing or different golden is an expected
/// outcome, so it is a case here rather than an exception (testing R15); only
/// <see cref="Golden.AssertMatches(GoldenComparison, GoldenProvenance?)"/> turns one into a throw.
/// </summary>
public closed record class GoldenComparison;

/// <summary>The decoded golden and the actual buffer have the same size and the same pixels.</summary>
public sealed record class Matches : GoldenComparison;

/// <summary>There is no golden PNG. The actual image was written for the owner to look at.</summary>
public sealed record class Missing(string ActualPath) : GoldenComparison;

/// <summary>The golden and the actual buffer differ in size. Both images were written.</summary>
public sealed record class SizeDiffers(
    int ExpectedWidth,
    int ExpectedHeight,
    int ActualWidth,
    int ActualHeight,
    string ActualPath,
    string ExpectedPath) : GoldenComparison;

/// <summary>
/// The sizes agree and <paramref name="Count"/> pixels differ. The first, in row-major order, is at
/// (<paramref name="FirstX"/>, <paramref name="FirstY"/>). Both images were written.
/// </summary>
public sealed record class PixelsDiffer(
    int Count,
    int FirstX,
    int FirstY,
    Rgba32 Expected,
    Rgba32 Actual,
    string ActualPath,
    string ExpectedPath) : GoldenComparison;
