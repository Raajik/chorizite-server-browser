using Xunit;

namespace ServerBrowser.Tests;

/// <summary>
/// Skips instead of failing on non-Windows hosts, so the suite still runs there.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute {
    public WindowsOnlyFactAttribute() {
        if (!OperatingSystem.IsWindows()) {
            Skip = "Requires Windows Credential Manager";
        }
    }
}
