using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqM5InclusivePolicyCommandTests : IDisposable
{
    private readonly string owned = Path.Combine(Path.GetTempPath(), "moneyway-inclusive-command-tests-" + Guid.NewGuid().ToString("N"));
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MoneyWay.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository unavailable.");
    }
    private static string FileAt(string relative) => Path.Combine(Root(), relative);
    private static string Evidence => FileAt("docs/data/strategy-lab/context-v1/development-review.json");
    private static string Sha(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
    private int Run(string input, string target, bool paired, string? evidence = null)
    {
        var args = new List<string> { "m5-swing-lab", "--input", input, "--output", target };
        if (paired) args.Add("--compare-inclusive");
        if (evidence is not null) { args.Add("--human-evidence"); args.Add(evidence); }
        using var output = new StringWriter(); using var error = new StringWriter();
        return new M5SwingLabCommand().Execute(args, output, error);
    }

    [Theory]
    [InlineData("synthetic", "docs/data/strategy-lab/m5-swing-v1/synthetic-disagreement.json", "src/frontend/moneyway-web/src/research/reports/synthetic-v1.json", 80, false)]
    [InlineData("development-ascending", "docs/data/strategy-lab/m5-blind-v1/inputs/m5-20260623-1300-ascending-v1.json", "src/frontend/moneyway-web/src/research/blind/data/reports/m5-20260623-1300-ascending-v1.json", 29381.7, true)]
    [InlineData("development-descending", "docs/data/strategy-lab/m5-blind-v1/inputs/m5-20260623-1400-descending-v1.json", "src/frontend/moneyway-web/src/research/blind/data/reports/m5-20260623-1400-descending-v1.json", 29811.6, true)]
    public void NewReportsReproduceAllEightVariantsAndPreserveOriginalHumanAndV1Artifacts(string name, string fixture, string legacy, double expectedInclusive, bool human)
    {
        var input = FileAt(fixture); var originalReport = FileAt(legacy); var inputHash = Sha(input); var legacyHash = Sha(originalReport); var evidenceHash = Sha(Evidence);
        var candidate = JsonSerializer.Serialize(MoneyWayNasdaqM5StrictCloseCandidate.Definition); var definitions = new StrategyDefinitionCatalog().GetAll(); var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var first = Path.Combine(owned, name); Assert.Equal(0, Run(input, first, true, human ? Evidence : null));
        Assert.False(File.Exists(Path.Combine(first, "comparison.json")));
        var bytes = File.ReadAllBytes(Path.Combine(first, "policy-comparison.json")); Assert.DoesNotContain((byte)'\r', bytes);
        var committed = FileAt($"docs/data/strategy-lab/m5-inclusive-v2/{name}");
        Assert.Equal(File.ReadAllBytes(Path.Combine(committed, "policy-comparison.json")), bytes);
        Assert.Equal(File.ReadAllBytes(Path.Combine(committed, "policy-comparison.md")), File.ReadAllBytes(Path.Combine(first, "policy-comparison.md")));
        using var json = JsonDocument.Parse(bytes); var root = json.RootElement; var pair = root.GetProperty("pairedComparison");
        Assert.Equal(legacyHash, root.GetProperty("originalExclusiveReportSha256").GetString()); Assert.Equal(inputHash, root.GetProperty("inputSha256").GetString());
        using var old = JsonDocument.Parse(File.ReadAllBytes(originalReport)); Assert.True(JsonElement.DeepEquals(old.RootElement.GetProperty("comparison"), pair.GetProperty("exclusiveV1")));
        var v1 = pair.GetProperty("exclusiveV1").GetProperty("results").EnumerateArray().ToArray();
        var v2 = pair.GetProperty("inclusiveV2").GetProperty("results").EnumerateArray().ToArray(); Assert.Equal(4, v1.Length); Assert.Equal(4, v2.Length);
        foreach (var (exclusive, inclusive) in v1.Zip(v2))
        {
            Assert.Equal((decimal)expectedInclusive, inclusive.GetProperty("proposedReferencePrice").GetDecimal());
            Assert.Equal("2.0.0", inclusive.GetProperty("algorithm").GetProperty("algorithmVersion").GetString());
            Assert.Equal(exclusive.GetProperty("pullbackStart").GetRawText(), inclusive.GetProperty("pullbackStart").GetRawText());
            Assert.Equal(exclusive.GetProperty("confirmationAtUtc").GetString(), inclusive.GetProperty("confirmationAtUtc").GetString());
            Assert.False(inclusive.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(inclusive.GetProperty("operationalSignal").GetBoolean());
        }
        if (human)
        {
            var annotation = root.GetProperty("humanReference"); Assert.Equal(evidenceHash, annotation.GetProperty("evidenceFileSha256").GetString()); Assert.Equal(1, annotation.GetProperty("originalLabelRevision").GetInt32());
            var agreements = annotation.GetProperty("agreements").EnumerateArray().ToArray(); Assert.Equal(8, agreements.Length);
            Assert.All(agreements.Skip(4), a => Assert.True(a.GetProperty("exactReferenceAgreement").GetBoolean()));
            Assert.All(agreements.Take(4), a => Assert.Equal(name == "development-descending", a.GetProperty("exactReferenceAgreement").GetBoolean()));
            Assert.All(agreements, a => { Assert.Equal(JsonValueKind.Null, a.GetProperty("independentHumanConfirmationUtc").ValueKind); Assert.Equal(JsonValueKind.Null, a.GetProperty("humanConfirmationLatency").ValueKind); });
        }
        else { Assert.Equal(JsonValueKind.Null, root.GetProperty("humanReference").ValueKind); Assert.Equal(new[] { -10m, -10m, -13m, -10m }, pair.GetProperty("differences").EnumerateArray().Select(p => p.GetProperty("signedPriceDifference").GetDecimal())); }
        var repeated = Path.Combine(owned, name + "-repeated"); Assert.Equal(0, Run(input, repeated, true, human ? Evidence : null)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(repeated, "policy-comparison.json")));
        Assert.Equal(2, Run(input, first, true, human ? Evidence : null)); // no report overwrite
        var defaultV1 = Path.Combine(owned, name + "-default-v1"); Assert.Equal(0, Run(input, defaultV1, false)); Assert.Equal(File.ReadAllBytes(originalReport), File.ReadAllBytes(Path.Combine(defaultV1, "comparison.json")));
        Assert.Equal(inputHash, Sha(input)); Assert.Equal(legacyHash, Sha(originalReport)); Assert.Equal(evidenceHash, Sha(Evidence));
        Assert.Equal(candidate, JsonSerializer.Serialize(MoneyWayNasdaqM5StrictCloseCandidate.Definition)); Assert.Equal(definitions, new StrategyDefinitionCatalog().GetAll()); Assert.Same(evaluators, MoneyWayReplayRuleEvaluators.GetAll());
    }

    [Theory]
    [InlineData("input")]
    [InlineData("report")]
    [InlineData("price")]
    [InlineData("cutoff")]
    [InlineData("revision")]
    [InlineData("scope")]
    public void HumanEvidenceCannotChangeCalculationOrBackdateAuthorization(string invalid)
    {
        Directory.CreateDirectory(owned); var node = JsonNode.Parse(File.ReadAllText(Evidence))!; var first = node["reviews"]![0]!;
        switch (invalid)
        {
            case "input": first["inputSha256"] = new string('0', 64); break;
            case "report": first["reportSha256"] = new string('0', 64); break;
            case "price": first["label"]!["price"] = 1; break;
            case "cutoff": first["label"]!["cutoffUtc"] = "2026-06-23T13:30:00Z"; break;
            case "revision": first["label"]!["revision"] = 2; break;
            default: node["canonicalAdmissionPermitted"] = true; break;
        }
        var altered = Path.Combine(owned, "invalid.json"); File.WriteAllText(altered, node.ToJsonString());
        var target = Path.Combine(owned, "unpublished");
        Assert.Equal(2, Run(FileAt("docs/data/strategy-lab/m5-blind-v1/inputs/m5-20260623-1300-ascending-v1.json"), target, true, altered)); Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void UnsupportedPriceUnitsFailClosedAndMissingInitializationRemainsAnAbstention()
    {
        Directory.CreateDirectory(owned); var fixture = FileAt("docs/data/strategy-lab/m5-swing-v1/synthetic-disagreement.json");
        var node = JsonNode.Parse(File.ReadAllText(fixture))!; node["priceUnit"] = "Percent";
        var unsupported = Path.Combine(owned, "units.json"); File.WriteAllText(unsupported, node.ToJsonString());
        Assert.Equal(2, Run(unsupported, Path.Combine(owned, "unsupported"), true)); Assert.False(Directory.Exists(Path.Combine(owned, "unsupported")));
        node.AsObject().Remove("priceUnit"); node["initialization"] = null; var missing = Path.Combine(owned, "missing.json"); File.WriteAllText(missing, node.ToJsonString());
        var target = Path.Combine(owned, "abstained"); Assert.Equal(0, Run(missing, target, true));
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(target, "policy-comparison.json")));
        foreach (var version in new[] { "exclusiveV1", "inclusiveV2" }) Assert.All(report.RootElement.GetProperty("pairedComparison").GetProperty(version).GetProperty("results").EnumerateArray(), r => Assert.Equal("AbstainedMissingInitialization", r.GetProperty("status").GetString()));
    }

    [Fact]
    public void PublishedInclusiveArtifactsContainOnlyExposedDevelopmentAndSoftwareInputs()
    {
        var directory = FileAt("docs/data/strategy-lab/m5-inclusive-v2");
        Assert.Equal(new[] { "development-ascending", "development-descending", "synthetic" }, Directory.GetDirectories(directory).Select(Path.GetFileName).Order());
        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file); Assert.DoesNotContain("m5-20260625-", text); Assert.DoesNotContain("m5-20260630-", text);
        }
    }
    public void Dispose() { if (Directory.Exists(owned)) Directory.Delete(owned, true); }
}
