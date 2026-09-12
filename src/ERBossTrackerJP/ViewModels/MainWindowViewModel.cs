using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ERBossTrackerJP.Commands;
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

namespace ERBossTrackerJP.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ISaveFileLocator _saveFileLocator;
    private readonly ISaveLoadService _saveLoadService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly ISaveFileMonitor _saveFileMonitor;
    private readonly IUserSettingsService _userSettingsService;
    private readonly IApplicationThemeService _applicationThemeService;
    private readonly IObsTextFileOutput _obsTextFileOutput;
    private readonly ITrackerSnapshotService _trackerSnapshotService;
    private readonly TrackerDisplayService _trackerDisplayService;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly ObservableCollection<SaveFileCandidate> _saveCandidates = [];
    private readonly ObservableCollection<CharacterSlot> _characterSlots = [];
    private readonly ObservableCollection<BossListItem> _bosses = [];
    private readonly ObservableCollection<RegionListItem> _regions = [];
    private readonly ObservableCollection<RegionFilterOption> _regionOptions = [];
    private readonly AsyncRelayCommand _browseFolderCommand;
    private readonly AsyncRelayCommand _refreshDefaultCommand;
    private readonly AsyncRelayCommand _reloadCommand;
    private readonly AsyncRelayCommand _browseObsOutputFolderCommand;
    private readonly AsyncRelayCommand _testObsOutputCommand;
    private readonly AsyncRelayCommand _applyObsProgressFormatCommand;
    private CancellationTokenSource _obsOutputCancellationSource = new();
    private SaveFileCandidate? _selectedSaveCandidate;
    private CharacterSlot? _selectedCharacter;
    private RegionListItem? _selectedRegion;
    private LoadedSaveFile? _loadedSave;
    private TrackerSnapshot? _trackerSnapshot;
    private DisplayLanguage _displayLanguage = DisplayLanguage.Japanese;
    private BossCompletionFilter _completionFilter = BossCompletionFilter.All;
    private GameContent? _contentFilter;
    private string? _regionFilterId;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private bool _isAutoMonitoringEnabled = true;
    private bool _isProcessingAutomaticReload;
    private bool _pendingAutomaticReload;
    private bool _isSynchronizingRegionSelection;
    private bool _isUpdatingRegionOptions;
    private bool _suppressSettingsSave;
    private bool _disposed;
    private bool _isObsOutputEnabled;
    private ApplicationTheme _applicationTheme = ApplicationTheme.Dark;
    private string _obsOutputDirectory;
    private string _obsProgressFormatDraft;
    private string _obsProgressFormatStatusText = "適用済み";
    private string? _settingsSaveFilePath;
    private int? _settingsCharacterSlotIndex;
    private int? _pendingRestoredCharacterSlotIndex;
    private UserSettings _lastPersistedSettings = UserSettings.Default;
    private string _monitoringStatusText = "停止中";
    private string _obsOutputStatusText = "停止中";
    private string _obsLastOutputTimeText = "未出力";
    private string _settingsSaveStatusText =
        "適用または変更した内容は自動的に保存されます。";
    private bool _hasSettingsSaveError;
    private IReadOnlyList<string> _latestObsBossIds = [];
    private string _statusMessage = "セーブファイルを検索しています…";
    private bool _isProgressStatusMessageVisible = true;
    private string _saveLastWriteTimeText = "未読み込み";

    public MainWindowViewModel(
        ISaveFileLocator saveFileLocator,
        ISaveLoadService saveLoadService,
        IFolderPickerService folderPickerService,
        ISaveFileMonitor saveFileMonitor,
        IUserSettingsService userSettingsService,
        IApplicationThemeService applicationThemeService,
        IObsTextFileOutput obsTextFileOutput,
        ITrackerSnapshotService trackerSnapshotService,
        TrackerDisplayService trackerDisplayService)
    {
        ArgumentNullException.ThrowIfNull(saveFileLocator);
        ArgumentNullException.ThrowIfNull(saveLoadService);
        ArgumentNullException.ThrowIfNull(folderPickerService);
        ArgumentNullException.ThrowIfNull(saveFileMonitor);
        ArgumentNullException.ThrowIfNull(userSettingsService);
        ArgumentNullException.ThrowIfNull(applicationThemeService);
        ArgumentNullException.ThrowIfNull(obsTextFileOutput);
        ArgumentNullException.ThrowIfNull(trackerSnapshotService);
        ArgumentNullException.ThrowIfNull(trackerDisplayService);

        _saveFileLocator = saveFileLocator;
        _saveLoadService = saveLoadService;
        _folderPickerService = folderPickerService;
        _saveFileMonitor = saveFileMonitor;
        _userSettingsService = userSettingsService;
        _applicationThemeService = applicationThemeService;
        _obsTextFileOutput = obsTextFileOutput;
        _trackerSnapshotService = trackerSnapshotService;
        _trackerDisplayService = trackerDisplayService;
        UserSettings settings = userSettingsService.Load();
        _displayLanguage = settings.DisplayLanguage is
            DisplayLanguage.Japanese or DisplayLanguage.English
                ? settings.DisplayLanguage
                : DisplayLanguage.Japanese;
        _isAutoMonitoringEnabled = settings.IsAutoMonitoringEnabled;
        ApplicationTheme requestedTheme = settings.Theme is
            ApplicationTheme.Dark or ApplicationTheme.Light
                ? settings.Theme
                : ApplicationTheme.Dark;
        _applicationTheme = applicationThemeService.TryApply(requestedTheme)
            ? requestedTheme
            : applicationThemeService.CurrentTheme;
        if (settings.ObsOutputDirectory is not null)
        {
            _ = obsTextFileOutput.TrySetOutputDirectory(
                settings.ObsOutputDirectory);
        }

        if (settings.ObsProgressFormat is not null)
        {
            _ = obsTextFileOutput.TrySetProgressFormat(
                settings.ObsProgressFormat);
        }

        _obsOutputDirectory = obsTextFileOutput.OutputDirectory;
        _obsProgressFormatDraft = obsTextFileOutput.ProgressFormat;
        _isObsOutputEnabled = settings.IsObsOutputEnabled;
        _obsOutputStatusText = _isObsOutputEnabled ? "進捗待ち" : "停止中";
        _settingsSaveFilePath = settings.SaveFilePath;
        _settingsCharacterSlotIndex = IsValidCharacterSlotIndex(
            settings.CharacterSlotIndex)
                ? settings.CharacterSlotIndex
                : null;
        _pendingRestoredCharacterSlotIndex = _settingsCharacterSlotIndex;
        _lastPersistedSettings = new UserSettings(
            _settingsSaveFilePath,
            _settingsCharacterSlotIndex,
            _displayLanguage,
            _isAutoMonitoringEnabled,
            _applicationTheme,
            _isObsOutputEnabled,
            _obsOutputDirectory,
            obsTextFileOutput.ProgressFormat);
        _synchronizationContext = SynchronizationContext.Current;
        SaveCandidates = new ReadOnlyObservableCollection<SaveFileCandidate>(_saveCandidates);
        CharacterSlots = new ReadOnlyObservableCollection<CharacterSlot>(_characterSlots);
        Bosses = new ReadOnlyObservableCollection<BossListItem>(_bosses);
        Regions = new ReadOnlyObservableCollection<RegionListItem>(_regions);
        RegionOptions = new ReadOnlyObservableCollection<RegionFilterOption>(_regionOptions);
        LanguageOptions = Array.AsReadOnly(
        [
            new LanguageFilterOption(DisplayLanguage.Japanese, "日本語"),
            new LanguageFilterOption(DisplayLanguage.English, "English"),
        ]);
        CompletionOptions = Array.AsReadOnly(
        [
            new CompletionFilterOption(BossCompletionFilter.All, "すべて"),
            new CompletionFilterOption(BossCompletionFilter.Defeated, "撃破済み"),
            new CompletionFilterOption(BossCompletionFilter.Undefeated, "未撃破"),
        ]);
        ContentOptions = Array.AsReadOnly(
        [
            new ContentFilterOption(null, "すべて"),
            new ContentFilterOption(GameContent.BaseGame, "本編"),
            new ContentFilterOption(GameContent.ShadowOfTheErdtree, "DLC"),
        ]);
        RebuildRegionOptions();
        _browseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync, () => !IsBusy);
        _refreshDefaultCommand = new AsyncRelayCommand(
            RefreshDefaultCandidatesAsync,
            () => !IsBusy);
        _reloadCommand = new AsyncRelayCommand(
            LoadSelectedSaveAsync,
            () => !IsBusy && SelectedSaveCandidate is not null);
        _browseObsOutputFolderCommand = new AsyncRelayCommand(
            BrowseObsOutputFolderAsync,
            () => !IsBusy);
        _testObsOutputCommand = new AsyncRelayCommand(
            TestObsOutputAsync,
            () => !IsBusy && IsObsOutputEnabled && _trackerSnapshot is not null);
        _applyObsProgressFormatCommand = new AsyncRelayCommand(
            ApplyObsProgressFormatAsync,
            CanApplyObsProgressFormat);
        _saveFileMonitor.ChangeDetected += OnSaveFileChangeDetected;
        _saveFileMonitor.MonitoringError += OnSaveFileMonitoringError;
    }

    public string Title => "ER Boss Tracker JP";

    public ReadOnlyObservableCollection<SaveFileCandidate> SaveCandidates { get; }

    public ReadOnlyObservableCollection<CharacterSlot> CharacterSlots { get; }

    public ReadOnlyObservableCollection<BossListItem> Bosses { get; }

    public ReadOnlyObservableCollection<RegionListItem> Regions { get; }

    public ReadOnlyObservableCollection<RegionFilterOption> RegionOptions { get; }

    public IReadOnlyList<LanguageFilterOption> LanguageOptions { get; }

    public IReadOnlyList<CompletionFilterOption> CompletionOptions { get; }

    public IReadOnlyList<ContentFilterOption> ContentOptions { get; }

    public ContentFilterOption SelectedContentOption
    {
        get => ContentOptions.First(option => option.Value == ContentFilter);
        set
        {
            if (value is not null)
            {
                ContentFilter = value.Value;
            }
        }
    }

    public RegionFilterOption SelectedRegionOption
    {
        get => _regionOptions.FirstOrDefault(
            option => option.RegionId == RegionFilterId) ?? _regionOptions[0];
        set
        {
            if (value is not null)
            {
                RegionFilterId = value.RegionId;
            }
        }
    }

    public AsyncRelayCommand BrowseFolderCommand => _browseFolderCommand;

    public AsyncRelayCommand RefreshDefaultCommand => _refreshDefaultCommand;

    public AsyncRelayCommand ReloadCommand => _reloadCommand;

    public AsyncRelayCommand BrowseObsOutputFolderCommand =>
        _browseObsOutputFolderCommand;

    public AsyncRelayCommand TestObsOutputCommand => _testObsOutputCommand;

    public AsyncRelayCommand ApplyObsProgressFormatCommand =>
        _applyObsProgressFormatCommand;

    public bool IsAutoMonitoringEnabled
    {
        get => _isAutoMonitoringEnabled;
        set
        {
            if (!SetProperty(ref _isAutoMonitoringEnabled, value))
            {
                return;
            }

            if (!value)
            {
                _pendingAutomaticReload = false;
                _saveFileMonitor.Stop();
                MonitoringStatusText = "停止中";
                PersistUserSettings();
                return;
            }

            if (_loadedSave is not null)
            {
                ConfigureMonitoring(_loadedSave);
            }

            PersistUserSettings();
        }
    }

    public bool IsDarkMode
    {
        get => _applicationTheme == ApplicationTheme.Dark;
        set
        {
            ApplicationTheme requestedTheme = value
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

            if (_applicationTheme == requestedTheme ||
                !_applicationThemeService.TryApply(requestedTheme))
            {
                return;
            }

            _applicationTheme = requestedTheme;
            OnPropertyChanged();
            PersistUserSettings();
        }
    }

    public bool IsObsOutputEnabled
    {
        get => _isObsOutputEnabled;
        set
        {
            if (!SetProperty(ref _isObsOutputEnabled, value))
            {
                return;
            }

            _testObsOutputCommand.NotifyCanExecuteChanged();

            if (!value)
            {
                CancelPendingObsOutput();
                ObsOutputStatusText = "停止中";
            }
            else if (_trackerSnapshot is null)
            {
                ObsOutputStatusText = "進捗待ち";
            }
            else
            {
                QueueObsOutput(_trackerSnapshot, previousSnapshot: null);
            }

            PersistUserSettings();
        }
    }

    public string ObsOutputDirectory => _obsOutputDirectory;

    public string ObsRecommendedProgressFilePath =>
        Path.Combine(ObsOutputDirectory, ObsTextFileOutput.ProgressFileName);

    public string ObsProgressFormatDraft
    {
        get => _obsProgressFormatDraft;
        set
        {
            value ??= string.Empty;

            if (!SetProperty(ref _obsProgressFormatDraft, value))
            {
                return;
            }

            UpdateObsProgressFormatStatus();
            _applyObsProgressFormatCommand.NotifyCanExecuteChanged();
        }
    }

    public string ObsProgressFormatStatusText
    {
        get => _obsProgressFormatStatusText;
        private set => SetProperty(ref _obsProgressFormatStatusText, value);
    }

    public string ObsOutputStatusText
    {
        get => _obsOutputStatusText;
        private set => SetProperty(ref _obsOutputStatusText, value);
    }

    public string ObsLastOutputTimeText
    {
        get => _obsLastOutputTimeText;
        private set => SetProperty(ref _obsLastOutputTimeText, value);
    }

    public string SettingsSaveStatusText
    {
        get => _settingsSaveStatusText;
        private set => SetProperty(ref _settingsSaveStatusText, value);
    }

    public bool HasSettingsSaveError
    {
        get => _hasSettingsSaveError;
        private set => SetProperty(ref _hasSettingsSaveError, value);
    }

    public string MonitoringStatusText
    {
        get => _monitoringStatusText;
        private set => SetProperty(ref _monitoringStatusText, value);
    }

    public SaveFileCandidate? SelectedSaveCandidate
    {
        get => _selectedSaveCandidate;
        set
        {
            if (!SetProperty(ref _selectedSaveCandidate, value))
            {
                return;
            }

            _loadedSave = null;
            _pendingAutomaticReload = false;
            _saveFileMonitor.Stop();
            MonitoringStatusText = "停止中";
            ReplaceCharacterSlots([]);
            SetSelectedCharacter(null);
            ClearTrackerSnapshot();
            SaveLastWriteTimeText = "未読み込み";
            StatusMessage = value is null
                ? "セーブファイルが選択されていません。"
                : "選択したセーブファイルを読み込んでください。";
            _reloadCommand.NotifyCanExecuteChanged();
        }
    }

    public CharacterSlot? SelectedCharacter
    {
        get => _selectedCharacter;
        set => SetSelectedCharacter(value);
    }

    public RegionListItem? SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (!SetProperty(ref _selectedRegion, value) ||
                _isSynchronizingRegionSelection || value is null)
            {
                return;
            }

            RegionFilterId = value.RegionId;
        }
    }

    public DisplayLanguage DisplayLanguage
    {
        get => _displayLanguage;
        set
        {
            if (!SetProperty(ref _displayLanguage, value))
            {
                return;
            }

            RebuildRegionOptions();
            RefreshDisplay();
            OnPropertyChanged(nameof(ObsPreviewItems));
            if (_trackerSnapshot is not null && IsObsOutputEnabled)
            {
                QueueObsOutput(_trackerSnapshot, _trackerSnapshot);
            }

            PersistUserSettings();
        }
    }

    public BossCompletionFilter CompletionFilter
    {
        get => _completionFilter;
        set
        {
            if (SetProperty(ref _completionFilter, value))
            {
                RefreshDisplay();
            }
        }
    }

    public GameContent? ContentFilter
    {
        get => _contentFilter;
        set
        {
            if (SetProperty(ref _contentFilter, value))
            {
                OnPropertyChanged(nameof(SelectedContentOption));
                RefreshDisplay();
            }
        }
    }

    public string? RegionFilterId
    {
        get => _regionFilterId;
        set
        {
            if (_isUpdatingRegionOptions)
            {
                return;
            }

            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Region filter ID must be null or non-blank.",
                    nameof(value));
            }

            if (SetProperty(ref _regionFilterId, value))
            {
                OnPropertyChanged(nameof(SelectedRegionOption));
                RefreshDisplay();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            value ??= string.Empty;

            if (SetProperty(ref _searchText, value))
            {
                RefreshDisplay();
            }
        }
    }

    public int Defeated => _trackerSnapshot?.Defeated ?? 0;

    public int Total => _trackerSnapshot?.Total ?? 0;

    public int Remaining => _trackerSnapshot?.Remaining ?? 0;

    public double ProgressPercentage => _trackerSnapshot?.ProgressPercentage ?? 0;

    public string ProgressPercentageText => $"{ProgressPercentage:F1}%";

    public IReadOnlyList<ObsPreviewItem> ObsPreviewItems =>
    [
        new(ObsTextFileOutput.ProgressFileName,
            ObsProgressTextFormatter.Format(
                _obsTextFileOutput.ProgressFormat,
                Defeated,
                Remaining,
                Total,
                ProgressPercentage),
            "進捗を1行表示（推奨）"),
        new(ObsTextFileOutput.DefeatedFileName, Defeated.ToString(), "撃破数だけ"),
        new(ObsTextFileOutput.RemainingFileName, Remaining.ToString(), "未撃破数だけ"),
        new(ObsTextFileOutput.TotalFileName, Total.ToString(), "総数だけ"),
        new(ObsTextFileOutput.PercentageFileName, ProgressPercentageText, "進捗率だけ"),
        new(ObsTextFileOutput.LatestBossFileName, GetLatestObsBossPreview(), "最新撃破ボス"),
        new(ObsTextFileOutput.SnapshotFileName, $"ボス情報 {Total}件", "外部連携用（OBS対象外）"),
    ];

    public string ApplicationVersionText =>
        $"バージョン {typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "不明"}";

    public string VisibleBossCountText => $"{_bosses.Count}件表示";

    public string TrackerLastUpdateText => _trackerSnapshot is null
        ? "未読み込み"
        : _trackerSnapshot.UpdatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            _browseFolderCommand.NotifyCanExecuteChanged();
            _refreshDefaultCommand.NotifyCanExecuteChanged();
            _reloadCommand.NotifyCanExecuteChanged();
            _browseObsOutputFolderCommand.NotifyCanExecuteChanged();
            _testObsOutputCommand.NotifyCanExecuteChanged();
            _applyObsProgressFormatCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsInteractionEnabled));
        }
    }

    public bool IsInteractionEnabled => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                IsProgressStatusMessageVisible = true;
            }
        }
    }

    public bool IsProgressStatusMessageVisible
    {
        get => _isProgressStatusMessageVisible;
        private set => SetProperty(ref _isProgressStatusMessageVisible, value);
    }

    public string SaveLastWriteTimeText
    {
        get => _saveLastWriteTimeText;
        private set => SetProperty(ref _saveLastWriteTimeText, value);
    }

    public Task InitializeAsync() => RunOperationAsync(InitializeCoreAsync);

    public Task RefreshDefaultCandidatesAsync()
    {
        _pendingRestoredCharacterSlotIndex = null;
        return RunOperationAsync(RefreshDefaultCandidatesCoreAsync);
    }

    private async Task InitializeCoreAsync()
    {
        if (TryFindSavedCandidate(out IReadOnlyList<SaveFileCandidate> candidates,
                out SaveFileCandidate? savedCandidate))
        {
            ReplaceSaveCandidates(candidates);
            SelectedSaveCandidate = savedCandidate;
            await LoadSelectedSaveCoreAsync();
            return;
        }

        _pendingRestoredCharacterSlotIndex = null;
        await RefreshDefaultCandidatesCoreAsync();
    }

    private async Task RefreshDefaultCandidatesCoreAsync()
    {
        IReadOnlyList<SaveFileCandidate> candidates =
            _saveFileLocator.FindDefaultCandidates();

        if (candidates.Count == 0)
        {
            ReplaceSaveCandidates([]);
            StatusMessage =
                "既定の保存場所にER0000.sl2が見つかりません。［フォルダーを選択］から場所を指定してください。";
            return;
        }

        ReplaceSaveCandidates(candidates);
        SelectedSaveCandidate = candidates[0];
        await LoadSelectedSaveCoreAsync();
    }

    public async Task BrowseFolderAsync()
    {
        string? initialDirectory =
            SelectedSaveCandidate?.SaveDirectoryPath ?? _saveFileLocator.DefaultSearchRoot;
        string? selectedFolder;

        try
        {
            selectedFolder = _folderPickerService.SelectFolder(initialDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine(
                $"[MainWindowViewModel] Folder picker failed: {exception}");
            StatusMessage = "フォルダー選択画面を開けませんでした。";
            return;
        }

        if (selectedFolder is null)
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
            _pendingRestoredCharacterSlotIndex = null;
            IReadOnlyList<SaveFileCandidate> candidates =
                _saveFileLocator.FindCandidatesInFolder(selectedFolder);

            if (candidates.Count == 0)
            {
                StatusMessage =
                    "指定したフォルダーにER0000.sl2が見つかりませんでした。";
                return;
            }

            ReplaceSaveCandidates(candidates);
            SelectedSaveCandidate = candidates[0];
            await LoadSelectedSaveCoreAsync();
        });
    }

    public Task LoadSelectedSaveAsync() => RunOperationAsync(LoadSelectedSaveCoreAsync);

    public Task BrowseObsOutputFolderAsync()
    {
        string? selectedFolder;

        try
        {
            selectedFolder = _folderPickerService.SelectFolder(
                ObsOutputDirectory,
                "OBSテキストの出力フォルダーを選択してください");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine(
                $"[MainWindowViewModel] OBS output folder picker failed: {exception}");
            ObsOutputStatusText = "出力先を選択できません";
            return Task.CompletedTask;
        }

        if (selectedFolder is null)
        {
            return Task.CompletedTask;
        }

        if (!_obsTextFileOutput.TrySetOutputDirectory(selectedFolder))
        {
            ObsOutputStatusText = "出力先が無効です";
            return Task.CompletedTask;
        }

        _obsOutputDirectory = _obsTextFileOutput.OutputDirectory;
        OnPropertyChanged(nameof(ObsOutputDirectory));
        OnPropertyChanged(nameof(ObsRecommendedProgressFilePath));

        if (IsObsOutputEnabled && _trackerSnapshot is not null)
        {
            QueueObsOutput(_trackerSnapshot, _trackerSnapshot);
        }

        PersistUserSettings();
        return Task.CompletedTask;
    }

    public Task TestObsOutputAsync()
    {
        if (_trackerSnapshot is null || !IsObsOutputEnabled)
        {
            return Task.CompletedTask;
        }

        ObsOutputStatusText = "再出力中…";
        var update = new TrackerOutputUpdate(
            _trackerSnapshot,
            _trackerSnapshot,
            DisplayLanguage);
        return PublishObsOutputAsync(
            update,
            _obsOutputCancellationSource.Token);
    }

    public Task ApplyObsProgressFormatAsync()
    {
        if (!_obsTextFileOutput.TrySetProgressFormat(ObsProgressFormatDraft))
        {
            UpdateObsProgressFormatStatus();
            return Task.CompletedTask;
        }

        ObsProgressFormatStatusText = "適用済み";
        OnPropertyChanged(nameof(ObsPreviewItems));
        _applyObsProgressFormatCommand.NotifyCanExecuteChanged();
        PersistUserSettings();

        if (IsObsOutputEnabled && _trackerSnapshot is not null)
        {
            QueueObsOutput(_trackerSnapshot, _trackerSnapshot);
        }

        return Task.CompletedTask;
    }

    private bool CanApplyObsProgressFormat() =>
        !IsBusy &&
        !string.Equals(
            ObsProgressFormatDraft,
            _obsTextFileOutput.ProgressFormat,
            StringComparison.Ordinal) &&
        ObsProgressTextFormatter.TryValidate(
            ObsProgressFormatDraft,
            out _);

    private void UpdateObsProgressFormatStatus()
    {
        if (string.Equals(
                ObsProgressFormatDraft,
                _obsTextFileOutput.ProgressFormat,
                StringComparison.Ordinal))
        {
            ObsProgressFormatStatusText = "適用済み";
            return;
        }

        ObsProgressFormatStatusText = ObsProgressTextFormatter.TryValidate(
            ObsProgressFormatDraft,
            out string errorMessage)
                ? "［書式を適用］で反映します。"
                : errorMessage;
    }

    private async Task LoadSelectedSaveCoreAsync()
    {
        SaveFileCandidate? candidate = SelectedSaveCandidate;

        if (candidate is null)
        {
            StatusMessage = "読み込むセーブファイルを選択してください。";
            return;
        }

        int? previousSlotIndex =
            SelectedCharacter?.SlotIndex ?? _pendingRestoredCharacterSlotIndex;
        LoadedSaveFile loadedSave = await _saveLoadService.LoadAsync(candidate.FilePath);
        _pendingRestoredCharacterSlotIndex = null;
        CharacterSlot? nextSelection = previousSlotIndex.HasValue
            ? loadedSave.CharacterSlots.FirstOrDefault(
                slot => slot.SlotIndex == previousSlotIndex.Value)
            : loadedSave.CharacterSlots.FirstOrDefault();

        bool isSameSettingsSave = PathsEqual(
            _settingsSaveFilePath,
            loadedSave.SourcePath);
        _settingsSaveFilePath = loadedSave.SourcePath;

        if (!isSameSettingsSave)
        {
            _settingsCharacterSlotIndex = null;
        }

        _suppressSettingsSave = true;

        try
        {
            _loadedSave = loadedSave;
            ReplaceCharacterSlots(loadedSave.CharacterSlots);
            SaveLastWriteTimeText =
                loadedSave.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            ConfigureMonitoring(loadedSave);

            if (loadedSave.CharacterSlots.Count == 0)
            {
                SetSelectedCharacter(null);
                ClearTrackerSnapshot();
                StatusMessage = "有効なキャラクターが見つかりませんでした。";
            }
            else if (previousSlotIndex.HasValue && nextSelection is null)
            {
                SetSelectedCharacter(null);
                ClearTrackerSnapshot();
                StatusMessage =
                    "選択中だったキャラクタースロットが無効になりました。別のキャラクターを選択してください。";
            }
            else
            {
                SetSelectedCharacter(nextSelection);
            }
        }
        finally
        {
            _suppressSettingsSave = false;
        }

        PersistUserSettings();
        Trace.WriteLine(
            $"[MainWindowViewModel] Save loaded: {loadedSave.SourcePath}; " +
            $"Slots={loadedSave.CharacterSlots.Count}; " +
            $"SelectedSlot={SelectedCharacter?.SlotIndex.ToString() ?? "none"}");
    }

    private void SetSelectedCharacter(CharacterSlot? character)
    {
        if (ReferenceEquals(_selectedCharacter, character))
        {
            return;
        }

        _selectedCharacter = character;
        OnPropertyChanged(nameof(SelectedCharacter));

        if (character is null)
        {
            ClearTrackerSnapshot();
            return;
        }

        _settingsCharacterSlotIndex = character.SlotIndex;
        LoadTrackerSnapshot(character);
        PersistUserSettings();
    }

    private void LoadTrackerSnapshot(CharacterSlot character)
    {
        if (_loadedSave is null)
        {
            ClearTrackerSnapshot();
            return;
        }

        TrackerSnapshot? previousSnapshot = _trackerSnapshot;

        try
        {
            _trackerSnapshot = _trackerSnapshotService.Create(_loadedSave, character);
            RebuildRegionOptions();
            RefreshDisplay();
            if (IsObsOutputEnabled)
            {
                QueueObsOutput(_trackerSnapshot, previousSnapshot);
            }

            StatusMessage =
                $"「{character.Name}」のボス進捗を読み込みました。撃破 {Defeated} / {Total}。";
            IsProgressStatusMessageVisible = false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Trace.WriteLine(
                $"[MainWindowViewModel] Progress creation failed for slot " +
                $"{character.SlotIndex}: {exception}");

            if (previousSnapshot?.Character.SlotIndex != character.SlotIndex)
            {
                ClearTrackerSnapshot();
            }

            StatusMessage = GetErrorMessage(exception);
        }
    }

    private void RefreshDisplay()
    {
        TrackerSnapshot? snapshot = _trackerSnapshot;

        if (snapshot is null)
        {
            ReplaceBosses([]);
            ReplaceRegions([]);
            RaiseTrackerSummaryPropertiesChanged();
            return;
        }

        var filter = new BossListFilter(
            DisplayLanguage,
            CompletionFilter,
            RegionFilterId,
            ContentFilter,
            SearchText);
        TrackerDisplayModel display = _trackerDisplayService.Create(snapshot, filter);
        ReplaceBosses(display.Bosses);
        ReplaceRegions(display.Regions);
        SynchronizeSelectedRegion();
        RaiseTrackerSummaryPropertiesChanged();
    }

    private void RebuildRegionOptions()
    {
        string? selectedRegionId = _regionFilterId;
        _isUpdatingRegionOptions = true;

        try
        {
            _regionOptions.Clear();
            _regionOptions.Add(new RegionFilterOption(null, "すべて"));

            if (_trackerSnapshot is not null)
            {
                foreach (RegionProgress region in _trackerSnapshot.Regions)
                {
                    string name = DisplayLanguage == DisplayLanguage.Japanese
                        ? region.RegionJa
                        : region.RegionEn;
                    _regionOptions.Add(new RegionFilterOption(region.RegionId, name));
                }
            }
        }
        finally
        {
            _isUpdatingRegionOptions = false;
        }

        if (selectedRegionId is not null &&
            !_regionOptions.Any(option => option.RegionId == selectedRegionId))
        {
            _regionFilterId = null;
        }

        OnPropertyChanged(nameof(RegionFilterId));
        OnPropertyChanged(nameof(SelectedRegionOption));
    }

    private void SynchronizeSelectedRegion()
    {
        RegionListItem? selected = _regionFilterId is null
            ? null
            : _regions.FirstOrDefault(
                region => region.RegionId == _regionFilterId);
        _isSynchronizingRegionSelection = true;

        try
        {
            SelectedRegion = selected;
        }
        finally
        {
            _isSynchronizingRegionSelection = false;
        }
    }

    private void ClearTrackerSnapshot()
    {
        _trackerSnapshot = null;
        _latestObsBossIds = [];
        _regionFilterId = null;
        OnPropertyChanged(nameof(RegionFilterId));
        OnPropertyChanged(nameof(SelectedRegionOption));
        ReplaceBosses([]);
        ReplaceRegions([]);
        RebuildRegionOptions();
        SynchronizeSelectedRegion();
        RaiseTrackerSummaryPropertiesChanged();
    }

    private void RaiseTrackerSummaryPropertiesChanged()
    {
        OnPropertyChanged(nameof(Defeated));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ProgressPercentageText));
        OnPropertyChanged(nameof(VisibleBossCountText));
        OnPropertyChanged(nameof(TrackerLastUpdateText));
        OnPropertyChanged(nameof(ObsPreviewItems));
        _testObsOutputCommand.NotifyCanExecuteChanged();
    }

    private bool TryFindSavedCandidate(
        out IReadOnlyList<SaveFileCandidate> candidates,
        out SaveFileCandidate? savedCandidate)
    {
        candidates = [];
        savedCandidate = null;

        if (string.IsNullOrWhiteSpace(_settingsSaveFilePath))
        {
            return false;
        }

        try
        {
            string savedPath = Path.GetFullPath(_settingsSaveFilePath);
            string? directoryPath = Path.GetDirectoryName(savedPath);

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            candidates = _saveFileLocator.FindCandidatesInFolder(directoryPath);
            savedCandidate = candidates.FirstOrDefault(
                candidate => PathsEqual(candidate.FilePath, savedPath));
            return savedCandidate is not null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or IOException or
            UnauthorizedAccessException)
        {
            Trace.WriteLine(
                $"[MainWindowViewModel] Saved path restore failed: {exception}");
            candidates = [];
            savedCandidate = null;
            return false;
        }
    }

    private void PersistUserSettings()
    {
        if (_suppressSettingsSave || _disposed)
        {
            return;
        }

        var settings = new UserSettings(
            _settingsSaveFilePath,
            _settingsCharacterSlotIndex,
            DisplayLanguage,
            IsAutoMonitoringEnabled,
            _applicationTheme,
            IsObsOutputEnabled,
            ObsOutputDirectory,
            _obsTextFileOutput.ProgressFormat);

        if (settings == _lastPersistedSettings)
        {
            HasSettingsSaveError = false;
            SettingsSaveStatusText =
                "適用または変更した内容は自動的に保存されます。";
            return;
        }

        if (_userSettingsService.TrySave(settings))
        {
            _lastPersistedSettings = settings;
            HasSettingsSaveError = false;
            SettingsSaveStatusText =
                "適用または変更した内容は自動的に保存されます。";
            return;
        }

        HasSettingsSaveError = true;
        SettingsSaveStatusText =
            "設定を保存できませんでした。変更は再起動後に戻る可能性があります。";
    }

    private void CancelPendingObsOutput()
    {
        CancellationTokenSource previousSource = _obsOutputCancellationSource;
        _obsOutputCancellationSource = new CancellationTokenSource();
        previousSource.Cancel();
        previousSource.Dispose();
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    private static bool IsValidCharacterSlotIndex(int? slotIndex) =>
        slotIndex is >= 0 and < CharacterSlot.MaximumSlotCount;

    private async Task RunOperationAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Trace.WriteLine($"[MainWindowViewModel] Operation failed: {exception}");
            StatusMessage = GetErrorMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }

        if (_pendingAutomaticReload && !_isProcessingAutomaticReload)
        {
            await ProcessAutomaticReloadsAsync();
        }
    }

    private void ConfigureMonitoring(LoadedSaveFile loadedSave)
    {
        if (!IsAutoMonitoringEnabled)
        {
            return;
        }

        try
        {
            if (_saveFileMonitor.IsRunning &&
                string.Equals(
                    _saveFileMonitor.MonitoredFilePath,
                    loadedSave.SourcePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                _saveFileMonitor.UpdateBaseline(
                    loadedSave.SourcePath,
                    loadedSave.FileSize,
                    loadedSave.LastWriteTimeUtc);
            }
            else
            {
                _saveFileMonitor.Start(
                    loadedSave.SourcePath,
                    loadedSave.FileSize,
                    loadedSave.LastWriteTimeUtc);
            }

            MonitoringStatusText = "監視中";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[MainWindowViewModel] Monitoring start failed: {exception}");
            _saveFileMonitor.Stop();
            MonitoringStatusText = "監視を開始できません";
        }
    }

    private void OnSaveFileChangeDetected(
        object? sender,
        SaveFileChangedEventArgs eventArgs) =>
        PostToSynchronizationContext(
            () => _ = HandleSaveFileChangeDetectedAsync(eventArgs));

    private void OnSaveFileMonitoringError(
        object? sender,
        SaveFileMonitorErrorEventArgs eventArgs) =>
        PostToSynchronizationContext(() =>
        {
            if (_disposed || !IsAutoMonitoringEnabled)
            {
                return;
            }

            Trace.WriteLine(
                $"[MainWindowViewModel] File watcher error: {eventArgs.Exception}");
            MonitoringStatusText = "監視中（定期確認で継続）";
        });

    internal async Task HandleSaveFileChangeDetectedAsync(
        SaveFileChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (_disposed || !IsAutoMonitoringEnabled ||
            SelectedSaveCandidate is null ||
            !string.Equals(
                SelectedSaveCandidate.FilePath,
                eventArgs.FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _pendingAutomaticReload = true;

        if (IsBusy || _isProcessingAutomaticReload)
        {
            MonitoringStatusText = "更新待機中";
            return;
        }

        await ProcessAutomaticReloadsAsync();
    }

    private async Task ProcessAutomaticReloadsAsync()
    {
        if (_isProcessingAutomaticReload)
        {
            return;
        }

        _isProcessingAutomaticReload = true;

        try
        {
            while (_pendingAutomaticReload &&
                   IsAutoMonitoringEnabled &&
                   SelectedSaveCandidate is not null)
            {
                _pendingAutomaticReload = false;
                MonitoringStatusText = "更新を反映中…";
                await RunOperationAsync(LoadSelectedSaveCoreAsync);
            }
        }
        finally
        {
            _isProcessingAutomaticReload = false;

            if (IsAutoMonitoringEnabled && _saveFileMonitor.IsRunning)
            {
                MonitoringStatusText = "監視中";
            }
        }
    }

    private void PostToSynchronizationContext(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_synchronizationContext is null ||
            ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            action();
            return;
        }

        _synchronizationContext.Post(
            static state => ((Action)state!).Invoke(),
            action);
    }

    private void QueueObsOutput(
        TrackerSnapshot snapshot,
        TrackerSnapshot? previousSnapshot)
    {
        if (_disposed || !IsObsOutputEnabled)
        {
            return;
        }

        if (previousSnapshot?.Character.SlotIndex != snapshot.Character.SlotIndex)
        {
            previousSnapshot = null;
            _latestObsBossIds = [];
        }

        if (previousSnapshot is not null)
        {
            HashSet<string> previouslyDefeated = previousSnapshot.Bosses
                .Where(progress => progress.IsDefeated)
                .Select(progress => progress.Boss.Id)
                .ToHashSet(StringComparer.Ordinal);
            string[] newlyDefeated = snapshot.Bosses
                .Where(progress =>
                    progress.IsDefeated &&
                    !previouslyDefeated.Contains(progress.Boss.Id))
                .Select(progress => progress.Boss.Id)
                .ToArray();

            if (newlyDefeated.Length > 0)
            {
                _latestObsBossIds = newlyDefeated;
            }
        }

        OnPropertyChanged(nameof(ObsPreviewItems));

        ObsOutputStatusText = "出力中…";
        var update = new TrackerOutputUpdate(
            snapshot,
            previousSnapshot,
            DisplayLanguage);
        _ = PublishObsOutputAsync(
            update,
            _obsOutputCancellationSource.Token);
    }

    private async Task PublishObsOutputAsync(
        TrackerOutputUpdate update,
        CancellationToken cancellationToken)
    {
        try
        {
            await _obsTextFileOutput.PublishAsync(update, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            PostToSynchronizationContext(() =>
            {
                if (!_disposed && IsObsOutputEnabled)
                {
                    ObsOutputStatusText = "出力済み";
                    ObsLastOutputTimeText = DateTimeOffset.Now.ToString("HH:mm:ss");
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[MainWindowViewModel] OBS output failed: {exception}");
            PostToSynchronizationContext(() =>
            {
                if (!_disposed && IsObsOutputEnabled)
                {
                    ObsOutputStatusText = "出力エラー";
                }
            });
        }
    }

    private string GetLatestObsBossPreview()
    {
        if (_trackerSnapshot is null || _latestObsBossIds.Count == 0)
        {
            return "—";
        }

        string[] names = _trackerSnapshot.Bosses
            .Where(progress => _latestObsBossIds.Contains(
                progress.Boss.Id,
                StringComparer.Ordinal))
            .Select(progress => DisplayLanguage == DisplayLanguage.English
                ? progress.Boss.NameEn
                : progress.Boss.NameJa)
            .ToArray();
        return names.Length == 0 ? "—" : string.Join(" / ", names);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _obsOutputCancellationSource.Cancel();
        _obsOutputCancellationSource.Dispose();
        _saveFileMonitor.ChangeDetected -= OnSaveFileChangeDetected;
        _saveFileMonitor.MonitoringError -= OnSaveFileMonitoringError;
        _saveFileMonitor.Dispose();
    }

    private void ReplaceSaveCandidates(IEnumerable<SaveFileCandidate> candidates)
    {
        SelectedSaveCandidate = null;
        _saveCandidates.Clear();

        foreach (SaveFileCandidate candidate in candidates)
        {
            _saveCandidates.Add(candidate);
        }
    }

    private void ReplaceCharacterSlots(IEnumerable<CharacterSlot> characterSlots)
    {
        _characterSlots.Clear();

        foreach (CharacterSlot characterSlot in characterSlots)
        {
            _characterSlots.Add(characterSlot);
        }
    }

    private void ReplaceBosses(IEnumerable<BossListItem> bosses)
    {
        _bosses.Clear();

        foreach (BossListItem boss in bosses)
        {
            _bosses.Add(boss);
        }
    }

    private void ReplaceRegions(IEnumerable<RegionListItem> regions)
    {
        _regions.Clear();

        foreach (RegionListItem region in regions)
        {
            _regions.Add(region);
        }
    }

    private static string GetErrorMessage(Exception exception) => exception switch
    {
        SaveFileReadException { ErrorCode: SaveFileReadErrorCode.FileNotFound } =>
            "セーブファイルが見つかりません。場所を再確認してください。",
        SaveFileReadException { ErrorCode: SaveFileReadErrorCode.AccessDenied } =>
            "セーブファイルを読み取る権限がありません。",
        SaveFileReadException { ErrorCode: SaveFileReadErrorCode.FileTooLarge } =>
            "セーブファイルのサイズが対応上限を超えています。",
        SaveFileReadException { ErrorCode: SaveFileReadErrorCode.ChangedDuringRead } =>
            "セーブ処理中のため読み取れませんでした。少し待って再読み込みしてください。",
        SaveFileReadException =>
            "セーブファイルを一時的に読み取れません。少し待って再読み込みしてください。",
        SaveParseException { ErrorCode: SaveParseErrorCode.InvalidBnd4Magic } =>
            "選択したファイルは対応するELDEN RINGセーブではありません。",
        SaveParseException { ErrorCode: SaveParseErrorCode.EmptyCharacterSlot } =>
            "選択したキャラクタースロットは空です。",
        SaveParseException =>
            "セーブデータを解析できませんでした。対応バージョンか確認してください。",
        DirectoryNotFoundException =>
            "指定したフォルダーが見つかりません。",
        UnauthorizedAccessException =>
            "指定したフォルダーへアクセスできません。",
        IOException =>
            "セーブフォルダーの確認中にエラーが発生しました。",
        _ => "予期しないエラーが発生しました。",
    };

    internal LoadedSaveFile? LoadedSave => _loadedSave;

    internal TrackerSnapshot? TrackerSnapshot => _trackerSnapshot;
}

public sealed record LanguageFilterOption(DisplayLanguage Value, string Label);

public sealed record CompletionFilterOption(BossCompletionFilter Value, string Label);

public sealed record ContentFilterOption(GameContent? Value, string Label);

public sealed record RegionFilterOption(string? RegionId, string Label);

public sealed record ObsPreviewItem(string FileName, string Contents, string Usage);
