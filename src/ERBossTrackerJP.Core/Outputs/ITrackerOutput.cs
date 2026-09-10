using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Outputs;

public interface ITrackerOutput
{
    ValueTask PublishAsync(
        TrackerSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
