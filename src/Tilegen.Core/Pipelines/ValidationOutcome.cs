using System.Collections.Immutable;

namespace Tilegen.Core.Pipelines;

/// <summary>The result of validating a description: a pipeline, or every finding against it.</summary>
public closed record class ValidationOutcome;

public sealed record class Valid(ValidatedPipeline Pipeline) : ValidationOutcome;

/// <summary>
/// Every finding against a description. Never empty: an empty list is a bug in validation, so the
/// constructor throws.
/// </summary>
public sealed record class Invalid : ValidationOutcome
{
    internal Invalid(ImmutableArray<Finding> findings)
    {
        if (findings.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An Invalid outcome has at least one finding.", nameof(findings));
        }

        Findings = findings;
    }

    public ImmutableArray<Finding> Findings { get; }
}
