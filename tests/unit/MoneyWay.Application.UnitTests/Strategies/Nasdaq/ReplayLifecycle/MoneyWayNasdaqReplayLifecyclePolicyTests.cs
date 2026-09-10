using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayLifecycle;

public sealed class MoneyWayNasdaqReplayLifecyclePolicyTests
{
    private static readonly StrategyDefinition Nasdaq = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly TimeZoneInfo Bogota = ResolveBogotaTimeZone();
    private static readonly StrategyReplayWorkflowDefinition Workflow = new StrategyReplayWorkflowCatalog(
        new StrategyDefinitionCatalog().GetAll(),
        MoneyWayReplayWorkflowDefinitions.GetAll()).Find(Nasdaq.StrategyId, Nasdaq.Version)!;
    private static readonly MoneyWayNasdaqReplayLifecyclePolicy Policy = new();
    private readonly AdvanceStrategyReplayLifecycleUseCase useCase = new(new());

    [Fact]
    public void RegistryResolvesOnlyTheExactNasdaqVersionAndLeavesForexUnconfigured()
    {
        var policies = MoneyWayReplayLifecyclePolicies.GetAll();
        var registered = Assert.Single(policies);
        var catalog = new StrategyReplayLifecyclePolicyCatalog(new StrategyDefinitionCatalog().GetAll(), policies);

        Assert.IsType<MoneyWayNasdaqReplayLifecyclePolicy>(registered);
        Assert.Same(policies, MoneyWayReplayLifecyclePolicies.GetAll());
        Assert.Same(registered, catalog.Find(new("moneyway-nasdaq"), new("nasdaq-0.1.0-draft")));
        Assert.Null(catalog.Find(Nasdaq.StrategyId, new("other-version")));
        var forex = MoneyWayForexStrategyDefinition.Instance;
        Assert.Null(catalog.Find(forex.StrategyId, forex.Version));
    }

    [Fact]
    public void CanonicalRunnerCarriesPreActivationPrerequisitesAndExpiresAtTheRealCutoff()
    {
        var evaluators = new IReplayRuleEvaluator[]
        {
            new ControlledEvaluator("NQ-H4-001", context => context.Step == 1),
            new ControlledEvaluator("NQ-LIQ-002", context => context.Step == 1),
            new ControlledEvaluator("NQ-TIME-001", _ => true),
            new ControlledEvaluator("NQ-LIQ-003", context => context.Step >= 2),
            new MoneyWayNasdaqTradingWindowEndEvaluator(),
        };
        var progressionUseCase = new AdvanceStrategyReplayProgressionUseCase();
        var runner = new GenerateMultiTimeframeStrategyBacktestRunUseCase(
            new(),
            new(),
            new(evaluators),
            new(new StrategyDefinitionCatalog().GetAll(), MoneyWayReplayWorkflowDefinitions.GetAll()),
            progressionUseCase,
            new(new StrategyDefinitionCatalog().GetAll(), MoneyWayReplayLifecyclePolicies.GetAll()),
            new(progressionUseCase));

        var run = runner.Execute(Nasdaq, [Series(Symbol, AtLocal(10, 58), AtLocal(10, 59, 59), AtLocal(11))]);

        Assert.Null(run.StrategyObservations[0].LifecycleProgression!.ActiveInstance);
        var active = Assert.IsType<StrategyReplayProgressionInstanceSnapshot>(run.StrategyObservations[1].LifecycleProgression!.ActiveInstance);
        Assert.Equal(new RuleId("NQ-LIQ-003"), active.WorkflowProgression.EstablishedRuleIds.Last());
        var expired = Assert.Single(run.StrategyObservations[2].LifecycleProgression!.Instances);
        Assert.Equal(StrategyReplayProgressionTerminationKind.Expired, expired.TerminationKind);
        Assert.Equal(AtLocal(11), expired.TerminatedAtUtc);
    }

    [Fact]
    public void IncompleteSetupRemainsActiveBeforeCutoffThenExpiresExactlyOnceAndCannotRevive()
    {
        var state = StartIncompleteSetup(Symbol);
        var beforeSignature = Signature(state.Lifecycle!);

        state = Advance(state, AtLocal(11), Evaluation("NQ-M1-003", RuleEvaluationResult.Passed), Cutoff(RuleEvaluationResult.Failed));
        var atCutoff = state;
        state = Advance(state, AtLocal(11, 0, 1), Evaluation("NQ-M1-003", RuleEvaluationResult.Passed), Cutoff(RuleEvaluationResult.Failed));
        state = Advance(state, AtLocal(11, 30), Evaluation("NQ-M1-003", RuleEvaluationResult.Passed), Cutoff(RuleEvaluationResult.Failed));

        Assert.NotNull(state.Progression);
        Assert.Null(atCutoff.Lifecycle!.ActiveInstance);
        Assert.Null(state.Lifecycle!.ActiveInstance);
        var terminal = Assert.Single(state.Lifecycle.Instances);
        Assert.Equal(StrategyReplayProgressionTerminationKind.Expired, terminal.TerminationKind);
        Assert.Equal(AtLocal(11), terminal.TerminatedAtUtc);
        Assert.DoesNotContain(new RuleId("NQ-M1-003"), terminal.WorkflowProgression.EstablishedRuleIds);
        Assert.Equal(
            [StrategyReplayLifecycleTransitionKind.Start, StrategyReplayLifecycleTransitionKind.Expire],
            state.Lifecycle.TransitionHistory.Select(item => item.Kind));
        Assert.Equal(RuleEvaluationResult.Passed, state.Observation!.Evaluations.Single(item => item.RuleId == new RuleId("NQ-M1-003")).Result);
        Assert.Equal(beforeSignature, Signature(StartIncompleteSetup(Symbol).Lifecycle!));
    }

    [Fact]
    public void CompletedSequenceBeforeCutoffIsNotExpiredOrRetroactivelyInvalidated()
    {
        var state = CompleteSetupBeforeCutoff(Symbol);
        var completed = state.Lifecycle!.ActiveInstance!.WorkflowProgression;

        state = Advance(state, AtLocal(11), Cutoff(RuleEvaluationResult.Failed));

        var active = Assert.IsType<StrategyReplayProgressionInstanceSnapshot>(state.Lifecycle!.ActiveInstance);
        Assert.Contains(new RuleId("NQ-M1-003"), active.WorkflowProgression.EstablishedRuleIds);
        Assert.Null(active.TerminationKind);
        Assert.Single(state.Lifecycle.TransitionHistory);
        Assert.Contains(new RuleId("NQ-M1-003"), completed.EstablishedRuleIds);
    }

    [Fact]
    public void CutoffWithoutAnActiveSetupIsANoOpAndCreatesNoHistory()
    {
        var state = Advance(default, AtLocal(11), Cutoff(RuleEvaluationResult.Failed));

        Assert.Null(state.Lifecycle!.ActiveInstance);
        Assert.Empty(state.Lifecycle.Instances);
        Assert.Empty(state.Lifecycle.TransitionHistory);
    }

    [Theory]
    [InlineData(11, 0, 0)]
    [InlineData(11, 0, 1)]
    public void EligibleStep3CannotStartANewSetupAtOrAfterCutoff(int hour, int minute, int second)
    {
        var state = Advance(default, AtLocal(10, 59, 58),
            Evaluation("NQ-H4-001", RuleEvaluationResult.Passed),
            Evaluation("NQ-LIQ-002", RuleEvaluationResult.Passed),
            Evaluation("NQ-TIME-001", RuleEvaluationResult.Passed),
            Cutoff(RuleEvaluationResult.Passed));

        state = Advance(state, AtLocal(hour, minute, second),
            Evaluation("NQ-LIQ-003", RuleEvaluationResult.Passed),
            Cutoff(RuleEvaluationResult.Failed));

        Assert.Null(state.Lifecycle!.ActiveInstance);
        Assert.Empty(state.Lifecycle.Instances);
        Assert.Empty(state.Lifecycle.TransitionHistory);
        Assert.Contains(new RuleId("NQ-LIQ-003"), state.Progression!.EstablishedRuleIds);
    }

    [Fact]
    public void FutureCutoffDoesNotChangeTheEquivalentPrefixOrEarlierImmutableSnapshot()
    {
        var shortRun = StartIncompleteSetup(Symbol);
        var longRunAtPrefix = StartIncompleteSetup(Symbol);
        var prefixSignature = Signature(longRunAtPrefix.Lifecycle!);

        _ = Advance(longRunAtPrefix, AtLocal(11), Cutoff(RuleEvaluationResult.Failed));

        Assert.Equal(Signature(shortRun.Lifecycle!), prefixSignature);
        Assert.Equal(prefixSignature, Signature(longRunAtPrefix.Lifecycle!));
        Assert.NotNull(longRunAtPrefix.Lifecycle!.ActiveInstance);
    }

    [Fact]
    public void LifecycleStateIsIsolatedBySymbolAndProvider()
    {
        var intended = StartIncompleteSetup(new("NQ-A"), Provider);
        var other = Advance(default, AtLocal(11), new MarketSymbol("NQ-B"), new MarketDataProviderId("other-provider"),
            Evaluation("NQ-H4-001", RuleEvaluationResult.Passed),
            Evaluation("NQ-LIQ-002", RuleEvaluationResult.Passed),
            Evaluation("NQ-TIME-001", RuleEvaluationResult.Passed),
            Cutoff(RuleEvaluationResult.Failed));

        intended = Advance(intended, AtLocal(11), Cutoff(RuleEvaluationResult.Failed));

        Assert.Equal(StrategyReplayProgressionTerminationKind.Expired, Assert.Single(intended.Lifecycle!.Instances).TerminationKind);
        Assert.Empty(other.Lifecycle!.Instances);
        Assert.Equal(new MarketSymbol("NQ-A"), intended.Lifecycle.Symbol);
        Assert.Equal(new MarketSymbol("NQ-B"), other.Lifecycle.Symbol);
        Assert.Equal(Provider, intended.Lifecycle.ProviderId);
        Assert.Equal(new MarketDataProviderId("other-provider"), other.Lifecycle.ProviderId);
    }

    [Fact]
    public void TimingRuleRemainsRawAndAbsentFromThePrerequisiteGraph()
    {
        var everyPrerequisite = Workflow.RulePrerequisites.SelectMany(item => item.PrerequisiteRuleIds);
        var evaluator = new MoneyWayNasdaqTradingWindowEndEvaluator();

        Assert.DoesNotContain(new RuleId("NQ-TIME-002"), everyPrerequisite);
        Assert.Equal(RuleEvaluationResult.Passed, evaluator.Evaluate(Context(AtLocal(10, 59, 59))).Result);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(Context(AtLocal(11))).Result);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(Context(AtLocal(11, 0, 1))).Result);
    }

    private LifecycleState StartIncompleteSetup(MarketSymbol symbol, MarketDataProviderId? provider = null)
    {
        var state = Advance(default, AtLocal(10, 58), symbol, provider ?? Provider,
            Evaluation("NQ-H4-001", RuleEvaluationResult.Passed),
            Evaluation("NQ-LIQ-002", RuleEvaluationResult.Passed),
            Evaluation("NQ-TIME-001", RuleEvaluationResult.Passed),
            Cutoff(RuleEvaluationResult.Passed));
        state = Advance(state, AtLocal(10, 59, 59),
            Evaluation("NQ-LIQ-003", RuleEvaluationResult.Passed),
            Cutoff(RuleEvaluationResult.Passed));
        Assert.NotNull(state.Lifecycle!.ActiveInstance);
        return state;
    }

    private LifecycleState CompleteSetupBeforeCutoff(MarketSymbol symbol)
    {
        var state = Advance(default, AtLocal(10, 50), symbol, Provider,
            Evaluation("NQ-H4-001", RuleEvaluationResult.Passed),
            Evaluation("NQ-LIQ-002", RuleEvaluationResult.Passed),
            Evaluation("NQ-TIME-001", RuleEvaluationResult.Passed),
            Cutoff(RuleEvaluationResult.Passed));
        state = Advance(state, AtLocal(10, 51),
            Evaluation("NQ-LIQ-003", RuleEvaluationResult.Passed),
            Cutoff(RuleEvaluationResult.Passed));
        var rules = new[] { "NQ-M5-001", "NQ-FVG-001", "NQ-M1-001", "NQ-M1-002", "NQ-M1-003" };
        for (var index = 0; index < rules.Length; index++)
            state = Advance(state, AtLocal(10, 52 + index), Evaluation(rules[index], RuleEvaluationResult.Passed), Cutoff(RuleEvaluationResult.Passed));
        return state;
    }

    private LifecycleState Advance(LifecycleState previous, DateTimeOffset at, params RuleEvaluation[] evaluations) =>
        Advance(previous, at, previous.Lifecycle?.Symbol ?? Symbol, previous.Lifecycle?.ProviderId ?? Provider, evaluations);

    private LifecycleState Advance(
        LifecycleState previous,
        DateTimeOffset at,
        MarketSymbol symbol,
        MarketDataProviderId provider,
        params RuleEvaluation[] evaluations)
    {
        var step = (previous.Lifecycle?.Step ?? 0) + 1;
        var observation = new StrategyReplayContextObservation(
            Nasdaq.StrategyId,
            Nasdaq.Version,
            provider,
            symbol,
            step,
            at,
            evaluations
                .OrderBy(item => item.Sequence)
                .Select(item => new RuleEvaluation(
                    item.RuleId,
                    item.DefinitionStatus,
                    item.Result,
                    item.Sequence,
                    item.IsRequired,
                    item.Reason,
                    at,
                    item.EvidenceReference)));
        var advanced = useCase.Execute(Workflow, Policy, observation, previous.Lifecycle, previous.Progression);
        return new(advanced.WorkflowProgression, advanced.LifecycleProgression, observation);
    }

    private static RuleEvaluation Cutoff(RuleEvaluationResult result) => Evaluation("NQ-TIME-002", result);

    private static RuleEvaluation Evaluation(string ruleId, RuleEvaluationResult result)
    {
        var rule = Nasdaq.Rules.Single(item => item.RuleId == new RuleId(ruleId));
        return new(rule.RuleId, rule.DefinitionStatus, result, rule.Sequence, rule.IsRequired, "Controlled lifecycle test.", DateTimeOffset.UnixEpoch, null);
    }

    private static StrategyReplayContext Context(DateTimeOffset at)
    {
        StrategyReplayContext? result = null;
        new RunMultiTimeframeReplayUseCase().Execute([Series(Symbol, at)], frame => result = new CreateStrategyReplayContextUseCase().Execute(Nasdaq, frame));
        return result!;
    }

    private static CandleSeries Series(MarketSymbol symbol, params DateTimeOffset[] closes)
    {
        var previousClose = closes[0].AddMinutes(-1);
        var candles = new List<Candle>();
        foreach (var close in closes)
        {
            candles.Add(new(Provider, symbol, Minute, previousClose, close, 100, 101, 99, 100, null));
            previousClose = close;
        }
        return new(Provider, symbol, Minute, candles);
    }

    private static DateTimeOffset AtLocal(int hour, int minute = 0, int second = 0)
    {
        var local = new DateTime(2026, 1, 15, hour, minute, second, DateTimeKind.Unspecified);
        return new(TimeZoneInfo.ConvertTimeToUtc(local, Bogota), TimeSpan.Zero);
    }

    private static string Signature(StrategyReplayLifecycleSnapshot snapshot) => string.Join('|',
        snapshot.StrategyId,
        snapshot.StrategyVersion,
        snapshot.ProviderId,
        snapshot.Symbol,
        snapshot.Step,
        snapshot.AsOfUtc,
        string.Join(';', snapshot.Instances.Select(item => $"{item.InstanceId}:{item.StartedAtUtc:o}:{item.TerminationKind}:{item.TerminatedAtUtc:o}:{string.Join(',', item.WorkflowProgression.EstablishedRuleIds)}")),
        string.Join(';', snapshot.TransitionHistory.Select(item => $"{item.InstanceId}:{item.AsOfUtc:o}:{item.Kind}")));

    private static TimeZoneInfo ResolveBogotaTimeZone()
    {
        const string timeZoneId = "America/Bogota";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }

    private readonly record struct LifecycleState(
        StrategyReplayProgressionSnapshot? Progression,
        StrategyReplayLifecycleSnapshot? Lifecycle,
        StrategyReplayContextObservation? Observation);

    private sealed class ControlledEvaluator(string ruleId, Func<StrategyReplayContext, bool> passes) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Nasdaq.StrategyId;
        public StrategyVersion StrategyVersion => Nasdaq.Version;
        public RuleId RuleId { get; } = new(ruleId);

        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(
            passes(context) ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed,
            "Controlled lifecycle test.",
            null);
    }
}
