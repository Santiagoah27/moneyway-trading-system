using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class EvaluateStrategyReplayContextOutcomeUseCaseTests
{
    private static readonly StrategyId Strategy = new("synthetic"); private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 10, 5, 0, TimeSpan.Zero); private readonly EvaluateStrategyReplayContextOutcomeUseCase useCase = new();

    [Fact]
    public void NullInputsAndStrategyIdentityMismatchesAreRejected()
    {
        var definition = Definition(Rule("A", 10, true));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, Observation(definition)));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(definition, null!));
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(definition, Observation(Definition(new("other"), Version, Rule("A", 10, true)))));
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(definition, Observation(Definition(Strategy, new("v2"), Rule("A", 10, true)))));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("sequence")]
    [InlineData("required")]
    [InlineData("status")]
    public void IncompatibleRuleMetadataIsRejected(string mismatch)
    {
        var rule = Rule("A", 10, true, RuleDefinitionStatus.Candidate); var definition = Definition(rule);
        var evaluation = mismatch switch
        {
            "unknown" => Evaluation(new("UNKNOWN"), 10, true, RuleDefinitionStatus.Candidate, RuleEvaluationResult.Passed),
            "sequence" => Evaluation(rule.RuleId, 20, true, RuleDefinitionStatus.Candidate, RuleEvaluationResult.Passed),
            "required" => Evaluation(rule.RuleId, 10, false, RuleDefinitionStatus.Candidate, RuleEvaluationResult.Passed),
            _ => Evaluation(rule.RuleId, 10, true, RuleDefinitionStatus.Confirmed, RuleEvaluationResult.Passed),
        };
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(definition, Observation(definition, evaluation)));
    }

    [Fact]
    public void MissingRequiredRulesAreReportedInDefinitionSequenceWithoutSyntheticEvaluations()
    {
        var definition = Definition(Rule("D", 40, true), Rule("B", 20, false), Rule("A", 10, true), Rule("C", 30, true));
        var observation = Observation(definition, From(definition.Rules.Single(x => x.RuleId == new RuleId("B")), RuleEvaluationResult.Passed)); var rulesBefore = definition.Rules.ToArray();
        var outcome = useCase.Execute(definition, observation);
        Assert.False(outcome.HasCompleteRequiredCoverage); Assert.Equal([new RuleId("A"), new RuleId("C"), new RuleId("D")], outcome.MissingRequiredRuleIds);
        Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict); Assert.Null(outcome.EvaluationOutcome); Assert.Single(observation.Evaluations); Assert.Equal(rulesBefore, definition.Rules);
    }

    [Fact]
    public void OptionalRulesMayBeMissingOrFailWithoutBlockingReady()
    {
        var definition = Definition(Rule("A", 10, true), Rule("B", 20, false), Rule("C", 30, true));
        var required = new[] { From(definition.Rules[0], RuleEvaluationResult.Passed), From(definition.Rules[2], RuleEvaluationResult.Passed) };
        Assert.Equal(StrategyVerdict.Ready, useCase.Execute(definition, Observation(definition, required)).Verdict);
        Assert.Equal(StrategyVerdict.Ready, useCase.Execute(definition, Observation(definition, [required[0], From(definition.Rules[1], RuleEvaluationResult.Failed), required[1]])).Verdict);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Passed, StrategyVerdict.Ready)]
    [InlineData(RuleEvaluationResult.Waiting, StrategyVerdict.Wait)]
    [InlineData(RuleEvaluationResult.Failed, StrategyVerdict.NoTrade)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired, StrategyVerdict.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.DataUnavailable, StrategyVerdict.DataUnavailable)]
    public void CompleteCoverageDelegatesVerdictToDomain(RuleEvaluationResult result, StrategyVerdict verdict)
    {
        var definition = Definition(Rule("A", 10, true)); var outcome = useCase.Execute(definition, Observation(definition, From(definition.Rules[0], result)));
        Assert.True(outcome.HasCompleteRequiredCoverage); Assert.Empty(outcome.MissingRequiredRuleIds); Assert.NotNull(outcome.EvaluationOutcome); Assert.Equal(verdict, outcome.Verdict); Assert.Equal(outcome.EvaluationOutcome.Reason, outcome.Reason);
    }

    [Fact]
    public void FirstRequiredBlockerComesFromSequentialEvaluator()
    {
        var definition = Definition(Rule("A", 10, true), Rule("B", 20, true), Rule("C", 30, true));
        var observation = Observation(definition, From(definition.Rules[0], RuleEvaluationResult.Passed), From(definition.Rules[1], RuleEvaluationResult.Waiting), From(definition.Rules[2], RuleEvaluationResult.Failed));
        var outcome = useCase.Execute(definition, observation);
        Assert.Equal(StrategyVerdict.Wait, outcome.Verdict); Assert.Equal(new RuleId("B"), outcome.EvaluationOutcome!.BlockingRuleId); Assert.Equal(20, outcome.EvaluationOutcome.BlockingSequence);
    }

    [Fact]
    public void NoRequiredRulesUseExistingEmptyDomainOutcome()
    {
        var definition = Definition(Rule("optional", 10, false)); var outcome = useCase.Execute(definition, Observation(definition));
        Assert.True(outcome.HasCompleteRequiredCoverage); Assert.NotNull(outcome.EvaluationOutcome); Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
    }

    [Theory]
    [InlineData(ReplayMarketDataObservabilityStatus.Sufficient)]
    [InlineData(ReplayMarketDataObservabilityStatus.ResolutionInsufficient)]
    public void ObservabilityDoesNotMapToRawResultOrStrategyVerdict(ReplayMarketDataObservabilityStatus status)
    {
        var definition = Definition(Rule("A", 10, true));
        var rawEvaluation = From(definition.Rules[0], RuleEvaluationResult.Passed);
        var observation = Observation(definition, [Assessment(definition, new("A"), status)], rawEvaluation);

        var outcome = useCase.Execute(definition, observation);

        Assert.Equal(RuleEvaluationResult.Passed, Assert.Single(outcome.Observation.Evaluations).Result);
        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
        Assert.Equal(status, Assert.Single(outcome.Observation.MarketDataObservability).Status);
    }

    [Fact]
    public void ObservabilityForUnknownRuleIsRejected()
    {
        var definition = Definition(Rule("A", 10, true));
        var observation = Observation(
            definition,
            [Assessment(definition, new("UNKNOWN"), ReplayMarketDataObservabilityStatus.Sufficient)],
            From(definition.Rules[0], RuleEvaluationResult.Passed));

        Assert.Throws<InvalidOperationException>(() => useCase.Execute(definition, observation));
    }

    private static StrategyDefinition Definition(params StrategyRuleDefinition[] rules) => Definition(Strategy, Version, rules);
    private static StrategyDefinition Definition(StrategyId strategy, StrategyVersion version, params StrategyRuleDefinition[] rules) => new(strategy, version, "Synthetic", "test", rules);
    private static StrategyRuleDefinition Rule(string id, int sequence, bool required, RuleDefinitionStatus status = RuleDefinitionStatus.Confirmed) => new(new(id), id, "stage", sequence, required, status, "description", "source");
    private static RuleEvaluation From(StrategyRuleDefinition rule, RuleEvaluationResult result) => Evaluation(rule.RuleId, rule.Sequence, rule.IsRequired, rule.DefinitionStatus, result);
    private static RuleEvaluation Evaluation(RuleId id, int sequence, bool required, RuleDefinitionStatus status, RuleEvaluationResult result) => new(id, status, result, sequence, required, "Synthetic.", AsOf, null);
    private static StrategyReplayContextObservation Observation(StrategyDefinition definition, params RuleEvaluation[] evaluations) =>
        Observation(definition, [], evaluations);
    private static StrategyReplayContextObservation Observation(
        StrategyDefinition definition,
        IEnumerable<ReplayMarketDataObservabilityAssessment> marketDataObservability,
        params RuleEvaluation[] evaluations) =>
        new(definition.StrategyId, definition.Version, Provider, Symbol, 1, AsOf, evaluations.OrderBy(x => x.Sequence), marketDataObservability: marketDataObservability);
    private static ReplayMarketDataObservabilityAssessment Assessment(
        StrategyDefinition definition,
        RuleId ruleId,
        ReplayMarketDataObservabilityStatus status) =>
        new(
            definition.StrategyId,
            definition.Version,
            Provider,
            Symbol,
            ruleId,
            1,
            AsOf,
            new("condition-a", AsOf.AddMinutes(-1), AsOf),
            new("condition-b", AsOf.AddMinutes(-1), AsOf),
            status,
            "Synthetic observability assessment.");
}
