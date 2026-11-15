using System.Text.Json;
using Tilegen.Core.Imaging;

namespace Tilegen.TestKit.Goldens;

/// <summary>
/// Compares a technique's output with its approved golden image, and reports a mismatch with both
/// images written out for inspection. It compares decoded <see cref="Rgba32"/> values, never PNG
/// bytes (testing R4).
/// <para>
/// There is no record mode: nothing here writes into a goldens directory, under any flag or
/// environment variable. A new or changed golden is copied in by hand after the owner has looked at
/// it (testing R6).
/// </para>
/// </summary>
public static class Golden
{
    private static readonly JsonSerializerOptions ProvenanceOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    /// <summary>
    /// Where failure images of <paramref name="technique"/> are written by default:
    /// <c>golden-results/&lt;technique&gt;</c> in the test output, so same-named goldens of different
    /// techniques can't collide.
    /// </summary>
    public static string ResultsDirectory(string technique) =>
        Path.Combine(AppContext.BaseDirectory, "golden-results", technique);

    /// <summary>Where the goldens of <paramref name="technique"/> are read from by default.</summary>
    public static string GoldenDirectory(string technique) =>
        Path.Combine(AppContext.BaseDirectory, "Goldens", technique);

    /// <summary>The provenance file beside the golden <paramref name="name"/> of <paramref name="technique"/>.</summary>
    public static string ProvenancePath(string technique, string name) =>
        Path.Combine(GoldenDirectory(technique), name + ".golden.json");

    /// <summary>Reads the provenance of the golden <paramref name="name"/> of <paramref name="technique"/>.</summary>
    public static GoldenProvenance ReadProvenance(string technique, string name) =>
        ReadProvenance(ProvenancePath(technique, name));

    /// <summary>
    /// Reads a <c>&lt;name&gt;.golden.json</c> file. A missing or malformed file is a bug in the
    /// repository, so it throws <see cref="FileNotFoundException"/> or <see cref="InvalidDataException"/>.
    /// </summary>
    public static GoldenProvenance ReadProvenance(string path)
    {
        var json = File.ReadAllText(path);
        try
        {
            return JsonSerializer.Deserialize<GoldenProvenance>(json, ProvenanceOptions)
                ?? throw new InvalidDataException($"{path} holds no provenance.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path} is not valid golden provenance: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Compares <paramref name="actual"/> with <c>&lt;goldenDirectory&gt;/&lt;name&gt;.png</c>.
    /// On anything but a match it writes <c>&lt;name&gt;.actual.png</c> into
    /// <paramref name="resultsDirectory"/>, and for a size or pixel mismatch
    /// <c>&lt;name&gt;.expected.png</c> too. On a match it removes stale copies of both, so the
    /// directory holds only failures from the last run.
    /// </summary>
    public static GoldenComparison Compare(
        string goldenDirectory,
        string name,
        PixelBuffer actual,
        string resultsDirectory)
    {
        ArgumentNullException.ThrowIfNull(actual);

        var goldenPath = Path.Combine(goldenDirectory, name + ".png");
        var actualPath = Path.Combine(resultsDirectory, name + ".actual.png");
        var expectedPath = Path.Combine(resultsDirectory, name + ".expected.png");

        if (!File.Exists(goldenPath))
        {
            Remove(expectedPath);
            WriteActual(actual, resultsDirectory, actualPath);
            return new Missing(actualPath);
        }

        var goldenBytes = File.ReadAllBytes(goldenPath);
        var expected = Png.Decode(goldenBytes);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            WriteFailure(actual, goldenBytes, resultsDirectory, actualPath, expectedPath);
            return new SizeDiffers(
                expected.Width,
                expected.Height,
                actual.Width,
                actual.Height,
                actualPath,
                expectedPath);
        }

        var count = 0;
        var firstX = 0;
        var firstY = 0;
        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                if (expected[x, y] == actual[x, y])
                {
                    continue;
                }

                if (count == 0)
                {
                    firstX = x;
                    firstY = y;
                }

                count++;
            }
        }

        if (count == 0)
        {
            Remove(actualPath);
            Remove(expectedPath);
            return new Matches();
        }

        WriteFailure(actual, goldenBytes, resultsDirectory, actualPath, expectedPath);
        return new PixelsDiffer(
            count,
            firstX,
            firstY,
            expected[firstX, firstY],
            actual[firstX, firstY],
            actualPath,
            expectedPath);
    }

    /// <summary>
    /// Compares with the golden <paramref name="name"/> of <paramref name="technique"/>, read from
    /// <c>Goldens/&lt;technique&gt;</c> in the test output, and writes failures to
    /// <c>golden-results/&lt;technique&gt;</c> beside it.
    /// </summary>
    public static GoldenComparison Compare(string technique, string name, PixelBuffer actual) =>
        Compare(GoldenDirectory(technique), name, actual, ResultsDirectory(technique));

    /// <summary>
    /// The test-only unwrap (railway R9): returns for <see cref="Matches"/> and throws
    /// <see cref="GoldenMismatchException"/> for every other case, with a message that names the
    /// case and the paths written. When <paramref name="provenance"/> is given, the message also
    /// gives the .NET version the golden was approved on and the one running now.
    /// </summary>
    public static void AssertMatches(GoldenComparison comparison, GoldenProvenance? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        if (comparison is Matches)
        {
            return;
        }

        throw new GoldenMismatchException(Describe(comparison, provenance), comparison);
    }

    /// <summary>Compares, then unwraps with <see cref="AssertMatches(GoldenComparison, GoldenProvenance?)"/>.</summary>
    public static void AssertMatches(
        string goldenDirectory,
        string name,
        PixelBuffer actual,
        string resultsDirectory,
        GoldenProvenance? provenance = null) =>
        AssertMatches(Compare(goldenDirectory, name, actual, resultsDirectory), provenance);

    /// <summary>Compares with the default directories, then unwraps.</summary>
    public static void AssertMatches(
        string technique,
        string name,
        PixelBuffer actual,
        GoldenProvenance? provenance = null) =>
        AssertMatches(Compare(technique, name, actual), provenance);

    // File.Delete throws when the directory is missing, as the results directory is before a first failure.
    private static void Remove(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void WriteActual(PixelBuffer actual, string resultsDirectory, string actualPath)
    {
        Directory.CreateDirectory(resultsDirectory);
        File.WriteAllBytes(actualPath, Png.Encode(actual));
    }

    private static void WriteFailure(
        PixelBuffer actual,
        byte[] goldenBytes,
        string resultsDirectory,
        string actualPath,
        string expectedPath)
    {
        WriteActual(actual, resultsDirectory, actualPath);
        File.WriteAllBytes(expectedPath, goldenBytes);
    }

    private static string Describe(GoldenComparison comparison, GoldenProvenance? provenance)
    {
        var lines = new List<string>();
        switch (comparison)
        {
            case Matches:
                lines.Add("The golden matches.");
                break;
            case Missing missing:
                lines.Add("Golden missing: there is no golden image for this output.");
                lines.Add($"Actual image written to {missing.ActualPath}.");
                lines.Add(
                    "A new golden needs the owner's approval by eye before it is copied into Goldens/ "
                        + "(testing R6). Do not copy it in to make this test pass.");
                break;
            case SizeDiffers size:
                lines.Add(
                    $"Golden size differs: expected {size.ExpectedWidth}x{size.ExpectedHeight}, "
                        + $"actual {size.ActualWidth}x{size.ActualHeight}.");
                lines.Add($"Actual image written to {size.ActualPath}.");
                lines.Add($"Expected image written to {size.ExpectedPath}.");
                break;
            case PixelsDiffer pixels:
                lines.Add($"Golden pixels differ: {pixels.Count} pixel(s) differ.");
                lines.Add(
                    $"First difference at ({pixels.FirstX}, {pixels.FirstY}): "
                        + $"expected {Format(pixels.Expected)}, actual {Format(pixels.Actual)}.");
                lines.Add($"Actual image written to {pixels.ActualPath}.");
                lines.Add($"Expected image written to {pixels.ExpectedPath}.");
                break;
        }

        if (provenance is not null)
        {
            var current = $"{Environment.Version.Major}.{Environment.Version.Minor}";
            lines.Add(
                provenance.Dotnet == current
                    ? $"The golden was approved on .NET {provenance.Dotnet}; this run is on .NET {current}."
                    : $"The golden was approved on .NET {provenance.Dotnet}; this run is on .NET {current}, "
                        + "so a runtime change may explain the difference.");
        }

        if (comparison is not Missing)
        {
            lines.Add("Establish whether the change in output was intended, and show the owner both images (testing R6).");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Format(Rgba32 pixel) => $"rgba({pixel.R}, {pixel.G}, {pixel.B}, {pixel.A})";
}
