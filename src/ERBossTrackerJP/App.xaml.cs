using System.Windows;
using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.Core.Presentation;
using ERBossTrackerJP.Save.Progress;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.Monitoring;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.Services.Tracking;
using ERBossTrackerJP.ViewModels;
using ERBossTrackerJP.Views;

namespace ERBossTrackerJP;

public partial class App : Application
{
    private MainWindowViewModel? _viewModel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var saveLoadService = new SaveLoadService();
        var trackerSnapshotService = new TrackerSnapshotService(
            saveLoadService,
            new BossProgressService(),
            EmbeddedBossDefinitions.Load());
        _viewModel = new MainWindowViewModel(
            new SaveFileLocator(),
            saveLoadService,
            new FolderPickerService(),
            new SaveFileMonitor(),
            trackerSnapshotService,
            new TrackerDisplayService());
        var window = new MainWindow
        {
            DataContext = _viewModel,
        };

        MainWindow = window;
        window.Show();
        await _viewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}
