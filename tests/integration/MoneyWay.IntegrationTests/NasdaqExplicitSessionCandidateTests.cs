using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using Xunit.Abstractions;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqExplicitSessionCandidateTests(ITestOutputHelper output)
{
    [Fact]
    public void CanonicalCandidateReusesRulesAndRequiresEightAmWhilePublishedParentIsUnchanged()
    {
        var parent = MoneyWayNasdaqM5StrictCloseCandidate.Definition; var parentBytes = JsonSerializer.Serialize(parent);
        var baselineBytes = JsonSerializer.Serialize(MoneyWayNasdaqStrategyDefinition.Instance); var definition = MoneyWayNasdaqSessionCandidate.Definition;
        Assert.Equal(parent.Rules.Count, definition.Rules.Count);
        foreach (var (before, after) in parent.Rules.Zip(definition.Rules))
        {
            Assert.Equal(before.RuleId, after.RuleId); Assert.Equal(before.Sequence, after.Sequence); Assert.Equal(before.IsRequired, after.IsRequired); Assert.Equal(before.DefinitionStatus, after.DefinitionStatus);
            if (before.RuleId.Value != "NQ-LIQ-001") Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        }
        var start = new DateTimeOffset(2026, 6, 22, 22, 0, 0, TimeSpan.Zero); var provider = new MarketDataProviderId("synthetic-session-fixture"); var symbol = new MarketSymbol("US100"); var hour = new Timeframe(1, TimeframeUnit.Hour);
        var source = new CandleSeries(provider, symbol, hour, Enumerable.Range(0, 15).Select(i => new Candle(provider, symbol, hour, start.AddHours(i), start.AddHours(i + 1), 100, 110, 90, 100, null)));
        var report = MoneyWayNasdaqSessionCandidate.CreateBacktest().Execute(definition, [source]);
        var observations = report.OutcomeRun.StrategyRun.StrategyObservations;
        var seven = observations.Single(o => o.AsOfUtc == new DateTimeOffset(2026, 6, 23, 12, 0, 0, TimeSpan.Zero));
        var eight = observations.Single(o => o.AsOfUtc == new DateTimeOffset(2026, 6, 23, 13, 0, 0, TimeSpan.Zero));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, seven.Evaluations.Single(e => e.RuleId.Value == "NQ-LIQ-001").Result);
        var evaluation = eight.Evaluations.Single(e => e.RuleId.Value == "NQ-LIQ-001"); Assert.Equal(RuleEvaluationResult.Passed, evaluation.Result);
        Assert.Contains("13:00:00", evaluation.EvidenceReference); Assert.Contains("AuditedExactH1", evaluation.EvidenceReference);
        Assert.Equal(0, report.ReadyCount); Assert.DoesNotContain(MoneyWayNasdaqSessionCandidate.Definition, new StrategyDefinitionCatalog().GetAll());
        Assert.All(MoneyWayNasdaqSessionCandidate.Evaluators(), e => Assert.Equal(definition.Version, e.StrategyVersion));
        var capability = new StrategyReplayEvaluationCapabilityReport(definition.StrategyId, definition.Version, definition.DisplayName,
            definition.Rules.Select(r => new ReplayRuleEvaluationCapability(definition.StrategyId, definition.Version, r,
                MoneyWayNasdaqSessionCandidate.Evaluators().Any(e => e.RuleId == r.RuleId) ? ReplayRuleEvaluationCapabilityStatus.Implemented : ReplayRuleEvaluationCapabilityStatus.NotImplemented,
                "Actual opt-in candidate registry", null)));
        Assert.Equal(0, capability.RequiredEvaluatorGapCount);
        Assert.Equal(parentBytes, JsonSerializer.Serialize(parent)); Assert.Equal(baselineBytes, JsonSerializer.Serialize(MoneyWayNasdaqStrategyDefinition.Instance));
        output.WriteLine(JsonSerializer.Serialize(new { Scope = "SyntheticCanonicalCandidate_NotAuthenticAdmission", report.ReadyCount, seven.AsOfUtc, AtEight = eight, Activated = false }));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void DerivedCandidatePreservesStrictCloseAndWf2SeparationWithoutReady(bool buy, bool confirms)
    {
        var fixture = new M5CandidateFixture(buy, buy ? confirms ? 100.01m : 100 : confirms ? 99.99m : 100, sessionCandidate: true);
        var report = fixture.Run(fixture.Inputs.Append(fixture.Swing()));
        Assert.Equal(confirms ? RuleEvaluationResult.Passed : RuleEvaluationResult.Waiting, M5CandidateFixture.Result(report, 10)); Assert.Equal(0, report.ReadyCount);
        if (buy) Assert.Throws<ArgumentException>(() => new M5CandidateFixture(buy, sessionCandidate: true, path: NasdaqStrategyPath.WickfillScenario2));
        else
        {
            var wf2 = new M5CandidateFixture(false, sessionCandidate: true, path: NasdaqStrategyPath.WickfillScenario2);
            Assert.Equal(RuleEvaluationResult.NotApplicable, M5CandidateFixture.Result(wf2.Run(wf2.Inputs.Append(wf2.Swing())), 10));
        }
    }

    [Fact]
    public void LaterSourceExtensionDoesNotRewriteCanonicalEarlierObservations()
    {
        var f = new M5CandidateFixture(sessionCandidate: true); var inputs = f.Inputs.Append(f.Swing()).ToArray();
        var original = f.Run(inputs); var end = original.OutcomeRun.StrategyRun.StrategyObservations[^1].AsOfUtc;
        var extended = f.Series.Select(s => s.Timeframe == new Timeframe(1, TimeframeUnit.Hour)
            ? new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Append(new(s.ProviderId, s.Symbol, s.Timeframe, end.AddHours(1), end.AddHours(2), 100, 999, 1, 100, null))) : s).ToArray();
        var after = f.Run(inputs, extended);
        Assert.Equal(JsonSerializer.Serialize(original.OutcomeRun.StrategyRun.StrategyObservations),
            JsonSerializer.Serialize(after.OutcomeRun.StrategyRun.StrategyObservations.Where(o => o.AsOfUtc <= end)));
    }
}
