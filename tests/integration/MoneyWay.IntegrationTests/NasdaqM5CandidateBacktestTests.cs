using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.Strategies;
using Xunit.Abstractions;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqM5CandidateBacktestTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, "100.01", true)]
    [InlineData(true, "105", true)]
    [InlineData(true, "99", false)]
    [InlineData(true, "100", false)]
    [InlineData(false, "99.99", true)]
    [InlineData(false, "95", true)]
    [InlineData(false, "101", false)]
    [InlineData(false, "100", false)]
    public void IndependentSyntheticCanonicalComparisonPreservesBaselineAndExposesActualCandidateEvidence(bool buy, string close, bool confirms)
    {
        var price = decimal.Parse(close, System.Globalization.CultureInfo.InvariantCulture);
        var baseline = new M5CandidateFixture(buy, price, baseline: true); var candidate = new M5CandidateFixture(buy, price);
        Assert.Equal(JsonSerializer.Serialize(baseline.Series), JsonSerializer.Serialize(candidate.Series));
        var before = baseline.Run(baseline.Inputs.Append(baseline.Swing())); var after = candidate.Run(candidate.Inputs.Append(candidate.Swing()));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, M5CandidateFixture.Result(before, 10));
        Assert.Equal(confirms ? RuleEvaluationResult.Passed : RuleEvaluationResult.Waiting, M5CandidateFixture.Result(after, 10));
        Assert.Equal(0, after.ReadyCount); Assert.Equal(0, before.ReadyCount);
        var observation = M5CandidateFixture.Frame(after, 10);
        Assert.Equal(candidate.Definition.Version, observation.StrategyVersion);
        var canonicalCapability = new StrategyReplayEvaluationCapabilityReport(candidate.Definition.StrategyId, candidate.Definition.Version, candidate.Definition.DisplayName,
            candidate.Definition.Rules.Select(r => new ReplayRuleEvaluationCapability(candidate.Definition.StrategyId, candidate.Definition.Version, r,
                MoneyWayNasdaqM5StrictCloseCandidate.Evaluators().Any(e => e.RuleId == r.RuleId) ? ReplayRuleEvaluationCapabilityStatus.Implemented : ReplayRuleEvaluationCapabilityStatus.NotImplemented,
                "Synthetic comparison: real exact-version registry coverage", null)));
        Assert.Equal(0, canonicalCapability.RequiredEvaluatorGapCount);
        var existing = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(baseline.Definition.StrategyId, baseline.Definition.Version)!;
        Assert.Equal(0, existing.RequiredEvaluatorGapCount);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Scope = "SyntheticAdmittedSoftwareComparison_NotAuthenticMarketBacktest",
            CandidateStatus = MoneyWayNasdaqM5StrictCloseCandidate.LifecycleStatus,
            buy,
            Close = close,
            Swing = 100,
            Before = M5CandidateFixture.Result(before, 10).ToString(),
            After = M5CandidateFixture.Result(after, 10).ToString(),
            observation.StrategyVersion,
            observation.AsOfUtc,
            CanonicalVerdict = after.Frames.Single(f => f.AsOfUtc == observation.AsOfUtc).Verdict.ToString(),
            Rules = observation.Evaluations.Select(r => new { RuleId = r.RuleId.Value, Result = r.Result.ToString(), r.EvaluatedAtUtc, r.EvidenceReference }),
            observation.RuleFacts,
            observation.WorkflowProgression,
            after.ReadyCount,
            CanonicalAdmissionOfRealMt5 = false,
            Activated = false
        }));
    }
}
