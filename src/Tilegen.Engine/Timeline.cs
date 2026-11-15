namespace Tilegen.Engine;

/// <summary>
/// Every frame a <see cref="GenerationRun"/> has yielded, in order, each as a copy with its stage
/// and step. Readers can count and index it but not change it: only the run that owns it appends,
/// and nothing removes or replaces an entry. A new run starts with a new timeline.
/// </summary>
public sealed class Timeline
{
    private readonly List<TimelineEntry> _entries = [];

    internal Timeline()
    {
    }

    public int Count => _entries.Count;

    /// <summary>The entry at <paramref name="index"/>, counting from 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not on the timeline.</exception>
    public TimelineEntry this[int index] => _entries[index];

    internal void Append(TimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}
