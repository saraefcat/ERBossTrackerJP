using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using ERBossTrackerJP.Services.Settings;
using ERBossTrackerJP.Services.Theming;
using ERBossTrackerJP.ViewModels;

namespace ERBossTrackerJP.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private bool _hasAppliedWindowPlacement;
    private WindowState _lastNonMinimizedWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyTitleBarTheme();
    }

    private void OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = eventArgs.NewValue as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyTitleBarTheme();
        ApplySavedWindowPlacement();
    }

    private void ApplySavedWindowPlacement()
    {
        if (_hasAppliedWindowPlacement ||
            _viewModel?.WindowPlacement is not { } placement)
        {
            return;
        }

        Rect virtualScreen = GetVirtualScreenBounds();
        Rect workArea = SystemParameters.WorkArea;
        Rect bounds = FitWindowBoundsToVisibleArea(
            placement,
            MinWidth,
            MinHeight,
            virtualScreen,
            workArea);

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        _lastNonMinimizedWindowState = placement.IsMaximized
            ? WindowState.Maximized
            : WindowState.Normal;
        WindowState = _lastNonMinimizedWindowState;
        _hasAppliedWindowPlacement = true;
    }

    internal static Rect FitWindowBoundsToVisibleArea(
        WindowPlacementSetting placement,
        double minimumWidth,
        double minimumHeight,
        Rect virtualScreen,
        Rect fallbackWorkArea)
    {
        ArgumentNullException.ThrowIfNull(placement);

        double width = Math.Min(
            Math.Max(placement.Width, minimumWidth),
            virtualScreen.Width);
        double height = Math.Min(
            Math.Max(placement.Height, minimumHeight),
            virtualScreen.Height);
        var candidate = new Rect(placement.Left, placement.Top, width, height);
        Rect visible = Rect.Intersect(candidate, virtualScreen);

        if (visible.IsEmpty || visible.Width < 96 || visible.Height < 48)
        {
            double fallbackWidth = Math.Min(width, fallbackWorkArea.Width);
            double fallbackHeight = Math.Min(height, fallbackWorkArea.Height);
            return new Rect(
                fallbackWorkArea.Left + (fallbackWorkArea.Width - fallbackWidth) / 2,
                fallbackWorkArea.Top + (fallbackWorkArea.Height - fallbackHeight) / 2,
                fallbackWidth,
                fallbackHeight);
        }

        return new Rect(
            Math.Clamp(
                candidate.Left,
                virtualScreen.Left,
                virtualScreen.Right - width),
            Math.Clamp(
                candidate.Top,
                virtualScreen.Top,
                virtualScreen.Bottom - height),
            width,
            height);
    }

    private static Rect GetVirtualScreenBounds()
    {
        var bounds = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        return bounds.Width > 0 && bounds.Height > 0
            ? bounds
            : SystemParameters.WorkArea;
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.IsDarkMode))
        {
            ApplyTitleBarTheme();
        }
    }

    private void ApplyTitleBarTheme()
    {
        ApplicationTheme theme = _viewModel?.IsDarkMode != false
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
        WindowThemeHelper.TryApply(this, theme);
    }

    private void CopyPathButton_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string path } &&
            !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                Clipboard.SetText(path);
            }
            catch (ExternalException exception)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[MainWindow] Clipboard copy failed: {exception}");
            }
        }
    }

    private void OnStateChanged(object? sender, EventArgs eventArgs)
    {
        if (WindowState != WindowState.Minimized)
        {
            _lastNonMinimizedWindowState = WindowState;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (eventArgs.Cancel || _viewModel is null)
        {
            return;
        }

        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;

        if (bounds.IsEmpty ||
            !double.IsFinite(bounds.Left) ||
            !double.IsFinite(bounds.Top) ||
            !double.IsFinite(bounds.Width) ||
            !double.IsFinite(bounds.Height) ||
            bounds.Width <= 0 ||
            bounds.Height <= 0)
        {
            return;
        }

        _viewModel.SaveWindowPlacement(new WindowPlacementSetting(
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            _lastNonMinimizedWindowState == WindowState.Maximized));
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        DataContextChanged -= OnDataContextChanged;
        StateChanged -= OnStateChanged;
        Closing -= OnClosing;
        Closed -= OnClosed;
    }
}
