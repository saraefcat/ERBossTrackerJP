using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.Core.Presentation;
using ERBossTrackerJP.Save.Progress;
using ERBossTrackerJP.Services.Diagnostics;
using ERBossTrackerJP.Services.Dialogs;
using ERBossTrackerJP.Services.Monitoring;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.Services.Settings;
using ERBossTrackerJP.Services.Tracking;
using ERBossTrackerJP.ViewModels;
using ERBossTrackerJP.Views;

namespace ERBossTrackerJP;

public partial class App : Application
{
    private DiagnosticLogService? _diagnosticLogService;
    private MainWindowViewModel? _viewModel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _diagnosticLogService = new DiagnosticLogService();
        _diagnosticLogService.TryStart();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Trace.WriteLine(
            $"[App] Starting. Version={GetVersion()}; Runtime={Environment.Version}; " +
            $"OS={Environment.OSVersion.VersionString}");

        try
        {
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
                new JsonUserSettingsService(),
                trackerSnapshotService,
                new TrackerDisplayService());
            var window = new MainWindow
            {
                DataContext = _viewModel,
            };

            MainWindow = window;
            window.Show();
            await _viewModel.InitializeAsync();
            Trace.WriteLine("[App] Startup completed.");
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[App] Startup failed: {exception}");
            Trace.Flush();
            DiagnosticLogService? diagnosticLogService = _diagnosticLogService;
            string logGuidance = diagnosticLogService?.IsEnabled == true &&
                                 diagnosticLogService.LogFilePath is not null
                ? $"\n\n診断ログ: {diagnosticLogService.LogFilePath}"
                : "\n\n診断ログは作成できませんでした。";
            MessageBox.Show(
                "アプリの起動中にエラーが発生しました。" + logGuidance,
                "ER Boss Tracker JP",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Trace.WriteLine($"[App] Exiting. Code={e.ApplicationExitCode}");
        _viewModel?.Dispose();
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _diagnosticLogService?.Dispose();
        base.OnExit(e);
    }

    private static string GetVersion() =>
        typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";

    private static void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs eventArgs) =>
        Trace.WriteLine(
            $"[App] Unhandled exception. Terminating={eventArgs.IsTerminating}: " +
            $"{eventArgs.ExceptionObject}");

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs eventArgs) =>
        Trace.WriteLine($"[App] Unobserved task exception: {eventArgs.Exception}");

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        Trace.WriteLine($"[App] Dispatcher unhandled exception: {eventArgs.Exception}");
        Trace.Flush();
    }
}
