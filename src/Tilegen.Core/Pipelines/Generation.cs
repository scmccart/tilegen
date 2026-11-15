using Tilegen.Core.Imaging;
using Tilegen.Core.Techniques;

namespace Tilegen.Core.Pipelines;

/// <summary>
/// Runs a validated stage with its technique. Each <see cref="Stage"/> case maps to one technique,
/// and the switch has no discard arm, so a new case doesn't compile until it is handled here.
/// </summary>
public static class Generation
{
    /// <summary>
    /// The frames of <paramref name="stage"/>'s technique, drawing only from
    /// <paramref name="random"/>, which the engine seeded from the stage's seed
    /// (functional-core R3, R9). Frames are valid until the next step (functional-core R4).
    /// </summary>
    public static IEnumerable<Frame> Run(Stage stage, Random random)
    {
        ArgumentNullException.ThrowIfNull(stage);

        return stage switch
        {
            Noise noise => ValueNoise.Run(noise.Parameters, random),
        };
    }
}
