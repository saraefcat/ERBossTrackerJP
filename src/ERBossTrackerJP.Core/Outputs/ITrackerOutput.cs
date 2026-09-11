namespace ERBossTrackerJP.Core.Outputs;

public interface ITrackerOutput
{
    ValueTask PublishAsync(
        TrackerOutputUpdate update,
        CancellationToken cancellationToken = default);
}
