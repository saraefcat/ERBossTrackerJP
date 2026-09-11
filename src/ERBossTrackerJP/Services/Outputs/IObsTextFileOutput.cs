using ERBossTrackerJP.Core.Outputs;

namespace ERBossTrackerJP.Services.Outputs;

public interface IObsTextFileOutput : ITrackerOutput
{
    string DefaultOutputDirectory { get; }

    string OutputDirectory { get; }

    string DefaultProgressFormat { get; }

    string ProgressFormat { get; }

    bool TrySetOutputDirectory(string outputDirectory);

    bool TrySetProgressFormat(string progressFormat);
}
