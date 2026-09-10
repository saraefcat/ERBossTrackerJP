namespace ERBossTrackerJP.Services.Dialogs;

public interface IFolderPickerService
{
    string? SelectFolder(string? initialDirectory = null);
}
