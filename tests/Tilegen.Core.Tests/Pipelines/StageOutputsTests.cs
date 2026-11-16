using Tilegen.Core.Imaging;
using Tilegen.Core.Pipelines;
using Xunit;

namespace Tilegen.Core.Tests.Pipelines;

public class StageOutputsTests
{
    private static readonly StageId Height = StageId.Create("height");
    private static readonly StageId Moisture = StageId.Create("moisture");

    private static Image OnePixel(byte value)
    {
        var buffer = new PixelBuffer(1, 1);
        buffer[0, 0] = Rgba32.Opaque(value, value, value);
        return buffer.ToImage();
    }

    [Fact]
    public void The_indexer_returns_the_image_added_for_a_stage()
    {
        var image = OnePixel(1);

        var outputs = StageOutputs.Empty.With(Height, image);

        Assert.Same(image, outputs[Height]);
    }

    [Fact]
    public void The_indexer_finds_a_stage_by_an_equal_id()
    {
        var image = OnePixel(1);

        var outputs = StageOutputs.Empty.With(Height, image);

        Assert.Same(image, outputs[StageId.Create("height")]);
    }

    [Fact]
    public void With_leaves_the_original_unchanged()
    {
        var original = StageOutputs.Empty.With(Height, OnePixel(1));
        var heightBefore = original[Height];

        var added = original.With(Moisture, OnePixel(2));

        Assert.Throws<KeyNotFoundException>(() => original[Moisture]);
        Assert.Same(heightBefore, original[Height]);
        Assert.Same(heightBefore, added[Height]);
        Assert.Throws<KeyNotFoundException>(() => StageOutputs.Empty[Height]);
    }

    [Fact]
    public void Each_stage_keeps_its_own_image()
    {
        var height = OnePixel(1);
        var moisture = OnePixel(2);

        var outputs = StageOutputs.Empty.With(Height, height).With(Moisture, moisture);

        Assert.Same(height, outputs[Height]);
        Assert.Same(moisture, outputs[Moisture]);
    }

    [Fact]
    public void Looking_up_a_stage_with_no_output_throws()
    {
        var outputs = StageOutputs.Empty.With(Height, OnePixel(1));

        Assert.Throws<KeyNotFoundException>(() => outputs[Moisture]);
    }
}
