using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class StrategyReplayContextObservationTests
{
    private static readonly StrategyId Strategy = new("synthetic"); private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyAndPopulatedObservationsPreserveMetadataAndDefensivelyCopy()
    {
        var empty = Create([]); Assert.Equal(Strategy, empty.StrategyId); Assert.Equal(Version, empty.StrategyVersion); Assert.Equal(Provider, empty.ProviderId); Assert.Equal(Symbol, empty.Symbol); Assert.Equal(2, empty.Step); Assert.Equal(AsOf, empty.AsOfUtc); Assert.Equal(0, empty.EvaluationCount); Assert.Empty(empty.MarketDataObservability);
        var list = new List<RuleEvaluation> { Evaluation("A", 10) }; var populated = Create(list); list.Add(Evaluation("B", 20));
        Assert.Single(populated.Evaluations); Assert.Equal(1, populated.EvaluationCount);
        var multiple = Create([Evaluation("A", 10), Evaluation("B", 20)]); Assert.Equal([10, 20], multiple.Evaluations.Select(x => x.Sequence));
    }

    [Fact]
    public void InvalidStructureIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create([], step: 0));
        Assert.Throws<ArgumentException>(() => Create([], asOf: AsOf.ToOffset(TimeSpan.FromHours(-5))));
        Assert.Throws<ArgumentNullException>(() => Create(null!));
        Assert.Throws<ArgumentException>(() => Create(new RuleEvaluation[] { null! }));
        Assert.Throws<ArgumentException>(() => Create([Evaluation("A", 10), Evaluation("A", 20)]));
        Assert.Throws<ArgumentException>(() => Create([Evaluation("A", 10), Evaluation("B", 10)]));
        Assert.Throws<ArgumentException>(() => Create([Evaluation("A", 20), Evaluation("B", 10)]));
        Assert.Throws<ArgumentException>(() => Create([Evaluation("A", 10, AsOf.AddMinutes(1))]));
    }

    [Fact]
    public void ObservabilityIsAnImmutableAdditiveCompanionToRawEvaluations()
    {
        var original = Create([Evaluation("A", 10)]);
        var items = new List<ReplayMarketDataObservabilityAssessment>
        {
            Assessment("B", ReplayMarketDataObservabilityStatus.ResolutionInsufficient),
            Assessment("A", ReplayMarketDataObservabilityStatus.Sufficient),
        };

        var enriched = original.WithMarketDataObservability(items);
        items.Clear();

        Assert.Empty(original.MarketDataObservability);
        Assert.Equal(["A", "B"], enriched.MarketDataObservability.Select(item => item.RuleId.Value));
        Assert.Equal(original.Evaluations, enriched.Evaluations);
        Assert.Null(enriched.WorkflowProgression);
        Assert.Null(enriched.LifecycleProgression);
        Assert.Throws<InvalidOperationException>(() => enriched.WithMarketDataObservability([]));
    }

    [Fact]
    public void ObservabilityRejectsDuplicateOrMismatchedIdentity()
    {
        var assessment = Assessment("A", ReplayMarketDataObservabilityStatus.Sufficient);
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextObservation(
            Strategy, Version, Provider, Symbol, 2, AsOf, [], marketDataObservability: [assessment, assessment]));
        Assert.Throws<ArgumentException>(() => new StrategyReplayContextObservation(
            Strategy, Version, Provider, Symbol, 3, AsOf, [], marketDataObservability: [assessment]));
    }

    [Fact]
    public void ObservabilityDoesNotAlterWorkflowOrLifecycleProgression()
    {
        var evaluation = Evaluation("A", 10);
        var eligibility = new StrategyReplayRuleEligibility(new("A"), AsOf, true, [], [], true);
        var workflow = new StrategyReplayProgressionSnapshot(
            Strategy, Version, Provider, Symbol, 2, AsOf, [eligibility], [new("A")]);
        var lifecycle = new StrategyReplayLifecycleSnapshot(
            Strategy, Version, Provider, Symbol, 2, AsOf, [], []);
        var progressed = Create([evaluation]).WithProgressions(workflow, lifecycle);

        var enriched = progressed.WithMarketDataObservability(
            [Assessment("A", ReplayMarketDataObservabilityStatus.ResolutionInsufficient)]);

        Assert.Same(workflow, enriched.WorkflowProgression);
        Assert.Same(lifecycle, enriched.LifecycleProgression);
        Assert.Equal(RuleEvaluationResult.Passed, Assert.Single(enriched.Evaluations).Result);
    }

    private static StrategyReplayContextObservation Create(IEnumerable<RuleEvaluation> evaluations, int step = 2, DateTimeOffset? asOf = null) => new(Strategy, Version, Provider, Symbol, step, asOf ?? AsOf, evaluations);
    private static RuleEvaluation Evaluation(string id, int sequence, DateTimeOffset? at = null) => new(new(id), RuleDefinitionStatus.Confirmed, RuleEvaluationResult.Passed, sequence, true, "Synthetic.", at ?? AsOf, null);
    private static ReplayMarketDataObservabilityAssessment Assessment(string ruleId, ReplayMarketDataObservabilityStatus status) => new(
        Strategy,
        Version,
        Provider,
        Symbol,
        new(ruleId),
        2,
        AsOf,
        new($"{ruleId}-first", AsOf.AddMinutes(-1), AsOf),
        new($"{ruleId}-second", AsOf.AddMinutes(-1), AsOf),
        status,
        "Synthetic observability assessment.");
}
