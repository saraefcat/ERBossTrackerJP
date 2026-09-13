using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace ERBossTrackerJP.Services.Theming;

public sealed class ApplicationThemeService : IApplicationThemeService
{
    private const string DarkThemePath = "Themes/DarkTheme.xaml";
    private const string LightThemePath = "Themes/LightTheme.xaml";
    private const string ResourceAssemblyPrefix =
        "/ERBossTrackerJP;component/";

    public ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Dark;

    public bool TryApply(ApplicationTheme theme)
    {
        if (theme is not ApplicationTheme.Dark and not ApplicationTheme.Light)
        {
            return false;
        }

        Application? application = Application.Current;

        if (application is null)
        {
            return false;
        }

        try
        {
            string themePath = theme == ApplicationTheme.Dark
                ? DarkThemePath
                : LightThemePath;
            var replacement = new ResourceDictionary
            {
                Source = new Uri(
                    ResourceAssemblyPrefix + themePath,
                    UriKind.Relative),
            };
            Collection<ResourceDictionary> dictionaries =
                application.Resources.MergedDictionaries;
            int existingIndex = FindThemeDictionary(dictionaries);

            if (existingIndex >= 0)
            {
                dictionaries[existingIndex] = replacement;
            }
            else
            {
                dictionaries.Insert(0, replacement);
            }

            CurrentTheme = theme;
            Trace.WriteLine($"[ApplicationThemeService] Theme applied: {theme}");
            return true;
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[ApplicationThemeService] Theme application failed: {exception}");
            return false;
        }
    }

    private static int FindThemeDictionary(
        Collection<ResourceDictionary> dictionaries)
    {
        for (int index = 0; index < dictionaries.Count; index++)
        {
            string? source = dictionaries[index].Source?.OriginalString;

            if (source is not null &&
                (source.EndsWith(DarkThemePath, StringComparison.OrdinalIgnoreCase) ||
                 source.EndsWith(LightThemePath, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }
}
