using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Outputs;

public sealed record TrackerOutputUpdate
{
    public TrackerOutputUpdate(
        TrackerSnapshot snapshot,
        TrackerSnapshot? previousSnapshot,
        DisplayLanguage displayLanguage)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Snapshot = snapshot;
        PreviousSnapshot = previousSnapshot;
        DisplayLanguage = displayLanguage;
    }

    public TrackerSnapshot Snapshot { get; }

    public TrackerSnapshot? PreviousSnapshot { get; }

    public DisplayLanguage DisplayLanguage { get; }
}
