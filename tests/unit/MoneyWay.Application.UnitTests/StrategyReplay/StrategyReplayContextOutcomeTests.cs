using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class StrategyReplayContextOutcomeTests
{
    private static readonly StrategyId Strategy = new("synthetic"); private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);

    [Fact]
    public void CompleteOutcomePreservesDomainOutcomeAndDerivesObservationMetadata()
    {
        var observation = Observation(Evaluation(Rule("A", 10, true), RuleEvaluationResult.Passed));
        var domain = new SequentialStrategyEvaluator().Evaluate(observation.Evaluations);
        var result = new StrategyReplayContextOutcome(observation, true, [], domain.Verdict, domain.Reason, domain);
        Assert.Same(observation, result.Observation); Assert.True(result.HasCompleteRequiredCoverage); Assert.Empty(result.MissingRequiredRuleIds);
        Assert.Equal(domain.Verdict, result.Verdict); Assert.Equal(domain.Reason, result.Reason); Assert.Same(domain, result.EvaluationOutcome);
        Assert.Equal(Strategy, result.StrategyId); Assert.Equal(Version, result.StrategyVersion); Assert.Equal(Provider, result.ProviderId); Assert.Equal(Symbol, result.Symbol); Assert.Equal(2, result.Step); Assert.Equal(AsOf, result.AsOfUtc);
    }

    [Fact]
    public void IncompleteOutcomeUsesExactSafeStateAndDefensivelyCopiesMissingRules()
    {
        var missing = new List<RuleId> { new("A") }; var result = new StrategyReplayContextOutcome(Observation(), false, missing, StrategyVerdict.DataUnavailable, StrategyReplayContextOutcome.IncompleteCoverageReason, null); missing.Clear();
        Assert.False(result.HasCompleteRequiredCoverage); Assert.Equal([new RuleId("A")], result.MissingRequiredRuleIds); Assert.Equal(StrategyVerdict.DataUnavailable, result.Verdict); Assert.Equal(StrategyReplayContextOutcome.IncompleteCoverageReason, result.Reason); Assert.Null(result.EvaluationOutcome);
    }

    [Fact]
    public void CompleteInvariantRejectsMissingOutcomeAndMismatchedVerdictOrReason()
    {
        var observation = Observation(Evaluation(Rule("A", 10, true), RuleEvaluationResult.Passed)); var domain = new SequentialStrategyEvaluator().Evaluate(observation.Evaluations);
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, true, [new("A")], domain.Verdict, domain.Reason, domain));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, true, [], domain.Verdict, domain.Reason, null));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, true, [], StrategyVerdict.Wait, domain.Reason, domain));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, true, [], domain.Verdict, "Different.", domain));
    }

    [Fact]
    public void IncompleteInvariantRejectsUnsafeShapes()
    {
        var observation = Observation(); var domain = new SequentialStrategyEvaluator().Evaluate([]);
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, false, [], StrategyVerdict.DataUnavailable, StrategyReplayContextOutcome.IncompleteCoverageReason, null));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, false, [new("A")], StrategyVerdict.DataUnavailable, StrategyReplayContextOutcome.IncompleteCoverageReason, domain));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, false, [new("A")], StrategyVerdict.Ready, StrategyReplayContextOutcome.IncompleteCoverageReason, null));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, false, [new("A")], StrategyVerdict.DataUnavailable, "Different.", null));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextOutcome(observation, false, [new("A"), new("A")], StrategyVerdict.DataUnavailable, StrategyReplayContextOutcome.IncompleteCoverageReason, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    public void InvalidReasonIsRejected(string? reason) => Assert.ThrowsAny<ArgumentException>(() => new StrategyReplayContextOutcome(Observation(), false, [new("A")], StrategyVerdict.DataUnavailable, reason!, null));

    private static StrategyRuleDefinition Rule(string id, int sequence, bool required, RuleDefinitionStatus status = RuleDefinitionStatus.Confirmed) => new(new(id), id, "stage", sequence, required, status, "description", "source");
    private static RuleEvaluation Evaluation(StrategyRuleDefinition rule, RuleEvaluationResult result) => new(rule.RuleId, rule.DefinitionStatus, result, rule.Sequence, rule.IsRequired, "Synthetic.", AsOf, null);
    private static StrategyReplayContextObservation Observation(params RuleEvaluation[] evaluations) => new(Strategy, Version, Provider, Symbol, 2, AsOf, evaluations.OrderBy(x => x.Sequence));
}
