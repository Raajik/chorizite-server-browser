using System;
using System.Diagnostics;

namespace ServerBrowser;

internal static class ExternalLink {
    internal static bool Open(string url, out string? error) {
        try {
            Process.Start(new ProcessStartInfo {
                FileName = url,
                UseShellExecute = true
            });
            error = null;
            return true;
        }
        catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }
}
