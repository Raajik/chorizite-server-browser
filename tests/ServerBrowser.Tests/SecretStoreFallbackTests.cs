using ServerBrowser.Accounts;
using Xunit;

namespace ServerBrowser.Tests;

public class SecretStoreFallbackTests {
    [Fact]
    public void UnavailableStoreExplainsItselfInsteadOfThrowingInterop() {
        var store = new UnavailableSecretStore();

        var write = Assert.Throws<PlatformNotSupportedException>(() => store.Write("id", "secret"));
        var read = Assert.Throws<PlatformNotSupportedException>(() => store.Read("id"));

        Assert.Equal(UnavailableSecretStore.Reason, write.Message);
        Assert.Equal(UnavailableSecretStore.Reason, read.Message);
    }

    [Fact]
    public void UnavailableStoreStillAllowsAccountRemoval() {
        var store = new UnavailableSecretStore();

        store.Delete("id");
    }

    [Fact]
    public void AvailabilityProbeMatchesTheHostPlatform() {
        Assert.Equal(OperatingSystem.IsWindows(), WindowsCredentialStore.IsAvailable());
    }
}
