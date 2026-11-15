namespace Tilegen.Core.Pipelines;

/// <summary>The name of a stage: not empty, and containing no whitespace.</summary>
public sealed record class StageId
{
    internal StageId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsValid(value))
        {
            throw new ArgumentException("A stage id is not empty and contains no whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static bool IsValid(string value) => value.Length > 0 && !value.Any(char.IsWhiteSpace);

    /// <summary>For constants and tests, where a bad value is a bug. Ids from a definition go through validation.</summary>
    public static StageId Create(string value) => new(value);

    public override string ToString() => Value;
}
