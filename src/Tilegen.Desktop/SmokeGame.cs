using Apos.Gui;
using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tilegen.Engine;
using Tilegen.Export;
using Tilegen.Scripting.CSharp;

namespace Tilegen.Desktop;

/// <summary>
/// The scaffold's smoke window: it opens, loads the UI font and shows one label naming the
/// layer assemblies Desktop was wired with. It is replaced by the real app.
/// </summary>
public sealed class SmokeGame : Game
{
    // Naming each layer's assembly, through a type it declares, makes Desktop's references to the
    // layers real uses in IL.
    private readonly string _labelText =
        $"tilegen: {typeof(GenerationRun).Assembly.GetName().Name}, "
        + $"{typeof(ScriptingMarker).Assembly.GetName().Name}, "
        + $"{typeof(ExportMarker).Assembly.GetName().Name}";

    private readonly GraphicsDeviceManager _graphics;

    // Set in LoadContent, the first point where the graphics device and the font stream exist.
    private IMGUI? _ui;

    public SmokeGame()
    {
        // Apos.Shapes needs the HiDef profile.
        _graphics = new GraphicsDeviceManager(this) { GraphicsProfile = GraphicsProfile.HiDef };
        IsMouseVisible = true;
        Window.Title = "tilegen";
    }

    protected override void LoadContent()
    {
        using var fontStream = TitleContainer.OpenStream("Fonts/FiraSans-Regular.ttf");
        GuiHelper.Setup(this, new ShapeFont(fontStream));
        _ui = new IMGUI();
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_ui is { } ui)
        {
            GuiHelper.UpdateSetup(gameTime);
            ui.UpdateStart(gameTime);
            MenuPanel.Push();
            Label.Put(_labelText);
            MenuPanel.Pop();
            ui.UpdateEnd(gameTime);
            GuiHelper.UpdateCleanup();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        _ui?.Draw(gameTime);
        base.Draw(gameTime);
    }
}
