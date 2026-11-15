using System.Text.Json;

namespace Tilegen.TestKit.Goldens;

/// <summary>
/// What a golden was made from, read from <c>&lt;name&gt;.golden.json</c> beside its PNG (testing R5).
/// The golden's test takes its parameters and stage seed from here, so the two can't drift apart.
/// </summary>
/// <param name="Technique">The technique's name.</param>
/// <param name="Parameters">
/// The technique's parameters as a JSON object. The technique's own test interprets it.
/// </param>
/// <param name="StageSeed">The stage seed the technique's <c>System.Random</c> was created from.</param>
/// <param name="Dotnet">The runtime the golden was approved on, as major.minor (for example "11.0").</param>
/// <param name="ApprovedIn">The OpenSpec change in which the owner approved the golden.</param>
public sealed record class GoldenProvenance(
    string Technique,
    JsonElement Parameters,
    int StageSeed,
    string Dotnet,
    string ApprovedIn);
