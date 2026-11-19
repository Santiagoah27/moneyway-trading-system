using System.Security.Cryptography;
using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class M5SwingLabCommandTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "moneyway-m5-lab-tests-" + Guid.NewGuid().ToString("N"));
    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable."), "docs/data/strategy-lab/m5-swing-v1/synthetic-disagreement.json");
    }
    private int Run(string input, string output, out string stdout, out string stderr)
    {
        using var o = new StringWriter(); using var e = new StringWriter();
        var code = new M5SwingLabCommand().Execute(["m5-swing-lab", "--input", input, "--output", output], o, e);
        stdout = o.ToString(); stderr = e.ToString(); return code;
    }
    [Fact]
    public void RealCommandPublishesDeterministicMachineAndHumanReportsWithoutTouchingInputOrStrategyCatalogs()
    {
        var source = Fixture(); var hash = SHA256.HashData(File.ReadAllBytes(source));
        var baseline = new StrategyDefinitionCatalog().GetAll(); var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var candidate = JsonSerializer.Serialize(MoneyWayNasdaqM5StrictCloseCandidate.Definition);
        Assert.Equal(0, Run(source, Path.Combine(root, "first"), out var output, out var error)); Assert.Equal("", error);
        Assert.Contains("ExperimentalResearchOnly", output);
        var jsonPath = Path.Combine(root, "first/comparison.json"); using var json = JsonDocument.Parse(File.ReadAllBytes(jsonPath));
        Assert.Equal(Convert.ToHexStringLower(hash), json.RootElement.GetProperty("inputSha256").GetString());
        var report = json.RootElement.GetProperty("comparison"); var results = report.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(4, results.Length); Assert.Equal(new[] { 90m, 90m, 93m, 90m }, results.Select(r => r.GetProperty("proposedReferencePrice").GetDecimal()));
        Assert.False(report.GetProperty("allAlgorithmsAgree").GetBoolean()); Assert.False(report.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(report.GetProperty("operationalSignal").GetBoolean());
        Assert.All(results, r => { Assert.Equal("ExperimentalMoneyWayHypothesis_NotMentorConfirmed", r.GetProperty("classification").GetString()); Assert.False(r.GetProperty("mentorRuleConfirmed").GetBoolean()); });
        var md = File.ReadAllText(Path.Combine(root, "first/comparison.md")); Assert.Contains("EXP-M5-SWING-C-V1", md); Assert.Contains("synthetic-m5-swing-disagreement-v1", md); Assert.Contains("2026-11-19T13:30:00", md); Assert.Contains("No winner selected", md);
        Assert.Equal(0, Run(source, Path.Combine(root, "second"), out _, out _));
        Assert.Equal(File.ReadAllBytes(jsonPath), File.ReadAllBytes(Path.Combine(root, "second/comparison.json")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(root, "first/comparison.md")), File.ReadAllBytes(Path.Combine(root, "second/comparison.md")));
        Assert.Equal(2, Run(source, Path.Combine(root, "first"), out _, out _)); Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.Equal(candidate, JsonSerializer.Serialize(MoneyWayNasdaqM5StrictCloseCandidate.Definition)); Assert.Equal(baseline, new StrategyDefinitionCatalog().GetAll()); Assert.Same(evaluators, MoneyWayReplayRuleEvaluators.GetAll()); Assert.Equal(18, evaluators.Count);
        Assert.False(typeof(IStrategyReplayInputObservation).IsAssignableFrom(typeof(M5SwingComparisonReport)));
    }
    [Theory]
    [InlineData("trust")]
    [InlineData("duplicate")]
    [InlineData("invalid-ohlc")]
    [InlineData("wrong-timeframe")]
    [InlineData("oversized")]
    public void InvalidInputsFailWithoutPublishingAReportDirectory(string kind)
    {
        Directory.CreateDirectory(root); var source = Path.Combine(root, "request.json"); var raw = File.ReadAllText(Fixture());
        raw = kind switch
        {
            "trust" => raw.Replace("\"seriesId\":", "\"canonicalAdmissionPermitted\":true,\"seriesId\":", StringComparison.Ordinal),
            "duplicate" => raw.Replace("\"seriesId\":", "\"seriesId\":\"duplicate\",\"seriesId\":", StringComparison.Ordinal),
            "invalid-ohlc" => raw.Replace("\"high\": 110", "\"high\": 1", StringComparison.Ordinal),
            "wrong-timeframe" => raw.Replace("\"5m\"", "\"1h\"", StringComparison.Ordinal),
            _ => new string(' ', 8 * 1024 * 1024 + 1)
        };
        File.WriteAllText(source, raw); var hash = SHA256.HashData(File.ReadAllBytes(source)); var target = Path.Combine(root, "rejected");
        Assert.Equal(2, Run(source, target, out _, out var error)); Assert.NotEmpty(error); Assert.False(Directory.Exists(target)); Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source)));
    }
    [Fact]
    public void RetrospectiveDeclarationNeverGrantsAuthenticationOrCanonicalAdmission()
    {
        Directory.CreateDirectory(root); var source = Path.Combine(root, "documentary.json");
        File.WriteAllText(source, File.ReadAllText(Fixture()).Replace("\"Synthetic\"", "\"RetrospectiveDocumentary\"", StringComparison.Ordinal));
        Assert.Equal(0, Run(source, Path.Combine(root, "documentary"), out _, out _));
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "documentary/comparison.json")));
        var comparison = report.RootElement.GetProperty("comparison"); Assert.Equal("RetrospectiveDocumentary", comparison.GetProperty("input").GetProperty("dataOrigin").GetString());
        Assert.False(comparison.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.Contains("Unauthenticated", comparison.GetProperty("input").GetProperty("historicalAvailability").GetString());
    }
    [Fact]
    public void MissingInitializationProducesInspectableAbstentionsInsteadOfAnAutomaticFirstSwing()
    {
        Directory.CreateDirectory(root); var source = Path.Combine(root, "missing.json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Fixture()))!; node["initialization"] = null; File.WriteAllText(source, node.ToJsonString());
        Assert.Equal(0, Run(source, Path.Combine(root, "missing"), out _, out _));
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "missing/comparison.json")));
        Assert.All(report.RootElement.GetProperty("comparison").GetProperty("results").EnumerateArray(), r => Assert.Equal("AbstainedMissingInitialization", r.GetProperty("status").GetString()));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
