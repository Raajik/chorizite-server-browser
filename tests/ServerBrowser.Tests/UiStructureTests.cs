using Xunit;

namespace ServerBrowser.Tests;

public class UiStructureTests {
    [Fact]
    public void FilteringKeepsEveryServerRowInTheVirtualDom() {
        var lua = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "server-browser.lua"));

        Assert.Contains("rows[#rows + 1] = ServerRow(server, not matches(server))", lua);
        Assert.DoesNotContain("if matches(server) then rows[#rows + 1] = ServerRow(server) end", lua);
        Assert.Contains("filtered = isFiltered", lua);
    }
}