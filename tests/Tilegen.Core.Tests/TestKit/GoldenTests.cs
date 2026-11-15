using Tilegen.Core.Imaging;
using Tilegen.TestKit.Goldens;
using Xunit;

namespace Tilegen.Core.Tests.TestKit;

public sealed class GoldenTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tilegen-golden-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _goldens;
    private readonly string _results;

    public GoldenTests()
    {
        _goldens = Path.Combine(_root, "goldens");
        _results = Path.Combine(_root, "results");
        Directory.CreateDirectory(_goldens);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Identical_pixels_match_and_write_nothing()
    {
        var buffer = TwoByTwo();
        StoreGolden("same", buffer);

        var comparison = Golden.Compare(_goldens, "same", buffer.Copy(), _results);

        Assert.IsType<Matches>(comparison);
        Assert.False(Directory.Exists(_results) && Directory.EnumerateFiles(_results).Any());
    }

    [Fact]
    public void A_match_removes_the_images_a_failed_run_left_behind()
    {
        var buffer = TwoByTwo();
        StoreGolden("recovered", buffer);
        var wrong = buffer.Copy();
        wrong[0, 0] = new Rgba32(9, 9, 9, 9);
        Golden.Compare(_goldens, "recovered", wrong, _results);

        Golden.Compare(_goldens, "recovered", buffer, _results);

        Assert.Empty(Directory.EnumerateFiles(_results));
    }

    [Fact]
    public void A_missing_golden_is_reported_and_the_actual_image_is_written()
    {
        var buffer = TwoByTwo();

        var comparison = Golden.Compare(_goldens, "absent", buffer, _results);

        var missing = Assert.IsType<Missing>(comparison);
        Assert.Equal(Path.Combine(_results, "absent.actual.png"), missing.ActualPath);
        AssertSamePixels(buffer, Png.Decode(File.ReadAllBytes(missing.ActualPath)));
        Assert.Empty(Directory.EnumerateFiles(_goldens));
    }

    [Fact]
    public void A_different_size_is_reported_and_both_images_are_written()
    {
        var expected = TwoByTwo();
        var actual = new PixelBuffer(3, 1);
        StoreGolden("size", expected);

        var comparison = Golden.Compare(_goldens, "size", actual, _results);

        var differs = Assert.IsType<SizeDiffers>(comparison);
        Assert.Equal((2, 2, 3, 1), (differs.ExpectedWidth, differs.ExpectedHeight, differs.ActualWidth, differs.ActualHeight));
        AssertSamePixels(actual, Png.Decode(File.ReadAllBytes(differs.ActualPath)));
        AssertSamePixels(expected, Png.Decode(File.ReadAllBytes(differs.ExpectedPath)));
    }

    [Fact]
    public void Differing_pixels_are_counted_with_the_first_in_row_major_order_and_both_images_are_written()
    {
        var expected = TwoByTwo();
        var actual = expected.Copy();
        actual[1, 0] = new Rgba32(1, 2, 3, 4);
        actual[0, 1] = new Rgba32(5, 6, 7, 8);
        StoreGolden("pixels", expected);

        var comparison = Golden.Compare(_goldens, "pixels", actual, _results);

        var differs = Assert.IsType<PixelsDiffer>(comparison);
        Assert.Equal(2, differs.Count);
        Assert.Equal((1, 0), (differs.FirstX, differs.FirstY));
        Assert.Equal(expected[1, 0], differs.Expected);
        Assert.Equal(new Rgba32(1, 2, 3, 4), differs.Actual);
        AssertSamePixels(actual, Png.Decode(File.ReadAllBytes(differs.ActualPath)));
        AssertSamePixels(expected, Png.Decode(File.ReadAllBytes(differs.ExpectedPath)));
    }

    [Fact]
    public void A_difference_in_alpha_alone_is_a_difference()
    {
        var expected = TwoByTwo();
        var actual = expected.Copy();
        var pixel = expected[0, 0];
        actual[0, 0] = pixel with { A = (byte)(pixel.A - 1) };
        StoreGolden("alpha", expected);

        var comparison = Golden.Compare(_goldens, "alpha", actual, _results);

        Assert.Equal(1, Assert.IsType<PixelsDiffer>(comparison).Count);
    }

    [Fact]
    public void AssertMatches_returns_for_a_match()
    {
        var buffer = TwoByTwo();
        StoreGolden("quiet", buffer);

        Golden.AssertMatches(_goldens, "quiet", buffer, _results);
    }

    [Fact]
    public void AssertMatches_throws_for_a_missing_golden_and_the_message_names_the_path_and_the_approval_rule()
    {
        var exception = Assert.Throws<GoldenMismatchException>(
            () => Golden.AssertMatches(_goldens, "unapproved", TwoByTwo(), _results));

        var missing = Assert.IsType<Missing>(exception.Comparison);
        Assert.Contains(missing.ActualPath, exception.Message);
        Assert.Contains("approval", exception.Message);
        Assert.Contains("testing R6", exception.Message);
    }

    [Fact]
    public void AssertMatches_throws_for_a_size_difference_and_the_message_names_both_paths_and_sizes()
    {
        StoreGolden("wide", TwoByTwo());

        var exception = Assert.Throws<GoldenMismatchException>(
            () => Golden.AssertMatches(_goldens, "wide", new PixelBuffer(3, 1), _results));

        var differs = Assert.IsType<SizeDiffers>(exception.Comparison);
        Assert.Contains(differs.ActualPath, exception.Message);
        Assert.Contains(differs.ExpectedPath, exception.Message);
        Assert.Contains("2x2", exception.Message);
        Assert.Contains("3x1", exception.Message);
    }

    [Fact]
    public void AssertMatches_throws_for_a_pixel_difference_and_the_message_gives_count_coordinate_colours_and_paths()
    {
        var expected = TwoByTwo();
        var actual = expected.Copy();
        actual[1, 1] = new Rgba32(10, 20, 30, 40);
        StoreGolden("diff", expected);

        var exception = Assert.Throws<GoldenMismatchException>(
            () => Golden.AssertMatches(_goldens, "diff", actual, _results));

        var differs = Assert.IsType<PixelsDiffer>(exception.Comparison);
        Assert.Contains(differs.ActualPath, exception.Message);
        Assert.Contains(differs.ExpectedPath, exception.Message);
        Assert.Contains("1 pixel(s) differ", exception.Message);
        Assert.Contains("(1, 1)", exception.Message);
        Assert.Contains("rgba(10, 20, 30, 40)", exception.Message);
        Assert.Contains("rgba(255, 255, 255, 255)", exception.Message);
    }

    [Fact]
    public void AssertMatches_reports_the_recorded_and_the_current_dotnet_version_when_given_provenance()
    {
        var provenance = new GoldenProvenance(
            "test",
            default,
            StageSeed: 1,
            Dotnet: "1.0",
            ApprovedIn: "a-change");

        var exception = Assert.Throws<GoldenMismatchException>(
            () => Golden.AssertMatches(_goldens, "versions", TwoByTwo(), _results, provenance));

        Assert.Contains(".NET 1.0", exception.Message);
        Assert.Contains($".NET {Environment.Version.Major}.{Environment.Version.Minor}", exception.Message);
    }

    [Fact]
    public void Comparing_never_writes_into_the_goldens_directory()
    {
        var golden = Path.Combine(_goldens, "kept.png");
        StoreGolden("kept", TwoByTwo());
        var bytes = File.ReadAllBytes(golden);

        Golden.Compare(_goldens, "kept", new PixelBuffer(1, 1), _results);
        Golden.Compare(_goldens, "unknown", new PixelBuffer(1, 1), _results);

        Assert.Equal([golden], Directory.EnumerateFiles(_goldens));
        Assert.Equal(bytes, File.ReadAllBytes(golden));
    }

    [Fact]
    public void The_technique_overload_writes_failure_images_under_a_directory_for_that_technique()
    {
        var technique = "tilegen-test-" + Guid.NewGuid().ToString("N");
        var resultsRoot = Path.Combine(AppContext.BaseDirectory, "golden-results");
        try
        {
            var comparison = Golden.Compare(technique, "absent", TwoByTwo());

            var missing = Assert.IsType<Missing>(comparison);
            Assert.Equal(Path.Combine(resultsRoot, technique, "absent.actual.png"), missing.ActualPath);
            Assert.True(File.Exists(missing.ActualPath));
        }
        finally
        {
            Directory.Delete(Path.Combine(resultsRoot, technique), recursive: true);
        }
    }

    private void StoreGolden(string name, PixelBuffer buffer) =>
        File.WriteAllBytes(Path.Combine(_goldens, name + ".png"), Png.Encode(buffer));

    // Four pixels that differ in every channel, with a translucent one.
    private static PixelBuffer TwoByTwo()
    {
        var buffer = new PixelBuffer(2, 2);
        buffer[0, 0] = new Rgba32(255, 0, 0, 255);
        buffer[1, 0] = new Rgba32(0, 255, 0, 128);
        buffer[0, 1] = new Rgba32(0, 0, 255, 0);
        buffer[1, 1] = new Rgba32(255, 255, 255, 255);
        return buffer;
    }

    private static void AssertSamePixels(PixelBuffer expected, PixelBuffer actual)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }
}
