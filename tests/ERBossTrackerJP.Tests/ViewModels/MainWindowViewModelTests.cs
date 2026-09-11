using System.Windows.Threading;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Core.Outputs;
using ERBossTrackerJP.Core.Presentation;
using ERBossTrackerJP.Save.Exceptions;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.Monitoring;
using ERBossTrackerJP.Services.Outputs;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.Services.Settings;
using ERBossTrackerJP.Services.Theming;
using ERBossTrackerJP.Services.Tracking;
using ERBossTrackerJP.ViewModels;
using ERBossTrackerJP.Views;

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
        Assert.Equal(3, viewModel.Total);
        Assert.Equal(1, viewModel.Defeated);
        Assert.Equal(2, viewModel.Remaining);
        Assert.Contains("ボス進捗を読み込みました", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsProgressStatusMessageVisible);
        Assert.NotEqual("未読み込み", viewModel.SaveLastWriteTimeText);
        Assert.NotNull(viewModel.LoadedSave);
        Assert.NotNull(viewModel.TrackerSnapshot);
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
        Assert.True(viewModel.IsProgressStatusMessageVisible);
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

    [Fact]
    public async Task LoadSelectedSaveAsync_KeepsPreviousProgressWhenSameSlotParsingFails()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(2, "Kept Progress", 100)));
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(2, "Kept Progress", 101)));
        var trackerService = new StubTrackerSnapshotService();
        var viewModel = CreateViewModel(
            locator,
            loadService,
            trackerSnapshotService: trackerService);
        await viewModel.InitializeAsync();
        TrackerSnapshot previousSnapshot = viewModel.TrackerSnapshot!;
        BossListItem[] previousBosses = viewModel.Bosses.ToArray();
        trackerService.NextException = new SaveParseException(
            SaveParseErrorCode.EventFlagSectionNotFound,
            "missing event flags");

        await viewModel.LoadSelectedSaveAsync();

        Assert.Same(previousSnapshot, viewModel.TrackerSnapshot);
        Assert.Equal(previousBosses, viewModel.Bosses);
        Assert.Contains("解析できません", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiltersAndLanguage_RefreshBossesWithoutReadingSaveAgain()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "Filter Hero", 90)));
        var trackerService = new StubTrackerSnapshotService();
        var viewModel = CreateViewModel(
            locator,
            loadService,
            trackerSnapshotService: trackerService);

        await viewModel.InitializeAsync();

        Assert.Equal(3, viewModel.Bosses.Count);
        Assert.Contains(viewModel.Bosses, boss => boss.Name == "ツリーガード");
        Assert.Equal(1, trackerService.CallCount);

        viewModel.CompletionFilter = BossCompletionFilter.Undefeated;
        viewModel.ContentFilter = GameContent.ShadowOfTheErdtree;

        BossListItem dlcBoss = Assert.Single(viewModel.Bosses);
        Assert.Equal("神獣獅子舞", dlcBoss.Name);
        Assert.Equal("未撃破", dlcBoss.StatusText);

        viewModel.DisplayLanguage = DisplayLanguage.English;
        viewModel.SearchText = "Divine Beast";

        BossListItem englishBoss = Assert.Single(viewModel.Bosses);
        Assert.Equal("Divine Beast Dancing Lion", englishBoss.Name);
        Assert.Contains(
            viewModel.RegionOptions,
            option => option.Label == "Gravesite Plain");
        Assert.Equal(1, trackerService.CallCount);
    }

    [Fact]
    public async Task SelectingRegion_RestrictsBossListToThatRegion()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "Region Hero", 40)));
        var viewModel = CreateViewModel(locator, loadService);
        await viewModel.InitializeAsync();
        RegionListItem selectedRegion = Assert.Single(
            viewModel.Regions,
            region => region.RegionId == "limgrave");

        viewModel.SelectedRegion = selectedRegion;

        BossListItem boss = Assert.Single(viewModel.Bosses);
        Assert.Equal("limgrave", boss.RegionId);
        Assert.Equal("limgrave", viewModel.RegionFilterId);
    }

    [Fact]
    public async Task InitializeAsync_StartsMonitoringAndDetectedChangeReloadsSameSlot()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(2, "Monitored", 100)));
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(2, "Monitored", 101)));
        var monitor = new StubSaveFileMonitor();
        var trackerService = new StubTrackerSnapshotService();
        var viewModel = CreateViewModel(
            locator,
            loadService,
            saveFileMonitor: monitor,
            trackerSnapshotService: trackerService);

        await viewModel.InitializeAsync();
        await viewModel.HandleSaveFileChangeDetectedAsync(
            new SaveFileChangedEventArgs(
                candidate.FilePath,
                256,
                new DateTimeOffset(2026, 9, 11, 2, 0, 0, TimeSpan.Zero)));

        Assert.True(monitor.IsRunning);
        Assert.Equal(candidate.FilePath, monitor.MonitoredFilePath);
        Assert.Equal(1, monitor.StartCount);
        Assert.Equal(1, monitor.UpdateBaselineCount);
        Assert.Equal(2, loadService.CallCount);
        Assert.Equal(2, trackerService.CallCount);
        Assert.Equal(2, viewModel.SelectedCharacter?.SlotIndex);
        Assert.Equal("監視中", viewModel.MonitoringStatusText);
    }

    [Fact]
    public async Task DisablingMonitoring_StopsMonitorAndIgnoresChange()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "Manual", 10)));
        var monitor = new StubSaveFileMonitor();
        var viewModel = CreateViewModel(
            locator,
            loadService,
            saveFileMonitor: monitor);
        await viewModel.InitializeAsync();

        viewModel.IsAutoMonitoringEnabled = false;
        await viewModel.HandleSaveFileChangeDetectedAsync(
            new SaveFileChangedEventArgs(
                candidate.FilePath,
                256,
                new DateTimeOffset(2026, 9, 11, 2, 0, 0, TimeSpan.Zero)));

        Assert.False(monitor.IsRunning);
        Assert.Equal(1, monitor.StopCount);
        Assert.Equal(1, loadService.CallCount);
        Assert.Equal("停止中", viewModel.MonitoringStatusText);
    }

    [Fact]
    public async Task InitializeAsync_RestoresValidSaveCharacterLanguageAndMonitoringSetting()
    {
        SaveFileCandidate candidate = CreateCandidate("D:\\custom\\ER0000.sl2");
        var locator = new StubSaveFileLocator(folderCandidates: [candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(1, "First", 40),
            new CharacterSlot(4, "Restored", 120)));
        var monitor = new StubSaveFileMonitor();
        var settingsService = new StubUserSettingsService(
            new UserSettings(
                candidate.FilePath,
                4,
                DisplayLanguage.English,
                IsAutoMonitoringEnabled: false));
        var viewModel = CreateViewModel(
            locator,
            loadService,
            saveFileMonitor: monitor,
            userSettingsService: settingsService);

        await viewModel.InitializeAsync();

        Assert.Equal("D:\\custom", locator.ReceivedFolderPath);
        Assert.Equal(candidate, viewModel.SelectedSaveCandidate);
        Assert.Equal(4, viewModel.SelectedCharacter?.SlotIndex);
        Assert.Equal(DisplayLanguage.English, viewModel.DisplayLanguage);
        Assert.False(viewModel.IsAutoMonitoringEnabled);
        Assert.False(monitor.IsRunning);
        Assert.Contains(viewModel.Bosses, boss => boss.Name == "Tree Sentinel");
    }

    [Fact]
    public async Task InitializeAsync_InvalidSavedPathFallsBackWithoutApplyingSavedSlot()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "Fallback", 20),
            new CharacterSlot(4, "Unrelated", 80)));
        var settingsService = new StubUserSettingsService(
            new UserSettings("D:\\missing\\ER0000.sl2", 4));
        var viewModel = CreateViewModel(
            locator,
            loadService,
            userSettingsService: settingsService);

        await viewModel.InitializeAsync();

        Assert.Equal(0, viewModel.SelectedCharacter?.SlotIndex);
        Assert.Equal(candidate.FilePath, settingsService.LastSaved?.SaveFilePath);
        Assert.Equal(0, settingsService.LastSaved?.CharacterSlotIndex);
    }

    [Fact]
    public async Task ChangedUserPreferences_ArePersistedTogether()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "First", 20),
            new CharacterSlot(3, "Preferred", 90)));
        var settingsService = new StubUserSettingsService();
        var viewModel = CreateViewModel(
            locator,
            loadService,
            userSettingsService: settingsService);
        await viewModel.InitializeAsync();

        viewModel.SelectedCharacter = viewModel.CharacterSlots[1];
        viewModel.DisplayLanguage = DisplayLanguage.English;
        viewModel.IsAutoMonitoringEnabled = false;
        viewModel.IsDarkMode = false;
        viewModel.IsObsOutputEnabled = true;

        Assert.Equal(candidate.FilePath, settingsService.LastSaved?.SaveFilePath);
        Assert.Equal(3, settingsService.LastSaved?.CharacterSlotIndex);
        Assert.Equal(DisplayLanguage.English, settingsService.LastSaved?.DisplayLanguage);
        Assert.False(settingsService.LastSaved?.IsAutoMonitoringEnabled);
        Assert.Equal(ApplicationTheme.Light, settingsService.LastSaved?.Theme);
        Assert.True(settingsService.LastSaved?.IsObsOutputEnabled);
        Assert.Equal("C:\\obs-default", settingsService.LastSaved?.ObsOutputDirectory);
    }

    [Fact]
    public void SettingsSaveFailure_IsShownAndClearedAfterSuccessfulRetry()
    {
        var settingsService = new StubUserSettingsService
        {
            CanSave = false,
        };
        using var viewModel = CreateViewModel(
            new StubSaveFileLocator(),
            new StubSaveLoadService(),
            userSettingsService: settingsService);

        viewModel.DisplayLanguage = DisplayLanguage.English;

        Assert.True(viewModel.HasSettingsSaveError);
        Assert.Contains(
            "設定を保存できませんでした",
            viewModel.SettingsSaveStatusText,
            StringComparison.Ordinal);

        settingsService.CanSave = true;
        viewModel.IsDarkMode = false;

        Assert.False(viewModel.HasSettingsSaveError);
        Assert.Equal(ApplicationTheme.Light, settingsService.LastSaved?.Theme);
        Assert.Equal(
            "適用または変更した内容は自動的に保存されます。",
            viewModel.SettingsSaveStatusText);
    }

    [Fact]
    public void Theme_DefaultsToDarkAndSwitchesImmediately()
    {
        var settingsService = new StubUserSettingsService();
        var themeService = new StubApplicationThemeService();
        var viewModel = CreateViewModel(
            new StubSaveFileLocator(),
            new StubSaveLoadService(),
            userSettingsService: settingsService,
            applicationThemeService: themeService);

        Assert.True(viewModel.IsDarkMode);
        Assert.Equal(ApplicationTheme.Dark, themeService.CurrentTheme);

        viewModel.IsDarkMode = false;

        Assert.False(viewModel.IsDarkMode);
        Assert.Equal(ApplicationTheme.Light, themeService.CurrentTheme);
        Assert.Equal(ApplicationTheme.Light, settingsService.LastSaved?.Theme);
    }

    [Fact]
    public async Task InitializeAsync_EnabledObsOutputPublishesInitialBaseline()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "OBS Hero", 75)));
        var settingsService = new StubUserSettingsService(
            new UserSettings(
                IsObsOutputEnabled: true,
                ObsOutputDirectory: "D:\\stream-overlay"));
        var obsOutput = new StubObsTextFileOutput();
        var viewModel = CreateViewModel(
            locator,
            loadService,
            userSettingsService: settingsService,
            obsTextFileOutput: obsOutput);

        await viewModel.InitializeAsync();

        TrackerOutputUpdate update = Assert.Single(obsOutput.Updates);
        Assert.Null(update.PreviousSnapshot);
        Assert.Same(viewModel.TrackerSnapshot, update.Snapshot);
        Assert.Equal(DisplayLanguage.Japanese, update.DisplayLanguage);
        Assert.Equal("D:\\stream-overlay", obsOutput.OutputDirectory);
        Assert.Equal("出力済み", viewModel.ObsOutputStatusText);
    }

    [Fact]
    public async Task DisablingObsOutput_CancelsInFlightPublish()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "OBS Hero", 75)));
        var obsOutput = new StubObsTextFileOutput();
        using var viewModel = CreateViewModel(
            locator,
            loadService,
            obsTextFileOutput: obsOutput);
        await viewModel.InitializeAsync();
        var publishStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        obsOutput.PublishHandler = (_, cancellationToken) =>
        {
            publishStarted.TrySetResult();
            cancellationToken.Register(
                () => cancellationObserved.TrySetResult());
            return new ValueTask(Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken));
        };

        viewModel.IsObsOutputEnabled = true;
        await publishStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        viewModel.IsObsOutputEnabled = false;

        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("停止中", viewModel.ObsOutputStatusText);
    }

    [Fact]
    public async Task AutomaticReload_ObsOutputReceivesPreviousSnapshotForDefeatDetection()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "OBS Hero", 75)));
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "OBS Hero", 76)));
        var settingsService = new StubUserSettingsService(
            new UserSettings(IsObsOutputEnabled: true));
        var obsOutput = new StubObsTextFileOutput();
        var viewModel = CreateViewModel(
            locator,
            loadService,
            userSettingsService: settingsService,
            obsTextFileOutput: obsOutput);
        await viewModel.InitializeAsync();
        TrackerSnapshot initialSnapshot = obsOutput.Updates[0].Snapshot;

        await viewModel.HandleSaveFileChangeDetectedAsync(
            new SaveFileChangedEventArgs(
                candidate.FilePath,
                256,
                new DateTimeOffset(2026, 9, 11, 9, 30, 0, TimeSpan.Zero)));

        Assert.Equal(2, obsOutput.Updates.Count);
        Assert.Same(initialSnapshot, obsOutput.Updates[1].PreviousSnapshot);
        Assert.NotSame(initialSnapshot, obsOutput.Updates[1].Snapshot);
    }

    [Fact]
    public async Task BrowseObsOutputFolderAsync_ChangesTargetAndPersistsIt()
    {
        var folderPicker = new StubFolderPicker("D:\\OBS");
        var settingsService = new StubUserSettingsService();
        var obsOutput = new StubObsTextFileOutput();
        var viewModel = CreateViewModel(
            new StubSaveFileLocator(),
            new StubSaveLoadService(),
            folderPicker,
            userSettingsService: settingsService,
            obsTextFileOutput: obsOutput);

        await viewModel.BrowseObsOutputFolderAsync();

        Assert.Equal("C:\\obs-default", folderPicker.ReceivedInitialDirectory);
        Assert.Contains("OBSテキスト", folderPicker.ReceivedTitle, StringComparison.Ordinal);
        Assert.Equal("D:\\OBS", viewModel.ObsOutputDirectory);
        Assert.Equal("D:\\OBS\\progress.txt", viewModel.ObsRecommendedProgressFilePath);
        Assert.Equal("D:\\OBS", settingsService.LastSaved?.ObsOutputDirectory);
    }

    [Fact]
    public async Task EnablingObsOutput_WriteFailureKeepsTrackerAndReportsError()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "OBS Hero", 75)));
        var obsOutput = new StubObsTextFileOutput
        {
            NextException = new IOException("write failed"),
        };
        var viewModel = CreateViewModel(
            locator,
            loadService,
            obsTextFileOutput: obsOutput);
        await viewModel.InitializeAsync();
        TrackerSnapshot snapshot = viewModel.TrackerSnapshot!;

        viewModel.IsObsOutputEnabled = true;

        Assert.Same(snapshot, viewModel.TrackerSnapshot);
        Assert.Equal("出力エラー", viewModel.ObsOutputStatusText);
    }

    [Fact]
    public async Task ObsPreviewItems_ShowCurrentTrackerValues()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "Preview Hero", 75)));
        var viewModel = CreateViewModel(locator, loadService);

        await viewModel.InitializeAsync();

        Assert.Contains(
            viewModel.ObsPreviewItems,
            item => item.FileName == ObsTextFileOutput.ProgressFileName &&
                    item.Contents == "1 / 3 (33.3%)" &&
                    item.Usage.Contains("推奨", StringComparison.Ordinal));
        Assert.Contains(
            viewModel.ObsPreviewItems,
            item => item.FileName == ObsTextFileOutput.LatestBossFileName &&
                    item.Contents == "—");
        Assert.Contains(
            viewModel.ObsPreviewItems,
            item => item.FileName == ObsTextFileOutput.SnapshotFileName &&
                    item.Contents == "ボス情報 3件");
    }

    [Fact]
    public async Task TestObsOutputAsync_PublishesCurrentSnapshotAgain()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "OBS Hero", 75)));
        var obsOutput = new StubObsTextFileOutput();
        var settingsService = new StubUserSettingsService(
            new UserSettings(IsObsOutputEnabled: true));
        var viewModel = CreateViewModel(
            locator,
            loadService,
            userSettingsService: settingsService,
            obsTextFileOutput: obsOutput);
        await viewModel.InitializeAsync();

        await viewModel.TestObsOutputAsync();

        Assert.Equal(2, obsOutput.Updates.Count);
        Assert.Same(viewModel.TrackerSnapshot, obsOutput.Updates[1].Snapshot);
        Assert.Same(viewModel.TrackerSnapshot, obsOutput.Updates[1].PreviousSnapshot);
        Assert.NotEqual("未出力", viewModel.ObsLastOutputTimeText);
    }

    [Fact]
    public async Task ApplyObsProgressFormatAsync_UpdatesPreviewPersistsAndRepublishes()
    {
        SaveFileCandidate candidate = CreateCandidate("C:\\saves\\ER0000.sl2");
        var locator = new StubSaveFileLocator([candidate]);
        var loadService = new StubSaveLoadService();
        loadService.Enqueue(CreateLoadedSave(
            candidate.FilePath,
            new CharacterSlot(0, "OBS Hero", 75)));
        var obsOutput = new StubObsTextFileOutput();
        var settingsService = new StubUserSettingsService(
            new UserSettings(IsObsOutputEnabled: true));
        var viewModel = CreateViewModel(
            locator,
            loadService,
            userSettingsService: settingsService,
            obsTextFileOutput: obsOutput);
        await viewModel.InitializeAsync();
        viewModel.ObsProgressFormatDraft =
            "撃破 {defeated}/{total}体・残り{remaining}体・{percentage}%";

        await viewModel.ApplyObsProgressFormatAsync();

        Assert.Equal(
            viewModel.ObsProgressFormatDraft,
            obsOutput.ProgressFormat);
        Assert.Equal(
            viewModel.ObsProgressFormatDraft,
            settingsService.LastSaved?.ObsProgressFormat);
        Assert.Equal("適用済み", viewModel.ObsProgressFormatStatusText);
        Assert.Equal(2, obsOutput.Updates.Count);
        Assert.Contains(
            viewModel.ObsPreviewItems,
            item => item.FileName == ObsTextFileOutput.ProgressFileName &&
                    item.Contents == "撃破 1/3体・残り2体・33.3%");
    }

    [Fact]
    public async Task ApplyObsProgressFormatAsync_InvalidFormatIsNotApplied()
    {
        var obsOutput = new StubObsTextFileOutput();
        var viewModel = CreateViewModel(
            new StubSaveFileLocator(),
            new StubSaveLoadService(),
            obsTextFileOutput: obsOutput);
        viewModel.ObsProgressFormatDraft = "{unknown}";

        await viewModel.ApplyObsProgressFormatAsync();

        Assert.Equal(
            ObsProgressTextFormatter.DefaultFormat,
            obsOutput.ProgressFormat);
        Assert.Contains(
            "使用できない変数",
            viewModel.ObsProgressFormatStatusText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RestoresObsProgressFormatFromSettings()
    {
        const string savedFormat =
            "撃破 {defeated}/{total}体（{percentage}%）";
        var obsOutput = new StubObsTextFileOutput();
        var settingsService = new StubUserSettingsService(
            new UserSettings(ObsProgressFormat: savedFormat));

        var viewModel = CreateViewModel(
            new StubSaveFileLocator(),
            new StubSaveLoadService(),
            userSettingsService: settingsService,
            obsTextFileOutput: obsOutput);

        Assert.Equal(savedFormat, obsOutput.ProgressFormat);
        Assert.Equal(savedFormat, viewModel.ObsProgressFormatDraft);
        Assert.Equal("適用済み", viewModel.ObsProgressFormatStatusText);
    }

    [Fact]
    public void MainWindow_BindingsAndThemeResourcesWorkAtRuntime()
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            App? application = null;
            MainWindow? window = null;
            MainWindowViewModel? viewModel = null;

            try
            {
                application = new App();
                application.InitializeComponent();
                SaveFileCandidate candidate = CreateCandidate(
                    "C:\\saves\\ER0000.sl2");
                var locator = new StubSaveFileLocator([candidate]);
                var loadService = new StubSaveLoadService();
                loadService.Enqueue(CreateLoadedSave(
                    candidate.FilePath,
                    new CharacterSlot(0, "Binding Hero", 30)));
                var themeService = new ApplicationThemeService();
                var settingsService = new StubUserSettingsService
                {
                    CanSave = false,
                };
                viewModel = CreateViewModel(
                    locator,
                    loadService,
                    userSettingsService: settingsService,
                    applicationThemeService: themeService);
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                window = new MainWindow
                {
                    DataContext = viewModel,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowState = System.Windows.WindowState.Minimized,
                };

                window.Show();
                window.Dispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.ApplicationIdle);

                var tabControl = Assert.IsType<System.Windows.Controls.TabControl>(
                    FindVisualDescendant<System.Windows.Controls.TabControl>(window));
                Assert.Equal(3, tabControl.Items.Count);
                Assert.Equal(
                    new[] { "進捗", "OBS出力", "設定" },
                    tabControl.Items
                        .Cast<System.Windows.Controls.TabItem>()
                        .Select(item => item.Header?.ToString() ?? string.Empty)
                        .ToArray());
                var obsOutputTab = Assert.IsType<System.Windows.Controls.TabItem>(
                    tabControl.Items[1]);
                var obsOutputLayout = Assert.IsType<System.Windows.Controls.Grid>(
                    obsOutputTab.Content);
                Assert.Equal(
                    System.Windows.GridUnitType.Star,
                    obsOutputLayout.RowDefinitions[1].Height.GridUnitType);
                var progressTab = Assert.IsType<System.Windows.Controls.TabItem>(
                    tabControl.Items[0]);
                var settingsTab = Assert.IsType<System.Windows.Controls.TabItem>(
                    tabControl.Items[2]);
                var settingsLayout = Assert.IsType<System.Windows.Controls.Grid>(
                    settingsTab.Content);
                settingsLayout.Measure(new System.Windows.Size(1252, 620));
                Assert.True(
                    settingsLayout.DesiredSize.Height <= 620,
                    $"Settings layout requires {settingsLayout.DesiredSize.Height:F1} DIPs.");

                AssertSingleBinding<System.Windows.Controls.ComboBox>(
                    progressTab,
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                    nameof(MainWindowViewModel.SelectedSaveCandidate));
                AssertSingleBinding<System.Windows.Controls.ComboBox>(
                    settingsTab,
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                    nameof(MainWindowViewModel.SelectedSaveCandidate));
                AssertSingleBinding<System.Windows.Controls.ComboBox>(
                    progressTab,
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                    nameof(MainWindowViewModel.SelectedCharacter));
                AssertSingleBinding<System.Windows.Controls.ComboBox>(
                    settingsTab,
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                    nameof(MainWindowViewModel.SelectedCharacter));
                AssertSingleBinding<System.Windows.Controls.CheckBox>(
                    progressTab,
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    nameof(MainWindowViewModel.IsAutoMonitoringEnabled));
                AssertSingleBinding<System.Windows.Controls.CheckBox>(
                    settingsTab,
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    nameof(MainWindowViewModel.IsAutoMonitoringEnabled));
                AssertSingleBinding<System.Windows.Controls.CheckBox>(
                    settingsTab,
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    nameof(MainWindowViewModel.IsDarkMode));
                AssertSingleBinding<System.Windows.Controls.ComboBox>(
                    progressTab,
                    System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
                    nameof(MainWindowViewModel.DisplayLanguage));
                AssertSingleBinding<System.Windows.Controls.ComboBox>(
                    obsOutputTab,
                    System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
                    nameof(MainWindowViewModel.DisplayLanguage));
                AssertSingleBinding<System.Windows.Controls.ComboBox>(
                    settingsTab,
                    System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
                    nameof(MainWindowViewModel.DisplayLanguage));
                AssertSingleBinding<System.Windows.Controls.CheckBox>(
                    obsOutputTab,
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    nameof(MainWindowViewModel.IsObsOutputEnabled));
                AssertSingleBinding<System.Windows.Controls.CheckBox>(
                    settingsTab,
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    nameof(MainWindowViewModel.IsObsOutputEnabled));
                AssertSingleBinding<System.Windows.Controls.TextBox>(
                    obsOutputTab,
                    System.Windows.Controls.TextBox.TextProperty,
                    nameof(MainWindowViewModel.ObsOutputDirectory));
                AssertSingleBinding<System.Windows.Controls.TextBox>(
                    settingsTab,
                    System.Windows.Controls.TextBox.TextProperty,
                    nameof(MainWindowViewModel.ObsOutputDirectory));
                AssertSingleBinding<System.Windows.Controls.TextBox>(
                    obsOutputTab,
                    System.Windows.Controls.TextBox.TextProperty,
                    nameof(MainWindowViewModel.ObsProgressFormatDraft));
                AssertSingleBinding<System.Windows.Controls.TextBox>(
                    settingsTab,
                    System.Windows.Controls.TextBox.TextProperty,
                    nameof(MainWindowViewModel.ObsProgressFormatDraft));
                AssertSingleBinding<System.Windows.Controls.TextBlock>(
                    settingsTab,
                    System.Windows.Controls.TextBlock.TextProperty,
                    nameof(MainWindowViewModel.SettingsSaveStatusText));
                Assert.True(viewModel.HasSettingsSaveError);

                var darkBackground = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    application.Resources["AppBackgroundBrush"]);
                Assert.Equal("#FF101318", darkBackground.Color.ToString());
                var darkInactiveSelection =
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(
                        application.Resources[
                            System.Windows.SystemColors.InactiveSelectionHighlightBrushKey]);
                Assert.Equal(
                    "#FF202F42",
                    darkInactiveSelection.Color.ToString());
                var comboBox = Assert.IsType<System.Windows.Controls.ComboBox>(
                    FindVisualDescendant<System.Windows.Controls.ComboBox>(window));
                comboBox.ApplyTemplate();
                var toggleButton = Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(
                    comboBox.Template.FindName("DropDownToggle", comboBox));
                toggleButton.ApplyTemplate();
                var comboBoxBorder = Assert.IsType<System.Windows.Controls.Border>(
                    toggleButton.Template.FindName("ToggleBorder", toggleButton));
                var darkInputBackground = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    comboBoxBorder.Background);
                Assert.Equal("#FF11161D", darkInputBackground.Color.ToString());
                var darkInputText = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    comboBox.Foreground);
                Assert.Equal("#FFF3F4F6", darkInputText.Color.ToString());
                var button = Assert.IsType<System.Windows.Controls.Button>(
                    FindVisualDescendant<System.Windows.Controls.Button>(window));
                button.ApplyTemplate();
                var buttonBorder = Assert.IsType<System.Windows.Controls.Border>(
                    button.Template.FindName("ButtonBorder", button));
                var darkButtonBackground =
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(
                        buttonBorder.Background);
                Assert.Equal("#FF252D39", darkButtonBackground.Color.ToString());

                viewModel.IsDarkMode = false;
                window.Dispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.ApplicationIdle);

                var lightBackground = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    application.Resources["AppBackgroundBrush"]);
                Assert.Equal("#FFF7F8FA", lightBackground.Color.ToString());
                var lightInactiveSelection =
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(
                        application.Resources[
                            System.Windows.SystemColors.InactiveSelectionHighlightBrushKey]);
                Assert.Equal(
                    "#FFE8EEF5",
                    lightInactiveSelection.Color.ToString());
                var lightInputBackground = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    comboBoxBorder.Background);
                Assert.Equal("#FFFFFFFF", lightInputBackground.Color.ToString());
                var lightInputText = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    comboBox.Foreground);
                Assert.Equal("#FF1F2937", lightInputText.Color.ToString());
                var lightButtonBackground =
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(
                        buttonBorder.Background);
                Assert.Equal("#FFF2F4F7", lightButtonBackground.Color.ToString());
            }
            catch (Exception exception)
            {
                capturedException = exception;
            }
            finally
            {
                window?.Close();
                viewModel?.Dispose();
                application?.Shutdown();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        bool completed = thread.Join(TimeSpan.FromSeconds(10));

        Assert.True(completed, "WPF binding verification did not complete in time.");
        Assert.Null(capturedException);
    }

    private static T? FindVisualDescendant<T>(
        System.Windows.DependencyObject parent)
        where T : System.Windows.DependencyObject
    {
        int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);

        for (int index = 0; index < childCount; index++)
        {
            System.Windows.DependencyObject child =
                System.Windows.Media.VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
            {
                return match;
            }

            T? descendant = FindVisualDescendant<T>(child);

            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindLogicalDescendants<T>(
        System.Windows.DependencyObject parent)
        where T : System.Windows.DependencyObject
    {
        foreach (object child in System.Windows.LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T match)
            {
                yield return match;
            }

            if (child is System.Windows.DependencyObject dependencyObject)
            {
                foreach (T descendant in FindLogicalDescendants<T>(dependencyObject))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static void AssertSingleBinding<T>(
        System.Windows.DependencyObject root,
        System.Windows.DependencyProperty targetProperty,
        string sourcePath)
        where T : System.Windows.DependencyObject
    {
        T[] matches = FindLogicalDescendants<T>(root)
            .Where(control =>
                System.Windows.Data.BindingOperations.GetBinding(
                    control,
                    targetProperty)?.Path.Path == sourcePath)
            .ToArray();

        Assert.Single(matches);
    }

    private static MainWindowViewModel CreateViewModel(
        ISaveFileLocator locator,
        ISaveLoadService loadService,
        IFolderPickerService? folderPicker = null,
        ISaveFileMonitor? saveFileMonitor = null,
        IUserSettingsService? userSettingsService = null,
        IApplicationThemeService? applicationThemeService = null,
        IObsTextFileOutput? obsTextFileOutput = null,
        ITrackerSnapshotService? trackerSnapshotService = null) =>
        new(
            locator,
            loadService,
            folderPicker ?? new StubFolderPicker(null),
            saveFileMonitor ?? new StubSaveFileMonitor(),
            userSettingsService ?? new StubUserSettingsService(),
            applicationThemeService ?? new StubApplicationThemeService(),
            obsTextFileOutput ?? new StubObsTextFileOutput(),
            trackerSnapshotService ?? new StubTrackerSnapshotService(),
            new TrackerDisplayService());

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

        public string? ReceivedTitle { get; private set; }

        public string? SelectFolder(
            string? initialDirectory = null,
            string? title = null)
        {
            ReceivedInitialDirectory = initialDirectory;
            ReceivedTitle = title;
            return selectedFolder;
        }
    }

    private sealed class StubSaveFileMonitor : ISaveFileMonitor
    {
        public event EventHandler<SaveFileChangedEventArgs>? ChangeDetected;

        public event EventHandler<SaveFileMonitorErrorEventArgs>? MonitoringError;

        public bool IsRunning { get; private set; }

        public string? MonitoredFilePath { get; private set; }

        public int StartCount { get; private set; }

        public int UpdateBaselineCount { get; private set; }

        public int StopCount { get; private set; }

        public void Start(
            string filePath,
            long fileSize,
            DateTimeOffset lastWriteTimeUtc)
        {
            StartCount++;
            IsRunning = true;
            MonitoredFilePath = filePath;
        }

        public void UpdateBaseline(
            string filePath,
            long fileSize,
            DateTimeOffset lastWriteTimeUtc)
        {
            Assert.Equal(MonitoredFilePath, filePath);
            UpdateBaselineCount++;
        }

        public void Stop()
        {
            if (IsRunning)
            {
                StopCount++;
            }

            IsRunning = false;
            MonitoredFilePath = null;
        }

        public void Dispose() => Stop();

        public void RaiseChange(SaveFileChangedEventArgs eventArgs) =>
            ChangeDetected?.Invoke(this, eventArgs);

        public void RaiseError(Exception exception) =>
            MonitoringError?.Invoke(
                this,
                new SaveFileMonitorErrorEventArgs(exception));
    }

    private sealed class StubUserSettingsService(
        UserSettings? settings = null) : IUserSettingsService
    {
        public UserSettings? LastSaved { get; private set; }

        public bool CanSave { get; set; } = true;

        public UserSettings Load() => settings ?? UserSettings.Default;

        public bool TrySave(UserSettings userSettings)
        {
            if (!CanSave)
            {
                return false;
            }

            LastSaved = userSettings;
            return true;
        }
    }

    private sealed class StubApplicationThemeService : IApplicationThemeService
    {
        public ApplicationTheme CurrentTheme { get; private set; } =
            ApplicationTheme.Dark;

        public bool TryApply(ApplicationTheme theme)
        {
            if (theme is not ApplicationTheme.Dark and not ApplicationTheme.Light)
            {
                return false;
            }

            CurrentTheme = theme;
            return true;
        }
    }

    private sealed class StubObsTextFileOutput : IObsTextFileOutput
    {
        public string DefaultOutputDirectory { get; } = "C:\\obs-default";

        public string OutputDirectory { get; private set; } = "C:\\obs-default";

        public string DefaultProgressFormat => ObsProgressTextFormatter.DefaultFormat;

        public string ProgressFormat { get; private set; } =
            ObsProgressTextFormatter.DefaultFormat;

        public List<TrackerOutputUpdate> Updates { get; } = [];

        public Exception? NextException { get; set; }

        public Func<TrackerOutputUpdate, CancellationToken, ValueTask>? PublishHandler { get; set; }

        public bool TrySetOutputDirectory(string outputDirectory)
        {
            OutputDirectory = outputDirectory;
            return true;
        }

        public bool TrySetProgressFormat(string progressFormat)
        {
            if (!ObsProgressTextFormatter.TryValidate(
                    progressFormat,
                    out _))
            {
                return false;
            }

            ProgressFormat = progressFormat;
            return true;
        }

        public ValueTask PublishAsync(
            TrackerOutputUpdate update,
            CancellationToken cancellationToken = default)
        {
            if (PublishHandler is not null)
            {
                return PublishHandler(update, cancellationToken);
            }

            if (NextException is not null)
            {
                Exception exception = NextException;
                NextException = null;
                return ValueTask.FromException(exception);
            }

            Updates.Add(update);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubTrackerSnapshotService : ITrackerSnapshotService
    {
        public int CallCount { get; private set; }

        public Exception? NextException { get; set; }

        public TrackerSnapshot Create(
            LoadedSaveFile loadedSave,
            CharacterSlot character)
        {
            CallCount++;

            if (NextException is not null)
            {
                Exception exception = NextException;
                NextException = null;
                throw exception;
            }

            BossDefinition treeSentinel = CreateBoss(
                "tree-sentinel",
                "Tree Sentinel",
                "ツリーガード",
                "limgrave",
                "Limgrave",
                "リムグレイブ",
                GameContent.BaseGame,
                0);
            BossDefinition margit = CreateBoss(
                "margit",
                "Margit, the Fell Omen",
                "忌み鬼、マルギット",
                "stormveil_castle",
                "Stormveil Castle",
                "ストームヴィル城",
                GameContent.BaseGame,
                1);
            BossDefinition dancingLion = CreateBoss(
                "dancing-lion",
                "Divine Beast Dancing Lion",
                "神獣獅子舞",
                "gravesite_plain",
                "Gravesite Plain",
                "墓地平原",
                GameContent.ShadowOfTheErdtree,
                2);

            return new TrackerSnapshot(
                loadedSave.LastWriteTimeUtc,
                character,
                [
                    new BossProgress(treeSentinel, true),
                    new BossProgress(margit, false),
                    new BossProgress(dancingLion, false),
                ],
                [
                    new RegionProgress("limgrave", "Limgrave", "リムグレイブ", 1, 1),
                    new RegionProgress(
                        "stormveil_castle",
                        "Stormveil Castle",
                        "ストームヴィル城",
                        0,
                        1),
                    new RegionProgress(
                        "gravesite_plain",
                        "Gravesite Plain",
                        "墓地平原",
                        0,
                        1),
                ]);
        }

        private static BossDefinition CreateBoss(
            string id,
            string nameEn,
            string nameJa,
            string regionId,
            string regionEn,
            string regionJa,
            GameContent content,
            int sortOrder) =>
            new(
                id,
                (uint)(1000 + sortOrder),
                nameEn,
                nameJa,
                regionId,
                regionEn,
                regionJa,
                $"{regionEn} location",
                $"{regionJa}の場所",
                content,
                sortOrder);
    }
}
