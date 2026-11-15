using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Tilegen.Desktop;

/// <summary>
/// A development aid for checking the window without looking at it, for example while the screen
/// is locked: with <c>TILEGEN_SCREENSHOT</c> set to a file path, the window saves its back buffer
/// there as a PNG once <c>TILEGEN_SCREENSHOT_AT</c> seconds of game time have passed (3 by
/// default), then exits. Without the variable it does nothing.
/// </summary>
internal sealed record Screenshot(string Path, TimeSpan At)
{
    private static readonly TimeSpan DefaultAt = TimeSpan.FromSeconds(3);

    public static Screenshot? FromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable("TILEGEN_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var at = Environment.GetEnvironmentVariable("TILEGEN_SCREENSHOT_AT");
        return new Screenshot(
            path,
            at is null ? DefaultAt : TimeSpan.FromSeconds(double.Parse(at, CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Saves the back buffer and returns true once <see cref="At"/> has passed. Call it after
    /// drawing and before presenting.
    /// </summary>
    public bool TrySave(GraphicsDevice device, GameTime gameTime)
    {
        if (gameTime.TotalGameTime < At)
        {
            return false;
        }

        var parameters = device.PresentationParameters;
        var (width, height) = (parameters.BackBufferWidth, parameters.BackBufferHeight);
        var pixels = new Color[width * height];
        device.GetBackBufferData(pixels);

        using var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        using var file = File.Create(Path);
        texture.SaveAsPng(file, width, height);
        return true;
    }
}
