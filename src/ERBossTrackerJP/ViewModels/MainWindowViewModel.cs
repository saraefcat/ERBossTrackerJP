using System.Collections.ObjectModel;
using System.IO;
using ERBossTrackerJP.Commands;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Exceptions;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.SaveFiles;

namespace ERBossTrackerJP.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ISaveFileLocator _saveFileLocator;
    private readonly ISaveLoadService _saveLoadService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly ObservableCollection<SaveFileCandidate> _saveCandidates = [];
    private readonly ObservableCollection<CharacterSlot> _characterSlots = [];
    private readonly AsyncRelayCommand _browseFolderCommand;
    private readonly AsyncRelayCommand _refreshDefaultCommand;
    private readonly AsyncRelayCommand _reloadCommand;
    private SaveFileCandidate? _selectedSaveCandidate;
    private CharacterSlot? _selectedCharacter;
    private LoadedSaveFile? _loadedSave;
    private bool _isBusy;
    private string _statusMessage = "セーブファイルを検索しています…";
    private string _saveLastWriteTimeText = "未読み込み";

    public MainWindowViewModel(
        ISaveFileLocator saveFileLocator,
        ISaveLoadService saveLoadService,
        IFolderPickerService folderPickerService)
    {
        ArgumentNullException.ThrowIfNull(saveFileLocator);
        ArgumentNullException.ThrowIfNull(saveLoadService);
        ArgumentNullException.ThrowIfNull(folderPickerService);

        _saveFileLocator = saveFileLocator;
        _saveLoadService = saveLoadService;
        _folderPickerService = folderPickerService;
        SaveCandidates = new ReadOnlyObservableCollection<SaveFileCandidate>(_saveCandidates);
        CharacterSlots = new ReadOnlyObservableCollection<CharacterSlot>(_characterSlots);
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
            SelectedCharacter = null;
            StatusMessage = value is null
                ? "セーブファイルが選択されていません。"
                : "選択したセーブファイルを読み込んでください。";
            _reloadCommand.NotifyCanExecuteChanged();
        }
    }

    public CharacterSlot? SelectedCharacter
    {
        get => _selectedCharacter;
        set => SetProperty(ref _selectedCharacter, value);
    }

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
        SelectedCharacter = nextSelection;
        SaveLastWriteTimeText =
            loadedSave.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");

        if (loadedSave.CharacterSlots.Count == 0)
        {
            StatusMessage = "有効なキャラクターが見つかりませんでした。";
        }
        else if (previousSlotIndex.HasValue && nextSelection is null)
        {
            StatusMessage =
                "選択中だったキャラクタースロットが無効になりました。別のキャラクターを選択してください。";
        }
        else
        {
            StatusMessage =
                $"キャラクターを{loadedSave.CharacterSlots.Count}件読み込みました。";
        }
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
}
