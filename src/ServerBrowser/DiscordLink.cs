using System;
using System.Diagnostics;

namespace ServerBrowser;

public static class DiscordLink {
    public static bool IsSupported(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "discord.gg", StringComparison.OrdinalIgnoreCase);

    public static bool TryOpen(string? url, out string? error) {
        if (!IsSupported(url)) {
            error = "Only https://discord.gg invite links can be opened";
            return false;
        }

        try {
            Process.Start(new ProcessStartInfo {
                FileName = url!,
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
