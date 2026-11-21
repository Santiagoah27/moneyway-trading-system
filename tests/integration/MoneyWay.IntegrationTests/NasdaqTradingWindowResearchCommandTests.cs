using System.Text.Json;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqTradingWindowResearchCommandTests
{
    [Theory]
    [InlineData("2026-06-23T16:15:00Z", "Observable", true)]
    [InlineData("2026-06-23T16:30:00Z", "EventNotYetObservable", null)]
    [InlineData(null, "MissingTimestamp", null)]
    public void ReportsVersionedComparisonAtTheSuppliedCutoff(string? at, string evidence, bool? inside)
    {
        string[] args = ["nasdaq-window-research", "--as-of-utc", "2026-06-23T16:15:00Z"];
        if (at is not null) args = [.. args, "--event-utc", at];
        var output = new StringWriter();
        Assert.Equal(0, new NasdaqTradingWindowResearchCommand().Execute(args, output, TextWriter.Null));
        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;
        Assert.Equal("EXP-NQ-TRADING-WINDOW-1130-V1", root.GetProperty("candidateConfigurationId").GetString());
        Assert.Equal(evidence, root.GetProperty("evidenceStatus").GetString());
        Assert.False(root.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        Assert.False(root.GetProperty("operationalSignal").GetBoolean());
        if (inside is { } eligible)
        {
            Assert.Equal(eligible, root.GetProperty("candidate").GetProperty("withinNewSetupAndEntryTimeWindow").GetBoolean());
            Assert.False(root.GetProperty("publishedRuntime").GetProperty("withinNewSetupAndEntryTimeWindow").GetBoolean());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, root.GetProperty("eventUtc").ValueKind);
            Assert.Equal(JsonValueKind.Null, root.GetProperty("candidate").ValueKind);
        }
    }

    [Fact]
    public void RejectsLocalClockStringAndExplicitNonUtcOffset()
    {
        var command = new NasdaqTradingWindowResearchCommand();
        Assert.Equal(2, command.Execute(["nasdaq-window-research", "--as-of-utc", "2026-06-23T11:15:00"],
            TextWriter.Null, TextWriter.Null));
        Assert.Equal(2, command.Execute(["nasdaq-window-research", "--as-of-utc", "2026-06-23T16:15:00Z",
            "--event-utc", "2026-06-23T11:15:00-05:00"], TextWriter.Null, TextWriter.Null));
    }
}
