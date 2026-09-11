using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ERBossTrackerJP.Services.Theming;

internal static class WindowThemeHelper
{
    private const int DwmwaUseImmersiveDarkModeBeforeWindows10_20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void TryApply(Window window, ApplicationTheme theme)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint windowHandle = new WindowInteropHelper(window).Handle;

        if (windowHandle == 0)
        {
            return;
        }

        try
        {
            int enabled = theme == ApplicationTheme.Dark ? 1 : 0;
            int result = DwmSetWindowAttribute(
                windowHandle,
                DwmwaUseImmersiveDarkMode,
                ref enabled,
                Marshal.SizeOf<int>());

            if (result != 0)
            {
                _ = DwmSetWindowAttribute(
                    windowHandle,
                    DwmwaUseImmersiveDarkModeBeforeWindows10_20H1,
                    ref enabled,
                    Marshal.SizeOf<int>());
            }
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[WindowThemeHelper] Title bar theme application failed: {exception}");
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
