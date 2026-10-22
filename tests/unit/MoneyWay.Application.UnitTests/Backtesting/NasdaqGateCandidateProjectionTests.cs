using System.Collections;
using System.Reflection;
using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class NasdaqGateCandidateProjectionTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("aligned")]
    [InlineData("terminal")]
    [InlineData("compatible")]
    [InlineData("conflicting")]
    [InlineData("later-setup")]
    [InlineData("other-ancestry")]
    [InlineData("late-h4")]
    [InlineData("late-terminal")]
    [InlineData("two-sessions")]
    [InlineData("missing-source")]
    [InlineData("no-preparation")]
    [InlineData("unresolved-h4")]
    [InlineData("input-order")]
    public void EveryFrameMatchesOriginalFullHistoryCoordinator(string scenario)
    {
        var inputs = Inputs(scenario);
        var series = M5Fixture.Series();
        var run = Run(series, inputs);
        AssertOriginalScanEquivalent(run, series, inputs);
        var takeFrame = run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 5));
        if (scenario == "terminal")
        {
            Assert.IsType<NasdaqLiquidityTakeRuleFact>(takeFrame.RuleFacts.Single(f => f.RuleId.Value == "NQ-LIQ-003").Fact);
            var gate = takeFrame.Evaluations.Single(e => e.RuleId.Value == "NQ-M5-001");
            Assert.Equal(RuleEvaluationResult.Failed, gate.Result);
            Assert.Equal("A terminal session fact blocks this downstream rule.", gate.Reason);
        }
        if (scenario == "aligned")
            Assert.NotEqual(RuleEvaluationResult.Passed, takeFrame.Evaluations.Single(e => e.RuleId.Value == "NQ-M5-001").Result);
    }

    [Fact]
    public void RerunsAndLaterInvalidationCannotRewriteEarlierFramesOrLeakIndexState()
    {
        var series = M5Fixture.Series();
        var baseline = Run(series, Inputs("aligned"));
        Run(series, Inputs("terminal"));
        Assert.Equal(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(Run(series, Inputs("aligned"))));
        var future = Run(series, Inputs("late-terminal"));
        foreach (var earlier in baseline.StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, 20)))
            Assert.Equal(JsonSerializer.Serialize(earlier), JsonSerializer.Serialize(future.StrategyObservations.Single(o => o.AsOfUtc == earlier.AsOfUtc)));
    }

    [Fact]
    public void ReusedCoordinatorDoesNotCarryGatesIntoAnotherMarketOrExecution()
    {
        var coordinator = Coordinator();
        coordinator.Execute(LiquidityFixture.Definition, M5Fixture.Series(), Inputs("terminal"));
        var provider = new MarketDataProviderId("other-provider");
        var symbol = new MarketSymbol("OTHER");
        var series = M5Fixture.Series().Select(s => new CandleSeries(provider, symbol, s.Timeframe,
            s.Candles.Select(c => new Candle(provider, symbol, c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc,
                c.Open, c.High, c.Low, c.Close, c.Volume)))).ToArray();
        AssertOriginalScanEquivalent(coordinator.Execute(LiquidityFixture.Definition, series, Array.Empty<IStrategyReplayInputObservation>()), series, []);
        var expected = Run(M5Fixture.Series(), Inputs("aligned"));
        var rerun = coordinator.Execute(LiquidityFixture.Definition, M5Fixture.Series(), Inputs("aligned"));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(rerun));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("instrument")]
    [InlineData("version")]
    [InlineData("current-time")]
    public void ExistingContextRejectsForeignOrNonPriorHistory(string mismatch)
    {
        var context = M5Fixture.Context(M5Fixture.At(14, 15), []);
        var prior = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll())
            .Execute(LiquidityFixture.Definition, M5Fixture.Context(M5Fixture.At(13, 15), []));
        var foreign = new StrategyReplayContextObservation(prior.StrategyId,
            mismatch == "version" ? new("foreign-version") : prior.StrategyVersion,
            mismatch == "provider" ? new("foreign-provider") : prior.ProviderId,
            mismatch == "instrument" ? new("foreign-instrument") : prior.Symbol,
            prior.Step, mismatch == "current-time" ? context.AsOfUtc : prior.AsOfUtc,
            mismatch == "current-time" ? [] : prior.Evaluations);
        Assert.Throws<ArgumentException>(() => context.WithPriorObservations([foreign]));
    }

    [Fact]
    public void ProjectionRetainsExactReferencesRepetitionsAndFirstBlockingPrecedence()
    {
        var inputs = Inputs("terminal");
        var run = Run(M5Fixture.Series(), inputs);
        var time = M5Fixture.At(14, 20);
        var history = run.StrategyObservations.Where(o => o.AsOfUtc < time).ToArray();
        var facts = history.SelectMany(o => o.RuleFacts).Select(f => f.Fact).OfType<IReplayRuleGateFact>().ToArray();
        Assert.True(facts.Length > 1);
        var candidates = new CountingCandidates(facts);
        var context = M5Fixture.Context(time, inputs).WithPriorObservations(history);
        var evaluator = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll());
        var projected = Projected(evaluator, context, candidates);
        Assert.Equal(JsonSerializer.Serialize(evaluator.Execute(LiquidityFixture.Definition, context)), JsonSerializer.Serialize(projected));
        Assert.All(candidates.Visited, g => Assert.Contains(facts, f => ReferenceEquals(g, f)));
        Assert.True(candidates.Visits <= facts.Length * 18);
        Assert.Same(facts.First(), candidates.Visited.First());
        var blocked = projected.Evaluations.Single(e => e.RuleId.Value == "NQ-M5-001");
        Assert.Equal(facts.First().EvidenceReference, blocked.EvidenceReference);
        // Repeating a compatible original reference is not deduplicated or replaced.
        var repeated = new CountingCandidates([facts.First(), facts.First()]);
        Projected(evaluator, context, repeated);
        Assert.True(repeated.Visited.Count(g => ReferenceEquals(g, facts.First())) > 2);
    }

    [Fact]
    public void HundredsOfFramesWithNoGatesKeepCandidateEnumerationEmpty()
    {
        var candles = Enumerable.Range(0, 300).Select(i => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            M5Fixture.Minute, M5Fixture.At(13).AddMinutes(i), M5Fixture.At(13).AddMinutes(i + 1), 100, 110, 90, 100, null));
        CandleSeries[] series = [new(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5Fixture.Minute, candles)];
        var run = Run(series, []);
        AssertOriginalScanEquivalent(run, series, []);
        Assert.DoesNotContain(run.StrategyObservations.SelectMany(o => o.RuleFacts), f => f.Fact is IReplayRuleGateFact);
        var context = LastContext(series).WithPriorObservations(run.StrategyObservations.SkipLast(1));
        var candidates = new CountingCandidates([]);
        Projected(new(MoneyWayReplayRuleEvaluators.GetAll()), context, candidates);
        Assert.Equal(0, candidates.Visits);
    }

    private static void AssertOriginalScanEquivalent(MultiTimeframeStrategyBacktestRun run, CandleSeries[] series, IStrategyReplayInputObservation[] inputs)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        var original = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll());
        var history = new List<StrategyReplayContextObservation>();
        while (cursor.TryAdvance(out var frame))
        {
            var context = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame!, inputs).WithPriorObservations(history);
            var expected = original.Execute(LiquidityFixture.Definition, context);
            var actual = run.StrategyObservations[history.Count];
            Assert.Equal(JsonSerializer.Serialize(expected.Evaluations), JsonSerializer.Serialize(actual.Evaluations));
            Assert.Equal(JsonSerializer.Serialize(expected.RuleFacts.Select(f => new { f.RuleId, Fact = (object)f.Fact })),
                JsonSerializer.Serialize(actual.RuleFacts.Select(f => new { f.RuleId, Fact = (object)f.Fact })));
            history.Add(actual);
        }
    }

    private static StrategyReplayContext LastContext(CandleSeries[] series)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        StrategyReplayContext? context = null;
        while (cursor.TryAdvance(out var frame)) context = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame!);
        return context!;
    }

    private static StrategyReplayContextObservation Projected(EvaluateStrategyReplayContextUseCase evaluator, StrategyReplayContext context, CountingCandidates candidates) =>
        (StrategyReplayContextObservation)typeof(EvaluateStrategyReplayContextUseCase)
            .GetMethod("ExecuteWithPriorGateCandidates", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(evaluator, [LiquidityFixture.Definition, context, candidates, null])!;

    private static MultiTimeframeStrategyBacktestRun Run(CandleSeries[] series, IStrategyReplayInputObservation[] inputs)
        => Coordinator().Execute(LiquidityFixture.Definition, series, inputs);

    private static GenerateMultiTimeframeStrategyBacktestRunUseCase Coordinator()
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()));
    }

    private static IStrategyReplayInputObservation[] Inputs(string scenario)
    {
        var take = M5Fixture.Take(scenario == "terminal");
        var inputs = M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Buy, take).ToList();
        if (scenario == "empty") return [];
        if (scenario == "compatible") inputs.Add(new NasdaqHumanLiquidityTakeObservation(take.Reference, take.Event, take.ReferenceEligibleAtUtc, take.ObservedAtUtc, "compatible review"));
        if (scenario is "conflicting" or "other-ancestry" or "later-setup" or "late-terminal")
        {
            var other = M5Fixture.Take(scenario != "other-ancestry", effective: scenario is "later-setup" or "late-terminal" ? M5Fixture.At(14, 20) : null,
                observed: scenario is "later-setup" or "late-terminal" ? M5Fixture.At(14, 20) : null, eventId: "other-ancestry");
            inputs.Add(other); inputs.Add(M5Fixture.Relevant(other));
        }
        if (scenario is "late-h4" or "unresolved-h4")
        {
            inputs.RemoveAll(o => o is NasdaqHumanH4ContextObservation);
            inputs.Add(M5Fixture.H4(scenario == "unresolved-h4" ? NasdaqHumanH4PermittedDirection.Unresolved : NasdaqHumanH4PermittedDirection.Buy,
                observed: scenario == "late-h4" ? M5Fixture.At(14, 20) : null));
        }
        if (scenario == "no-preparation") inputs.RemoveAll(o => o is NasdaqPreparationCompletionObservation);
        if (scenario == "two-sessions") inputs.AddRange(M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Sell, M5Fixture.Take(true, shift: 24), shift: 24));
        if (scenario == "missing-source")
            inputs.Add(new NasdaqHumanLiquidityTakeObservation(take.Reference,
                new NasdaqHumanLiquidityTakeEvent.CanonicalPrice(new(LiquidityFixture.Provider, LiquidityFixture.Symbol,
                    M5Fixture.At(14, 5), 89, "quote", "missing named source", 1)), M5Fixture.At(13, 15), M5Fixture.At(14, 5), "unavailable review"));
        if (scenario == "input-order") inputs.Reverse();
        return inputs.ToArray();
    }

    private sealed class CountingCandidates(IReadOnlyList<IReplayRuleGateFact> facts) : IReadOnlyList<IReplayRuleGateFact>
    {
        public int Visits { get; private set; }
        public List<IReplayRuleGateFact> Visited { get; } = [];
        public int Count => facts.Count;
        public IReplayRuleGateFact this[int index] => facts[index];
        public IEnumerator<IReplayRuleGateFact> GetEnumerator()
        {
            foreach (var fact in facts) { Visits++; Visited.Add(fact); yield return fact; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
