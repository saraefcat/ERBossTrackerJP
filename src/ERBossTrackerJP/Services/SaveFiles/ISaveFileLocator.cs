namespace ERBossTrackerJP.Services.SaveFiles;

public interface ISaveFileLocator
{
    string? DefaultSearchRoot { get; }

    IReadOnlyList<SaveFileCandidate> FindDefaultCandidates();

    IReadOnlyList<SaveFileCandidate> FindCandidatesInFolder(string folderPath);
}
