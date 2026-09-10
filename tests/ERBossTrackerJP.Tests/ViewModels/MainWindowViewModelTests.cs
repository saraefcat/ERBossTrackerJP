using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.ViewModels;

namespace ERBossTrackerJP.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task InitializeAsync_PromptsForFolderWhenDefaultLocationIsEmpty()
    {
        var locator = new StubSaveFileLocator();
        var loadService = new StubSaveLoadService();
        var viewModel = CreateViewModel(locator, loadService);

        await viewModel.InitializeAsync();

        Assert.Empty(viewModel.SaveCandidates);
        Assert.Empty(viewModel.CharacterSlots);
        Assert.Null(viewModel.SelectedSaveCandidate);
        Assert.Contains("フォルダー参照", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(0, loadService.CallCount);
    }

    [Fact]
    public async Task InitializeAsync_LoadsNewestDefaultCandidateAndCharacters()
    {
        SaveFileCandidate newest = CreateCandidate("C:\\saves\\new\\ER0000.sl2");
        SaveFileCandidate older = CreateCandidate("C:\\saves\\old\\ER0000.sl2");
        var locator = new StubSaveFileLocator([newest, older]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            newest.FilePath,
            new CharacterSlot(1, "褪せ人", 80),
            new CharacterSlot(4, "Tarnished", 150)));
        var viewModel = CreateViewModel(locator, loadService);

        await viewModel.InitializeAsync();

        Assert.Equal(newest, viewModel.SelectedSaveCandidate);
        Assert.Equal(newest.FilePath, loadService.LastFilePath);
        Assert.Equal(2, viewModel.CharacterSlots.Count);
        Assert.Equal(1, viewModel.SelectedCharacter?.SlotIndex);
        Assert.Equal("キャラクターを2件読み込みました。", viewModel.StatusMessage);
        Assert.NotEqual("未読み込み", viewModel.SaveLastWriteTimeText);
        Assert.NotNull(viewModel.LoadedSave);
    }

    [Fact]
    public async Task BrowseFolderAsync_LoadsCandidateFromSelectedFolder()
    {
        SaveFileCandidate candidate = CreateCandidate("D:\\custom\\ER0000.sl2");
        var locator = new StubSaveFileLocator(folderCandidates: [candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "Folder Hero", 25)));
        var folderPicker = new StubFolderPicker("D:\\custom");
        var viewModel = CreateViewModel(locator, loadService, folderPicker);
        await viewModel.InitializeAsync();

        await viewModel.BrowseFolderAsync();

        Assert.Equal(locator.DefaultSearchRoot, folderPicker.ReceivedInitialDirectory);
        Assert.Equal("D:\\custom", locator.ReceivedFolderPath);
        Assert.Equal(candidate, viewModel.SelectedSaveCandidate);
        Assert.Equal("Folder Hero", Assert.Single(viewModel.CharacterSlots).Name);
    }

    [Fact]
    public async Task LoadSelectedSaveAsync_KeepsPreviousResultWhenReloadFails()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        LoadedSaveFile successfulLoad = CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(2, "Kept Hero", 100));
        loadService.Enqueue(successfulLoad);
        loadService.Enqueue(new SaveFileReadException(
            SaveFileReadErrorCode.TemporarilyUnavailable,
            candidate.FilePath,
            "locked",
            3));
        var viewModel = CreateViewModel(locator, loadService);
        await viewModel.InitializeAsync();
        CharacterSlot previousCharacter = Assert.Single(viewModel.CharacterSlots);

        await viewModel.LoadSelectedSaveAsync();

        Assert.Same(previousCharacter, Assert.Single(viewModel.CharacterSlots));
        Assert.Same(previousCharacter, viewModel.SelectedCharacter);
        Assert.Same(successfulLoad, viewModel.LoadedSave);
        Assert.Contains("一時的に読み取れません", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadSelectedSaveAsync_DoesNotSwitchWhenTrackedSlotDisappears()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "First", 10),
            new CharacterSlot(5, "Tracked", 50)));
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "First", 11)));
        var viewModel = CreateViewModel(locator, loadService);
        await viewModel.InitializeAsync();
        viewModel.SelectedCharacter = viewModel.CharacterSlots[1];

        await viewModel.LoadSelectedSaveAsync();

        Assert.Null(viewModel.SelectedCharacter);
        Assert.Equal("First", Assert.Single(viewModel.CharacterSlots).Name);
        Assert.Contains("無効になりました", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel(
        ISaveFileLocator locator,
        ISaveLoadService loadService,
        IFolderPickerService? folderPicker = null) =>
        new(locator, loadService, folderPicker ?? new StubFolderPicker(null));

    private static SaveFileCandidate CreateCandidate(string filePath) =>
        new(
            filePath,
            Path.GetDirectoryName(filePath)!,
            null,
            1024,
            new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero));

    private static LoadedSaveFile CreateLoadedSave(
        string filePath,
        params CharacterSlot[] characterSlots) =>
        new(
            new SaveFileSnapshot(
                filePath,
                new byte[128],
                new DateTimeOffset(2026, 9, 10, 1, 2, 3, TimeSpan.Zero)),
            characterSlots);

    private sealed class StubSaveFileLocator : ISaveFileLocator
    {
        private readonly IReadOnlyList<SaveFileCandidate> _defaultCandidates;
        private readonly IReadOnlyList<SaveFileCandidate> _folderCandidates;

        public StubSaveFileLocator(
            IReadOnlyList<SaveFileCandidate>? defaultCandidates = null,
            IReadOnlyList<SaveFileCandidate>? folderCandidates = null)
        {
            _defaultCandidates = defaultCandidates ?? [];
            _folderCandidates = folderCandidates ?? [];
        }

        public string? DefaultSearchRoot => "C:\\Users\\test\\AppData\\Roaming\\EldenRing";

        public string? ReceivedFolderPath { get; private set; }

        public IReadOnlyList<SaveFileCandidate> FindDefaultCandidates() =>
            _defaultCandidates;

        public IReadOnlyList<SaveFileCandidate> FindCandidatesInFolder(string folderPath)
        {
            ReceivedFolderPath = folderPath;
            return _folderCandidates;
        }
    }

    private sealed class StubSaveLoadService : ISaveLoadService
    {
        private readonly Queue<object> _results = new();

        public int CallCount { get; private set; }

        public string? LastFilePath { get; private set; }

        public void Enqueue(LoadedSaveFile loadedSave) => _results.Enqueue(loadedSave);

        public void Enqueue(Exception exception) => _results.Enqueue(exception);

        public Task<LoadedSaveFile> LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastFilePath = filePath;
            object result = _results.Dequeue();

            return result switch
            {
                LoadedSaveFile loadedSave => Task.FromResult(loadedSave),
                Exception exception => Task.FromException<LoadedSaveFile>(exception),
                _ => throw new InvalidOperationException(),
            };
        }

        public ReadOnlyMemory<byte> ReadEventFlags(
            LoadedSaveFile loadedSave,
            int slotIndex) =>
            throw new NotSupportedException();
    }

    private sealed class StubFolderPicker(string? selectedFolder) : IFolderPickerService
    {
        public string? ReceivedInitialDirectory { get; private set; }

        public string? SelectFolder(string? initialDirectory = null)
        {
            ReceivedInitialDirectory = initialDirectory;
            return selectedFolder;
        }
    }
}
