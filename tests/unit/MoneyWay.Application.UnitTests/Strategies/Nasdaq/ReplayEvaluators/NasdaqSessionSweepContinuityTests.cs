using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;
using static MoneyWay.Application.Strategies.Nasdaq.ReplayInputs.NasdaqLiquidityTakeReference.SessionEndpoint;
using static MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs.M5Fixture;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class NasdaqSessionSweepContinuityTests
{
    private const string LiquidityRule = "NQ-LIQ-003";
    private const string TriggerRule = "NQ-M5-001";

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void StrictSessionSweepsKeepOneSetupAndWaitForASeparateTrigger(bool sell, bool londonFirst)
    {
        var series = SweepSeries(londonFirst);
        var (first, second) = Sweeps(series, sell, londonFirst);
        var initial = Inputs(Direction(sell), first);
        var extended = initial.Concat([second, Link(first, second)]).ToArray();
        var run = Replay(series, extended);
        var start = AtFrame(run, 5);
        var extension = AtFrame(run, 20);
        var active = extension.LifecycleProgression!.ActiveInstance!;
        Assert.Equal(start.LifecycleProgression!.ActiveInstance!.InstanceId, active.InstanceId);
        Assert.Single(extension.LifecycleProgression.Instances);
        Assert.Same(first, Fact(start).Take);
        Assert.Same(first, Fact(extension).InitiatingTake);
        Assert.Same(second, Fact(extension).Take);
        Assert.Same(second, Fact(extension).RecordedTake);
        Assert.DoesNotContain(active.WorkflowProgression.EstablishedRuleIds, rule => rule.Value is TriggerRule or "NQ-M1-003");
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(AtFrame(run, 25), TriggerRule));
        AssertPrefix(run, Replay(series, initial), second.ObservedAtUtc);
        var mutated = MakeTake(series, ((NasdaqLiquidityTakeReference.SessionLevel)second.Reference).Endpoint, 20,
            second.ObservedPrice + (sell ? 1 : -1), "mutated");
        AssertPrefix(run, Replay(series, initial.Concat([mutated, Link(first, mutated)]).ToArray()), second.ObservedAtUtc);
        AssertPrefix(run, Replay(series, extended.Reverse().ToArray()), At(16).AddTicks(1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanonicalStepFourSurvivesALaterSameSideSweep(bool sell)
    {
        var series = SweepSeries(londonNearer: true, sell: sell);
        var (first, second) = Sweeps(series, sell, londonFirst: true);
        var prior = Five(At(14, 10));
        var candle = Five(At(14, 15), sell ? 99 : 101);
        var trigger = new NasdaqHumanM5TriggerObservation(first,
            new NasdaqHumanM5TriggerEvent.StructuralChange(candle, Direction(sell), [prior], [prior], 100),
            candle.CloseTimeUtc, "review:SC", NasdaqHumanM5TakeTriggerOrder.Unspecified);
        var run = Replay(series, Inputs(Direction(sell), first).Concat([trigger, second, Link(first, second)]).ToArray());
        var before = AtFrame(run, 15);
        var after = AtFrame(run, 25);
        Assert.Equal(RuleEvaluationResult.Passed, Result(before, TriggerRule));
        Assert.Equal(RuleEvaluationResult.Passed, Result(after, TriggerRule));
        Assert.Same(first, Fact(after).Take);
        Assert.Same(second, Fact(after).RecordedTake);
        Assert.Same(trigger, Assert.IsType<NasdaqHumanM5TriggerRuleFact>(
            Assert.Single(after.RuleFacts, f => f.RuleId.Value == TriggerRule).Fact).Selection.Fact);
        Assert.Equal(before.LifecycleProgression!.ActiveInstance!.InstanceId, after.LifecycleProgression!.ActiveInstance!.InstanceId);
    }

    [Fact]
    public void ASeparateSweepOfAnEqualEndpointRetainsItsHumanDocumentedLineage()
    {
        var series = SweepSeries(londonNearer: true, equal: true);
        var (first, second) = Sweeps(series, sell: true, londonFirst: true);
        var run = Replay(series, Inputs(Direction(sell: true), first).Concat([second, Link(first, second)]).ToArray());
        var frame = AtFrame(run, 20);
        Assert.Equal(RuleEvaluationResult.Passed, Result(frame, LiquidityRule));
        Assert.Same(first, Fact(frame).InitiatingTake);
        Assert.Same(second, Fact(frame).Take);
        Assert.Equal(first.ReferencePrice, second.ReferencePrice);
        Assert.Same(second, Fact(frame).RecordedTake);
        Assert.Single(frame.LifecycleProgression!.Instances);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OppositeSweepAfterAnAcceptedExtensionEndsTheSameSession(bool sell)
    {
        var series = SweepSeries(londonNearer: true);
        var (first, second) = Sweeps(series, sell, londonFirst: true);
        var opposite = MakeTake(series, sell ? LondonLow : LondonHigh, 25, sell ? 89 : 111, "opposite");
        var later = MakeTake(series, sell ? AsiaHigh : AsiaLow, 30, sell ? 128 : 72, "after terminal");
        var run = Replay(series, Inputs(Direction(sell), first)
            .Concat([second, Link(first, second), opposite, Link(first, opposite), later, Link(first, later)]).ToArray());
        Assert.Same(second, Fact(AtFrame(run, 20)).Take);
        foreach (var at in new[] { 25, 30 })
        {
            var frame = AtFrame(run, at);
            Assert.Equal(RuleEvaluationResult.Failed, Result(frame, LiquidityRule));
            Assert.True(Fact(frame).IsSessionInvalidated);
            Assert.Same(opposite, Fact(frame).Take);
            Assert.Same(first, Fact(frame).InitiatingTake);
            Assert.Null(frame.LifecycleProgression!.ActiveInstance);
            Assert.Single(frame.LifecycleProgression.Instances);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void OneClosedCandleCannotPickAnInitiatorAcrossSessionEndpoints(bool sell, bool equal)
    {
        var candle = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5TriggerEvent.M5,
            At(14), At(14, 5), 100, sell ? 125 : 105, sell ? 95 : 75, 100, null);
        var series = SweepSeries(londonNearer: true, equal: equal).Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe,
            s.Candles.Select(c => c.Timeframe == candle.Timeframe && c.CloseTimeUtc == candle.CloseTimeUtc ? candle : c))).ToArray();
        var asia = MakeTake(series, sell ? AsiaHigh : AsiaLow, 5, sell ? candle.High : candle.Low, "same candle", candle: candle);
        var london = MakeTake(series, sell ? LondonHigh : LondonLow, 5, sell ? candle.High : candle.Low, "same candle", candle: candle);
        Assert.Equal(equal, asia.ReferencePrice == london.ReferencePrice);
        var input = Inputs(Direction(sell), asia).Concat([london, Relevant(london)]).ToArray();
        var contested = AtFrame(Replay(series, input), 5);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(contested, LiquidityRule));
        Assert.Empty(contested.LifecycleProgression!.Instances);
        Assert.DoesNotContain(contested.RuleFacts, f => f.RuleId.Value == LiquidityRule);
        var proof = contested.Evaluations.Single(e => e.RuleId.Value == LiquidityRule).EvidenceReference;
        Assert.Contains($"review:{(sell ? AsiaHigh : AsiaLow)}", proof);
        Assert.Contains($"review:{(sell ? LondonHigh : LondonLow)}", proof);
        Assert.Equal(proof, AtFrame(Replay(series, input.Reverse().ToArray()), 5)
            .Evaluations.Single(e => e.RuleId.Value == LiquidityRule).EvidenceReference);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingNamedSessionSourceCannotPass(bool london)
    {
        var series = SweepSeries(londonNearer: true);
        var take = MakeTake(series, london ? LondonHigh : AsiaHigh, 5, 125, "selected");
        var incomplete = series.Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe,
            s.Candles.Where(c => c.Timeframe != new Timeframe(1, TimeframeUnit.Hour) || c.OpenTimeUtc != At(london ? 7 : 0)))).ToArray();
        var frame = AtFrame(Replay(incomplete, Inputs(Direction(sell: true), take)), 5);
        Assert.Equal(RuleEvaluationResult.DataUnavailable, Result(frame, LiquidityRule));
        Assert.Empty(frame.LifecycleProgression!.Instances);
    }

    [Fact]
    public void LatePublicationCannotAdvanceTheReferenceOrReviveAnExpiredSetup()
    {
        var series = SweepSeries(londonNearer: true);
        var (first, _) = Sweeps(series, sell: true, londonFirst: true);
        var late = MakeTake(series, AsiaHigh, 20, 125, "published at cutoff", observed: At(16));
        var initial = Inputs(Direction(sell: true), first);
        var run = Replay(series, initial.Concat([late, Link(first, late)]).ToArray());
        AssertPrefix(run, Replay(series, initial), At(16));
        Assert.Same(first, Fact(AtFrame(run, 25)).Take);
        var cutoff = run.StrategyObservations.Single(o => o.AsOfUtc == At(16));
        Assert.Equal(RuleEvaluationResult.Failed, Result(cutoff, LiquidityRule));
        Assert.Null(cutoff.LifecycleProgression!.ActiveInstance);
        Assert.Single(cutoff.LifecycleProgression.Instances);
        Assert.DoesNotContain(cutoff.RuleFacts, f => f.RuleId.Value == LiquidityRule);
    }

    private static NasdaqHumanH4PermittedDirection Direction(bool sell) =>
        sell ? NasdaqHumanH4PermittedDirection.Sell : NasdaqHumanH4PermittedDirection.Buy;

    private static (NasdaqHumanLiquidityTakeObservation First, NasdaqHumanLiquidityTakeObservation Second) Sweeps(
        CandleSeries[] series, bool sell, bool londonFirst)
    {
        var asia = sell ? AsiaHigh : AsiaLow;
        var london = sell ? LondonHigh : LondonLow;
        return (MakeTake(series, londonFirst ? london : asia, 5, sell ? 111 : 89, "first"),
            MakeTake(series, londonFirst ? asia : london, 20, sell ? 125 : 75, "extension"));
    }

    private static NasdaqHumanRelevantLiquidityTakeObservation Link(NasdaqHumanLiquidityTakeObservation first,
        NasdaqHumanLiquidityTakeObservation second) => new(second, second.ObservedAtUtc, "review:linked event", first);

    private static CandleSeries[] SweepSeries(bool londonNearer, bool sell = false, bool equal = false) => Series()
        .Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Select(c =>
            c.Timeframe == new Timeframe(1, TimeframeUnit.Hour) && !equal && c.OpenTimeUtc == At(londonNearer ? 0 : 7)
                ? LiquidityFixture.Candle(londonNearer ? 0 : 7, high: 120, low: 80)
                : c.Timeframe == NasdaqHumanM5TriggerEvent.M5 && c.CloseTimeUtc == At(14, 15)
                    ? Five(c.CloseTimeUtc, sell ? 99 : 101)
                    : c))).ToArray();

    private static NasdaqHumanLiquidityTakeObservation MakeTake(CandleSeries[] series,
        NasdaqLiquidityTakeReference.SessionEndpoint endpoint, int minute, decimal price, string source,
        DateTimeOffset? observed = null, Candle? candle = null)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc != At(13, 15)) continue;
            var context = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, []);
            var reference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
                endpoint, context.AsOfUtc, $"review:{endpoint}");
            var effective = At(14, minute);
            return new(reference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol,
                source, price, effective, "fixture:documented event", candle is null ? [] : [candle]),
                context.AsOfUtc, observed ?? effective, $"review:{source}");
        }
        throw new InvalidOperationException("Missing selection boundary.");
    }

    private static MultiTimeframeStrategyBacktestRun Replay(CandleSeries[] series, IStrategyReplayInputObservation[] inputs)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, series, WithStandardApplicability(inputs, series));
    }

    private static StrategyReplayContextObservation AtFrame(MultiTimeframeStrategyBacktestRun run, int minute) =>
        run.StrategyObservations.Single(o => o.AsOfUtc == At(14, minute));

    private static NasdaqLiquidityTakeRuleFact Fact(StrategyReplayContextObservation frame) =>
        Assert.IsType<NasdaqLiquidityTakeRuleFact>(Assert.Single(frame.RuleFacts, f => f.RuleId.Value == LiquidityRule).Fact);

    private static RuleEvaluationResult Result(StrategyReplayContextObservation frame, string rule) =>
        frame.Evaluations.Single(e => e.RuleId.Value == rule).Result;

    private static void AssertPrefix(MultiTimeframeStrategyBacktestRun a, MultiTimeframeStrategyBacktestRun b, DateTimeOffset before)
    {
        var left = a.StrategyObservations.Where(f => f.AsOfUtc < before);
        var right = b.StrategyObservations.Where(f => f.AsOfUtc < before);
        Assert.Equal(left.Select(Describe), right.Select(Describe));
    }

    private static string Describe(StrategyReplayContextObservation frame) => string.Join("|",
        frame.AsOfUtc, frame.LifecycleProgression?.ActiveInstance?.InstanceId,
        string.Join(",", frame.WorkflowProgression!.EstablishedRuleIds),
        string.Join("|", frame.Evaluations.Select(e => (e.RuleId, e.Result, e.EvidenceReference))));
}
