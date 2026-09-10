using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay.Workflow;

public sealed class MoneyWayReplayWorkflowDefinitionsTests
{
    private static readonly StrategyDefinition Nasdaq = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 13, 30, 0, TimeSpan.Zero);
    private readonly AdvanceStrategyReplayProgressionUseCase progressionUseCase = new();

    [Fact]
    public void RegistryContainsOnlyTheExactCurrentNasdaqWorkflowGraph()
    {
        var first = MoneyWayReplayWorkflowDefinitions.GetAll();
        var second = MoneyWayReplayWorkflowDefinitions.GetAll();

        var workflow = Assert.Single(first);
        Assert.Same(first, second);
        Assert.Equal(new StrategyId("moneyway-nasdaq"), workflow.StrategyId);
        Assert.Equal(new StrategyVersion("nasdaq-0.1.0-draft"), workflow.StrategyVersion);
        Assert.Collection(
            workflow.RulePrerequisites,
            item => AssertDeclaration(item, "NQ-LIQ-003", "NQ-H4-001", "NQ-LIQ-002", "NQ-TIME-001"),
            item => AssertDeclaration(item, "NQ-M5-001", "NQ-LIQ-003"),
            item => AssertDeclaration(item, "NQ-FVG-001", "NQ-M5-001"),
            item => AssertDeclaration(item, "NQ-M1-001", "NQ-FVG-001"),
            item => AssertDeclaration(item, "NQ-M1-002", "NQ-M1-001"),
            item => AssertDeclaration(item, "NQ-M1-003", "NQ-M1-002"));
    }

    [Fact]
    public void ProductionCatalogValidatesAndResolvesOnlyTheExactNasdaqVersion()
    {
        var catalog = Catalog();

        var workflow = catalog.Find(Nasdaq.StrategyId, Nasdaq.Version);

        Assert.NotNull(workflow);
        Assert.Single(catalog.WorkflowDefinitions);
        Assert.Null(catalog.Find(Nasdaq.StrategyId, new("other-version")));
        var forex = MoneyWayForexStrategyDefinition.Instance;
        Assert.Null(catalog.Find(forex.StrategyId, forex.Version));
        Assert.Empty(StrategyReplayLifecyclePolicyCatalog.Empty.LifecyclePolicies);
        Assert.Null(StrategyReplayLifecyclePolicyCatalog.Empty.Find(Nasdaq.StrategyId, Nasdaq.Version));
    }

    [Fact]
    public void CutoffSessionCalculationAndUnrelatedEarlierRulesAreNotInferredAsPrerequisites()
    {
        var workflow = Workflow();
        var everyPrerequisite = workflow.RulePrerequisites.SelectMany(item => item.PrerequisiteRuleIds).ToArray();
        var liquidityTakePrerequisites = workflow.GetPrerequisiteRuleIds(new("NQ-LIQ-003"));

        Assert.DoesNotContain(new RuleId("NQ-TIME-002"), everyPrerequisite);
        Assert.DoesNotContain(new RuleId("NQ-LIQ-001"), everyPrerequisite);
        Assert.DoesNotContain(new RuleId("NQ-TIME-003"), liquidityTakePrerequisites);
        Assert.DoesNotContain(new RuleId("NQ-H4-002"), liquidityTakePrerequisites);
        Assert.True(Nasdaq.Rules.Single(item => item.RuleId == new RuleId("NQ-TIME-002")).IsRequired);
        Assert.True(Nasdaq.Rules.Single(item => item.RuleId == new RuleId("NQ-LIQ-001")).IsRequired);
        Assert.True(Nasdaq.Rules.Single(item => item.RuleId == new RuleId("NQ-TIME-003")).Sequence <
            Nasdaq.Rules.Single(item => item.RuleId == new RuleId("NQ-LIQ-003")).Sequence);
    }

    [Fact]
    public void MissingPrerequisitesBlockEligibilityWithoutChangingRawNasdaqEvaluation()
    {
        var raw = Evaluation("NQ-LIQ-003", RuleEvaluationResult.Passed, 1);
        var observation = Observation(1, raw);

        var progression = progressionUseCase.Execute(Workflow(), observation, null);

        Assert.Same(raw, Assert.Single(observation.Evaluations));
        Assert.Equal(RuleEvaluationResult.Passed, observation.Evaluations[0].Result);
        var eligibility = Assert.Single(progression.RuleEligibility);
        Assert.False(eligibility.IsEligible);
        Assert.Equal(
            [new RuleId("NQ-H4-001"), new RuleId("NQ-LIQ-002"), new RuleId("NQ-TIME-001")],
            eligibility.MissingPrerequisiteRuleIds);
        Assert.False(eligibility.EstablishesProgression);
    }

    [Fact]
    public void CanonicalRunnerUsesRealNasdaqMetadataAndKeepsRawEvaluationSeparateFromEligibility()
    {
        var evaluators = new IReplayRuleEvaluator[]
        {
            new ControlledEvaluator("NQ-H4-001", context => context.Step == 1),
            new ControlledEvaluator("NQ-LIQ-002", context => context.Step == 1),
            new ControlledEvaluator("NQ-TIME-001", context => context.Step == 1),
            new ControlledEvaluator("NQ-LIQ-003", _ => true),
        };
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(
            new RunMultiTimeframeReplayUseCase(),
            new(),
            new(evaluators),
            Catalog(),
            new()).Execute(Nasdaq, [Series(1, 2)]);

        var first = run.StrategyObservations[0];
        var second = run.StrategyObservations[1];
        Assert.Equal(RuleEvaluationResult.Passed, first.Evaluations.Single(item => item.RuleId == new RuleId("NQ-LIQ-003")).Result);
        Assert.False(Eligibility(first.WorkflowProgression!, "NQ-LIQ-003").IsEligible);
        Assert.True(Eligibility(second.WorkflowProgression!, "NQ-LIQ-003").IsEligible);
        Assert.Null(first.LifecycleProgression);
        Assert.Null(second.LifecycleProgression);
    }

    [Fact]
    public void AllLiquidityTakePrerequisitesMustBeEstablishedInAnEarlierFrame()
    {
        var first = Advance(1,
            null,
            Evaluation("NQ-H4-001", RuleEvaluationResult.Passed, 1),
            Evaluation("NQ-LIQ-002", RuleEvaluationResult.Passed, 1),
            Evaluation("NQ-TIME-001", RuleEvaluationResult.Passed, 1),
            Evaluation("NQ-LIQ-003", RuleEvaluationResult.Passed, 1));

        Assert.False(Eligibility(first, "NQ-LIQ-003").IsEligible);
        Assert.DoesNotContain(new RuleId("NQ-LIQ-003"), first.EstablishedRuleIds);

        var second = Advance(2, first, Evaluation("NQ-LIQ-003", RuleEvaluationResult.Passed, 2));

        Assert.True(Eligibility(second, "NQ-LIQ-003").IsEligible);
        Assert.Contains(new RuleId("NQ-LIQ-003"), second.EstablishedRuleIds);
    }

    [Fact]
    public void ExactNasdaqChainAdvancesOnlyThroughLaterEligibleObservations()
    {
        var snapshots = new List<StrategyReplayProgressionSnapshot>();
        snapshots.Add(Advance(1,
            null,
            Evaluation("NQ-H4-001", RuleEvaluationResult.Passed, 1),
            Evaluation("NQ-LIQ-002", RuleEvaluationResult.Passed, 1),
            Evaluation("NQ-TIME-001", RuleEvaluationResult.Passed, 1)));
        var chain = new[] { "NQ-LIQ-003", "NQ-M5-001", "NQ-FVG-001", "NQ-M1-001", "NQ-M1-002", "NQ-M1-003" };
        for (var index = 0; index < chain.Length; index++)
        {
            var step = index + 2;
            snapshots.Add(Advance(step, snapshots[^1], Evaluation(chain[index], RuleEvaluationResult.Passed, step)));
        }

        Assert.Equal(
            ["NQ-H4-001", "NQ-LIQ-002", "NQ-TIME-001", .. chain],
            snapshots[^1].EstablishedRuleIds.Select(item => item.Value));
        Assert.All(snapshots.Skip(1), snapshot => Assert.True(Assert.Single(snapshot.RuleEligibility).IsEligible));
    }

    [Fact]
    public void EarlierIneligibleDownstreamObservationIsNeverPromotedRetroactively()
    {
        var first = Advance(1, null, Evaluation("NQ-M5-001", RuleEvaluationResult.Passed, 1));
        var second = Advance(2,
            first,
            Evaluation("NQ-H4-001", RuleEvaluationResult.Passed, 2),
            Evaluation("NQ-LIQ-002", RuleEvaluationResult.Passed, 2),
            Evaluation("NQ-TIME-001", RuleEvaluationResult.Passed, 2));
        var third = Advance(3, second, Evaluation("NQ-LIQ-003", RuleEvaluationResult.Passed, 3));

        Assert.False(Eligibility(first, "NQ-M5-001").IsEligible);
        Assert.DoesNotContain(new RuleId("NQ-M5-001"), third.EstablishedRuleIds);

        var fourth = Advance(4, third, Evaluation("NQ-M5-001", RuleEvaluationResult.Passed, 4));

        Assert.True(Eligibility(fourth, "NQ-M5-001").IsEligible);
        Assert.Contains(new RuleId("NQ-M5-001"), fourth.EstablishedRuleIds);
    }

    private StrategyReplayProgressionSnapshot Advance(
        int step,
        StrategyReplayProgressionSnapshot? previous,
        params RuleEvaluation[] evaluations) => progressionUseCase.Execute(Workflow(), Observation(step, evaluations), previous);

    private static StrategyReplayRuleEligibility Eligibility(StrategyReplayProgressionSnapshot snapshot, string ruleId) =>
        snapshot.RuleEligibility.Single(item => item.RuleId == new RuleId(ruleId));

    private static StrategyReplayWorkflowCatalog Catalog() => new(
        new StrategyDefinitionCatalog().GetAll(),
        MoneyWayReplayWorkflowDefinitions.GetAll());

    private static StrategyReplayWorkflowDefinition Workflow() =>
        Catalog().Find(Nasdaq.StrategyId, Nasdaq.Version)!;

    private static void AssertDeclaration(
        StrategyReplayRulePrerequisite declaration,
        string downstreamRuleId,
        params string[] prerequisiteRuleIds)
    {
        Assert.Equal(new RuleId(downstreamRuleId), declaration.DownstreamRuleId);
        Assert.Equal(prerequisiteRuleIds, declaration.PrerequisiteRuleIds.Select(item => item.Value));
    }

    private static StrategyReplayContextObservation Observation(int step, params RuleEvaluation[] evaluations) => new(
        Nasdaq.StrategyId,
        Nasdaq.Version,
        Provider,
        Symbol,
        step,
        Start.AddMinutes(step),
        evaluations);

    private static RuleEvaluation Evaluation(string ruleId, RuleEvaluationResult result, int step)
    {
        var definition = Nasdaq.Rules.Single(item => item.RuleId == new RuleId(ruleId));
        return new(
            definition.RuleId,
            definition.DefinitionStatus,
            result,
            definition.Sequence,
            definition.IsRequired,
            "Controlled workflow metadata test.",
            Start.AddMinutes(step),
            null);
    }

    private static CandleSeries Series(params int[] closes) => new(
        Provider,
        Symbol,
        new Timeframe(1, TimeframeUnit.Minute),
        closes.Select(close => new Candle(
            Provider,
            Symbol,
            new Timeframe(1, TimeframeUnit.Minute),
            Start.AddMinutes(close - 1),
            Start.AddMinutes(close),
            100,
            101,
            99,
            100,
            null)));

    private sealed class ControlledEvaluator(string ruleId, Func<StrategyReplayContext, bool> passes) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Nasdaq.StrategyId;
        public StrategyVersion StrategyVersion => Nasdaq.Version;
        public RuleId RuleId { get; } = new(ruleId);

        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(
            passes(context) ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed,
            "Controlled workflow metadata test.",
            null);
    }
}
