using ERBossTrackerJP.Core.Outputs;

namespace ERBossTrackerJP.Services.Outputs;

public interface IObsTextFileOutput : ITrackerOutput
{
    string DefaultOutputDirectory { get; }

    string OutputDirectory { get; }

    bool TrySetOutputDirectory(string outputDirectory);
}
