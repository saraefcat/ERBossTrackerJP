using System.Windows;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.ViewModels;
using ERBossTrackerJP.Views;

namespace ERBossTrackerJP;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new MainWindowViewModel(
            new SaveFileLocator(),
            new SaveLoadService(),
            new FolderPickerService());
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }
}
