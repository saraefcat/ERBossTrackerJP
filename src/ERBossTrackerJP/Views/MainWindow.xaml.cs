using System.ComponentModel;
using System.Windows;
using ERBossTrackerJP.Services.Theming;
using ERBossTrackerJP.ViewModels;

namespace ERBossTrackerJP.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        DataContextChanged -= OnDataContextChanged;
        Closed -= OnClosed;
    }
}
