using System.Windows;
using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.Core.Presentation;
using ERBossTrackerJP.Save.Progress;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.Services.Tracking;
using ERBossTrackerJP.ViewModels;
using ERBossTrackerJP.Views;

namespace ERBossTrackerJP;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var saveLoadService = new SaveLoadService();
        var trackerSnapshotService = new TrackerSnapshotService(
            saveLoadService,
            new BossProgressService(),
            EmbeddedBossDefinitions.Load());
        var viewModel = new MainWindowViewModel(
            new SaveFileLocator(),
            saveLoadService,
            new FolderPickerService(),
            trackerSnapshotService,
            new TrackerDisplayService());
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }
}
