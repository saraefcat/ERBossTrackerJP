using System.Diagnostics;

namespace ERBossTrackerJP.Services.Clipboard;

public sealed class ClipboardService : IClipboardService
{
    public bool TrySetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[ClipboardService] Clipboard copy failed: {exception}");
            return false;
        }
    }
}
