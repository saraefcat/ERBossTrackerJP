using System.Collections.ObjectModel;
using System.IO;
using ERBossTrackerJP.Commands;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Core.Presentation;
using ERBossTrackerJP.Save.Exceptions;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.Services.Tracking;

namespace ERBossTrackerJP.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ISaveFileLocator _saveFileLocator;
    private readonly ISaveLoadService _saveLoadService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly ITrackerSnapshotService _trackerSnapshotService;
    private readonly TrackerDisplayService _trackerDisplayService;
    private readonly ObservableCollection<SaveFileCandidate> _saveCandidates = [];
    private readonly ObservableCollection<CharacterSlot> _characterSlots = [];
    private readonly ObservableCollection<BossListItem> _bosses = [];
    private readonly ObservableCollection<RegionListItem> _regions = [];
    private readonly ObservableCollection<RegionFilterOption> _regionOptions = [];
    private readonly AsyncRelayCommand _browseFolderCommand;
    private readonly AsyncRelayCommand _refreshDefaultCommand;
    private readonly AsyncRelayCommand _reloadCommand;
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
    private bool _isSynchronizingRegionSelection;
    private bool _isUpdatingRegionOptions;
    private string _statusMessage = "セーブファイルを検索しています…";
    private string _saveLastWriteTimeText = "未読み込み";

    public MainWindowViewModel(
        ISaveFileLocator saveFileLocator,
        ISaveLoadService saveLoadService,
        IFolderPickerService folderPickerService,
        ITrackerSnapshotService trackerSnapshotService,
        TrackerDisplayService trackerDisplayService)
    {
        ArgumentNullException.ThrowIfNull(saveFileLocator);
        ArgumentNullException.ThrowIfNull(saveLoadService);
        ArgumentNullException.ThrowIfNull(folderPickerService);
        ArgumentNullException.ThrowIfNull(trackerSnapshotService);
        ArgumentNullException.ThrowIfNull(trackerDisplayService);

        _saveFileLocator = saveFileLocator;
        _saveLoadService = saveLoadService;
        _folderPickerService = folderPickerService;
        _trackerSnapshotService = trackerSnapshotService;
        _trackerDisplayService = trackerDisplayService;
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
            OnPropertyChanged(nameof(IsInteractionEnabled));
        }
    }

    public bool IsInteractionEnabled => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SaveLastWriteTimeText
    {
        get => _saveLastWriteTimeText;
        private set => SetProperty(ref _saveLastWriteTimeText, value);
    }

    public Task InitializeAsync() => RefreshDefaultCandidatesAsync();

    public Task RefreshDefaultCandidatesAsync() =>
        RunOperationAsync(async () =>
        {
            IReadOnlyList<SaveFileCandidate> candidates =
                _saveFileLocator.FindDefaultCandidates();

            if (candidates.Count == 0)
            {
                ReplaceSaveCandidates([]);
                StatusMessage =
                    "既定の保存場所にER0000.sl2が見つかりません。［フォルダー参照］から場所を指定してください。";
                return;
            }

            ReplaceSaveCandidates(candidates);
            SelectedSaveCandidate = candidates[0];
            await LoadSelectedSaveCoreAsync();
        });

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
            StatusMessage = "フォルダー選択画面を開けませんでした。";
            return;
        }

        if (selectedFolder is null)
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
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

    private async Task LoadSelectedSaveCoreAsync()
    {
        SaveFileCandidate? candidate = SelectedSaveCandidate;

        if (candidate is null)
        {
            StatusMessage = "読み込むセーブファイルを選択してください。";
            return;
        }

        int? previousSlotIndex = SelectedCharacter?.SlotIndex;
        LoadedSaveFile loadedSave = await _saveLoadService.LoadAsync(candidate.FilePath);
        CharacterSlot? nextSelection = previousSlotIndex.HasValue
            ? loadedSave.CharacterSlots.FirstOrDefault(
                slot => slot.SlotIndex == previousSlotIndex.Value)
            : loadedSave.CharacterSlots.FirstOrDefault();

        _loadedSave = loadedSave;
        ReplaceCharacterSlots(loadedSave.CharacterSlots);
        SaveLastWriteTimeText =
            loadedSave.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");

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

        LoadTrackerSnapshot(character);
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
            StatusMessage =
                $"「{character.Name}」のボス進捗を読み込みました。撃破 {Defeated} / {Total}。";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
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
    }

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
            StatusMessage = GetErrorMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
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
