using System.Collections.Immutable;
using Apos.Gui;
using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tilegen.Core.Pipelines;
using Tilegen.Core.Replay;
using Tilegen.Engine;
using Tilegen.Export;
using Tilegen.Scripting.CSharp;
using static System.FormattableString;

namespace Tilegen.Desktop;

/// <summary>
/// The preview window, and the composition root. It validates the built-in description, runs it
/// one step per update and shows the frames as playback picks them. It only presents and gathers
/// input (ports-and-adapters R11): every decision is Core's or Engine's, and the UI is rebuilt each
/// frame from <see cref="Preview"/>.
/// </summary>
public sealed class TilegenGame : Game
{
    /// <summary>
    /// Desktop must depend on both adapter assemblies (the architecture tests' DependencyTests), and
    /// neither has a type Desktop uses yet. These references keep each dependency real in IL until a
    /// real use replaces it: the definitions change removes <see cref="ScriptingMarker"/>'s, and the
    /// export change removes <see cref="ExportMarker"/>'s.
    /// </summary>
    internal static readonly IReadOnlyList<Type> AdapterMarkers = [typeof(ScriptingMarker), typeof(ExportMarker)];

    // Layout, in UI units, which are window pixels at the UI's default scale of 1.
    private const int ColumnWidth = 320;
    private const int FontSize = 20;
    private const float Padding = 12;
    private const float Spacing = 6;

    private readonly GraphicsDeviceManager _graphics;

    // Everything LoadContent makes, the first point where the graphics device and the font stream
    // exist. MonoGame calls LoadContent before the first Update and Draw.
    private Loaded? _loaded;

    // The engine state the window reads each frame. Built the first time a Valid outcome is
    // switched over, and only read after that (design D7).
    private Preview? _preview;

    public TilegenGame()
    {
        // Apos.Shapes needs the HiDef profile.
        _graphics = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 800,
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "tilegen";
    }

    protected override void LoadContent()
    {
        // Composed with plain new, in order (ports-and-adapters R5). Adapters: none yet.

        // Engine state: the built-in description, validated once. The outcome is kept and switched
        // over each frame.
        var outcome = Pipeline.Validate(BuiltInPipeline.Description);

        // Loop wiring: the UI and its font, and what draws the shown frame.
        using var fontStream = TitleContainer.OpenStream("Fonts/FiraSans-Regular.ttf");
        GuiHelper.Setup(this, new ShapeFont(fontStream));
        _loaded = new Loaded(new IMGUI(), new SpriteBatch(GraphicsDevice), new FrameTexture(GraphicsDevice), outcome);

        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_loaded is { } loaded)
        {
            GuiHelper.UpdateSetup(gameTime);
            loaded.Ui.UpdateStart(gameTime);

            // Read every frame, so the layout follows the window as it is resized.
            var viewport = GraphicsDevice.Viewport.Bounds;
            Action content = loaded.Outcome switch
            {
                Valid valid => () => UpdatePreview(PreviewOf(valid), viewport, gameTime),
                Invalid invalid => () => PutFindings(invalid.Findings, FrameArea(viewport)),
            };
            content();

            loaded.Ui.UpdateEnd(gameTime);
            GuiHelper.UpdateCleanup();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Theme.Panel);

        if (_loaded is { } loaded)
        {
            var area = FrameArea(GraphicsDevice.Viewport.Bounds);
            Action content = loaded.Outcome switch
            {
                Valid valid => () => DrawShown(loaded, PreviewOf(valid), area),

                // The findings are UI labels, drawn with the rest of the UI.
                Invalid => () => { },
            };
            content();

            loaded.Ui.Draw(gameTime);
        }

        base.Draw(gameTime);
    }

    private Preview PreviewOf(Valid valid) =>
        _preview ??= new Preview(valid.Pipeline, BuiltInPipeline.StartSeed);

    /// <summary>The space beside the control column, where the frame, or the findings, go.</summary>
    private static Rectangle FrameArea(Rectangle viewport) =>
        new(ColumnWidth, 0, viewport.Width - ColumnWidth, viewport.Height);

    /// <summary>
    /// The control column, rebuilt from the preview each frame (ports-and-adapters R11). The
    /// buttons come first, so a press acts on what was on screen. Then the run and playback
    /// advance, once per update. The labels come last, so they describe the state Draw shows, even
    /// when a restart has just emptied the timeline.
    /// </summary>
    private static void UpdatePreview(Preview preview, Rectangle viewport, GameTime gameTime)
    {
        Dock.Put(0, 0, ColumnWidth, viewport.Height);
        var column = Vertical.Push();
        column.Padding = Padding;
        column.Spacing = Spacing;

        PutControls(preview);
        preview.Update(gameTime.ElapsedGameTime);
        PutLabels(preview);

        Vertical.Pop();
    }

    private static void PutControls(Preview preview)
    {
        // One button plays or pauses, labelled from the playback mode.
        var (playText, playCommand) = preview.Playback switch
        {
            FollowingLatest => ("Play", PlaybackCommand.Play),
            Paused => ("Play", PlaybackCommand.Play),
            Playing => ("Pause", PlaybackCommand.Pause),
        };

        if (Button.Put(playText, FontSize).Clicked)
        {
            preview.Apply(playCommand);
        }

        if (Button.Put("Step back", FontSize).Clicked)
        {
            preview.Apply(PlaybackCommand.StepBack);
        }

        if (Button.Put("Step forward", FontSize).Clicked)
        {
            preview.Apply(PlaybackCommand.StepForward);
        }

        if (Button.Put("First", FontSize).Clicked)
        {
            preview.Apply(PlaybackCommand.First);
        }

        if (Button.Put("Latest", FontSize).Clicked)
        {
            preview.Apply(PlaybackCommand.Latest);
        }

        if (Button.Put("Restart", FontSize).Clicked)
        {
            preview.Restart();
        }

        if (Button.Put("Next seed", FontSize).Clicked)
        {
            preview.NextSeed();
        }
    }

    private static void PutLabels(Preview preview)
    {
        var run = preview.Run;
        var (stage, step, position) = preview.Shown switch
        {
            NoFrame => ("none yet", "none yet", Invariant($"none of {run.Timeline.Count}")),
            FrameAt at => DescribeEntry(run.Timeline, at.Index),
        };

        Label.Put($"Stage: {stage}", FontSize);
        Label.Put($"Step: {step}", FontSize);
        Label.Put($"Frame: {position}", FontSize);
        Label.Put(Invariant($"Seed: {run.Seed.Value}"), FontSize);
        Label.Put($"Run: {RunStateText(run.State)}", FontSize);
        Label.Put($"Playback: {PlaybackText(preview.Playback)}", FontSize);
    }

    /// <summary>The shown entry's stage id and step, and its 1-based position on the timeline.</summary>
    private static (string Stage, string Step, string Position) DescribeEntry(Timeline timeline, int index)
    {
        var entry = timeline[index];
        return (entry.Stage.Value, Invariant($"{entry.Step}"), Invariant($"{index + 1} of {timeline.Count}"));
    }

    private static string RunStateText(RunState state) => state switch
    {
        Running => "running",
        Completed => "completed",
        Stopped => "stopped",
    };

    private static string PlaybackText(Playback playback) => playback switch
    {
        FollowingLatest => "following latest",
        Paused => "paused",
        Playing => "playing",
    };

    /// <summary>An invalid description starts no run: every finding is listed in place of the frame, one per line.</summary>
    private static void PutFindings(ImmutableArray<Finding> findings, Rectangle area)
    {
        Dock.Put(area.Left, area.Top, area.Right, area.Bottom);
        var lines = Vertical.Push();
        lines.Padding = Padding;
        lines.Spacing = Spacing;

        Label.Put("The built-in pipeline description is not valid:", FontSize);
        foreach (var finding in findings)
        {
            Label.Put(FindingText.Describe(finding), FontSize);
        }

        Vertical.Pop();
    }

    private static void DrawShown(Loaded loaded, Preview preview, Rectangle area)
    {
        Action draw = preview.Shown switch
        {
            // Nothing has been generated yet.
            NoFrame => () => { },
            FrameAt at => () => DrawFrame(loaded.Sprites, loaded.Frames.For(preview.Run, at.Index), area),
        };
        draw();
    }

    private static void DrawFrame(SpriteBatch sprites, Texture2D texture, Rectangle area)
    {
        // Point sampling keeps each frame pixel a sharp square. Rgba32 isn't premultiplied, and
        // MonoGame's default AlphaBlend assumes it is.
        sprites.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);
        sprites.Draw(texture, Fit(texture.Width, texture.Height, area), Color.White);
        sprites.End();
    }

    /// <summary>
    /// The frame scaled by the largest whole factor that fits the area, and at least 1, centred in
    /// it. Presentation only: it changes no generated pixel and nothing about what is drawn
    /// (design, layer mapping).
    /// </summary>
    private static Rectangle Fit(int width, int height, Rectangle area)
    {
        var scale = Math.Max(1, Math.Min(area.Width / width, area.Height / height));
        var (scaledWidth, scaledHeight) = (width * scale, height * scale);
        return new Rectangle(
            area.X + ((area.Width - scaledWidth) / 2),
            area.Y + ((area.Height - scaledHeight) / 2),
            scaledWidth,
            scaledHeight);
    }

    private sealed class Loaded(IMGUI ui, SpriteBatch sprites, FrameTexture frames, ValidationOutcome outcome)
    {
        public IMGUI Ui { get; } = ui;

        public SpriteBatch Sprites { get; } = sprites;

        public FrameTexture Frames { get; } = frames;

        public ValidationOutcome Outcome { get; } = outcome;
    }
}
