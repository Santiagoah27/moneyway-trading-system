using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class StrategyReplayFrameOutcomeTests
{
    private static readonly StrategyId Strategy = new("test-strategy");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly EvaluateStrategyReplayFrameOutcomeUseCase useCase = new();

    [Fact]
    public void MissingRequiredRulesProduceSafeIncompleteOutcomeWithoutSyntheticEvaluations()
    {
        var definition = Definition(Rule("R-10", 10, true), Rule("R-20", 20, false), Rule("R-30", 30, true));
        var observation = Observation(definition, Evaluation(definition.Rules[1], RuleEvaluationResult.Passed));

        var outcome = useCase.Execute(definition, observation);

        Assert.False(outcome.HasCompleteRequiredCoverage);
        Assert.Equal([new RuleId("R-10"), new RuleId("R-30")], outcome.MissingRequiredRuleIds);
        Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
        Assert.Equal(StrategyReplayFrameOutcome.IncompleteCoverageReason, outcome.Reason);
        Assert.Null(outcome.EvaluationOutcome);
        Assert.Single(observation.Evaluations);
        Assert.Same(observation, outcome.Observation);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Passed, StrategyVerdict.Ready)]
    [InlineData(RuleEvaluationResult.Waiting, StrategyVerdict.Wait)]
    [InlineData(RuleEvaluationResult.Failed, StrategyVerdict.NoTrade)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired, StrategyVerdict.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.DataUnavailable, StrategyVerdict.DataUnavailable)]
    public void CompleteCoverageDelegatesVerdictToSequentialEvaluator(RuleEvaluationResult result, StrategyVerdict verdict)
    {
        var definition = Definition(Rule("R-1", 10, true));
        var outcome = useCase.Execute(definition, Observation(definition, Evaluation(definition.Rules[0], result)));
        Assert.True(outcome.HasCompleteRequiredCoverage); Assert.Empty(outcome.MissingRequiredRuleIds);
        Assert.NotNull(outcome.EvaluationOutcome); Assert.Equal(verdict, outcome.Verdict); Assert.Equal(outcome.EvaluationOutcome.Reason, outcome.Reason);
    }

    [Fact]
    public void OptionalRulesMayBeMissingOrFailWithoutBlockingReady()
    {
        var definition = Definition(Rule("required", 10, true), Rule("optional", 20, false));
        var required = Evaluation(definition.Rules[0], RuleEvaluationResult.Passed);
        Assert.Equal(StrategyVerdict.Ready, useCase.Execute(definition, Observation(definition, required)).Verdict);
        Assert.Equal(StrategyVerdict.Ready, useCase.Execute(definition, Observation(definition, required, Evaluation(definition.Rules[1], RuleEvaluationResult.Failed))).Verdict);
    }

    [Fact]
    public void NoRequiredRulesWithNoEvaluationsUsesExistingEmptyOutcome()
    {
        var definition = Definition(Rule("optional", 10, false));
        var outcome = useCase.Execute(definition, Observation(definition));
        Assert.True(outcome.HasCompleteRequiredCoverage); Assert.NotNull(outcome.EvaluationOutcome); Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("unknown")]
    [InlineData("sequence")]
    [InlineData("required")]
    [InlineData("status")]
    public void UseCaseRejectsIncompatibleObservation(string mismatch)
    {
        var definition = Definition(Rule("R-1", 10, true)); var rule = definition.Rules[0];
        var evaluation = mismatch switch
        {
            "unknown" => new RuleEvaluation(new("unknown"), rule.DefinitionStatus, RuleEvaluationResult.Passed, 10, true, "r", Start.AddMinutes(5), null),
            "sequence" => new RuleEvaluation(rule.RuleId, rule.DefinitionStatus, RuleEvaluationResult.Passed, 20, true, "r", Start.AddMinutes(5), null),
            "required" => new RuleEvaluation(rule.RuleId, rule.DefinitionStatus, RuleEvaluationResult.Passed, 10, false, "r", Start.AddMinutes(5), null),
            "status" => new RuleEvaluation(rule.RuleId, RuleDefinitionStatus.Candidate, RuleEvaluationResult.Passed, 10, true, "r", Start.AddMinutes(5), null),
            _ => Evaluation(rule, RuleEvaluationResult.Passed),
        };
        var observation = mismatch == "identity" ? Observation(definition, new StrategyId("other"), evaluation) : Observation(definition, evaluation);
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(definition, observation));
    }

    [Fact]
    public void OutcomeInvariantsAndDefensiveCopyAreEnforced()
    {
        var definition = Definition(Rule("R-1", 10, true)); var observation = Observation(definition); var missing = new List<RuleId> { definition.Rules[0].RuleId };
        var valid = new StrategyReplayFrameOutcome(observation, false, missing, StrategyVerdict.DataUnavailable, StrategyReplayFrameOutcome.IncompleteCoverageReason, null); missing.Clear();
        Assert.Single(valid.MissingRequiredRuleIds);
        Assert.Throws<ArgumentException>(() => new StrategyReplayFrameOutcome(observation, false, [], StrategyVerdict.DataUnavailable, StrategyReplayFrameOutcome.IncompleteCoverageReason, null));
        Assert.Throws<ArgumentException>(() => new StrategyReplayFrameOutcome(observation, false, [definition.Rules[0].RuleId], StrategyVerdict.Ready, StrategyReplayFrameOutcome.IncompleteCoverageReason, null));
        var sequential = new SequentialStrategyEvaluator().Evaluate([Evaluation(definition.Rules[0], RuleEvaluationResult.Passed)]);
        Assert.Throws<ArgumentException>(() => new StrategyReplayFrameOutcome(observation, true, [], StrategyVerdict.Wait, sequential.Reason, sequential));
    }

    [Fact]
    public void NullInputsAreRejected()
    {
        var definition = Definition(Rule("R", 1, true));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, Observation(definition)));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(definition, null!));
    }

    private static StrategyRuleDefinition Rule(string id, int sequence, bool required) => new(new(id), id, "stage", sequence, required, RuleDefinitionStatus.Confirmed, "description", "test");
    private static StrategyDefinition Definition(params StrategyRuleDefinition[] rules) => new(Strategy, Version, "Test strategy", "test", rules);
    private static RuleEvaluation Evaluation(StrategyRuleDefinition rule, RuleEvaluationResult result) => new(rule.RuleId, rule.DefinitionStatus, result, rule.Sequence, rule.IsRequired, "Synthetic result.", Start.AddMinutes(5), null);
    private static StrategyReplayFrameObservation Observation(StrategyDefinition definition, params RuleEvaluation[] evaluations) => Observation(definition, definition.StrategyId, evaluations);
    private static StrategyReplayFrameObservation Observation(StrategyDefinition definition, StrategyId strategyId, params RuleEvaluation[] evaluations)
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var timeframe = new Timeframe(5, TimeframeUnit.Minute); var candle = new Candle(provider, symbol, timeframe, Start, Start.AddMinutes(5), 100, 101, 99, 100, null);
        return new(strategyId, definition.Version, 1, candle.CloseTimeUtc, candle, evaluations.OrderBy(x => x.Sequence));
    }
}
