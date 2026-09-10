using System.IO;
using Microsoft.Win32;

namespace ERBossTrackerJP.Services.Dialogs;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? SelectFolder(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "ELDEN RINGのセーブフォルダーを選択してください",
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) &&
            Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
