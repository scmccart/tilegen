using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tilegen.Core.Imaging;
using Tilegen.Engine;

namespace Tilegen.Desktop;

/// <summary>
/// The shown frame as a texture (design D6). It uploads only when a different frame is shown,
/// keyed by the run and the timeline index; the run, not its seed, because a restart keeps the
/// seed. This is a GPU copy of an immutable timeline frame, not mirrored state
/// (ports-and-adapters R11): nothing reads it back, and a new key replaces it.
/// </summary>
internal sealed class FrameTexture(GraphicsDevice device)
{
    // Sized to the shown frame. Core's pixels are converted to MonoGame's field by field, here at
    // the edge (functional-core R7), without relying on the two structs sharing a layout.
    private Rgba32[] _pixels = [];
    private Color[] _colors = [];
    private Texture2D? _texture;
    private Key? _shown;

    /// <summary>The texture of the frame at <paramref name="index"/> on <paramref name="run"/>'s timeline.</summary>
    public Texture2D For(GenerationRun run, int index)
    {
        var key = new Key(run, index);
        if (_texture is { } cached && _shown == key)
        {
            return cached;
        }

        var texture = Upload(run.Timeline[index].Image);
        _shown = key;
        return texture;
    }

    private Texture2D Upload(Image image)
    {
        if (_texture is null || _texture.Width != image.Width || _texture.Height != image.Height)
        {
            _texture?.Dispose();
            _texture = new Texture2D(device, image.Width, image.Height);
            _pixels = new Rgba32[image.Width * image.Height];
            _colors = new Color[_pixels.Length];
        }

        image.CopyTo(_pixels);
        for (var i = 0; i < _pixels.Length; i++)
        {
            var pixel = _pixels[i];
            _colors[i] = new Color(pixel.R, pixel.G, pixel.B, pixel.A);
        }

        _texture.SetData(_colors);
        return _texture;
    }

    /// <summary>Which frame the texture holds. The run compares by reference: each run is a new object.</summary>
    private readonly record struct Key(GenerationRun Run, int Index);
}
