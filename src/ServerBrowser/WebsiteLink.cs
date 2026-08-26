using System;

namespace ServerBrowser;

public static class WebsiteLink {
    public static bool IsSupported(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(uri.Host);

    public static bool TryOpen(string? url, out string? error) {
        if (!IsSupported(url)) {
            error = "Only http and https website links can be opened";
            return false;
        }

        return ExternalLink.Open(url!, out error);
    }
}
