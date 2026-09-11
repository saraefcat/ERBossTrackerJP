namespace ERBossTrackerJP.Services.Theming;

public interface IApplicationThemeService
{
    ApplicationTheme CurrentTheme { get; }

    bool TryApply(ApplicationTheme theme);
}
