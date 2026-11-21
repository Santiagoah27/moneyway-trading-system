using System.Text.Json;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class NasdaqTradingWindowAlignmentTests
{
    private static readonly DateTimeOffset Day = new(2026, 6, 23, 0, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    [Theory]
    [InlineData(12, 59, NasdaqPreparationClockPhase.BeforePreparation, NasdaqWindowClockPhase.BeforeTradingWindow, false, false)]
    [InlineData(13, 0, NasdaqPreparationClockPhase.PreparationPhase, NasdaqWindowClockPhase.BeforeTradingWindow, false, false)]
    [InlineData(13, 29, NasdaqPreparationClockPhase.PreparationPhase, NasdaqWindowClockPhase.BeforeTradingWindow, false, false)]
    [InlineData(13, 30, NasdaqPreparationClockPhase.PreparationIntervalElapsed, NasdaqWindowClockPhase.InsideTradingWindow, true, true)]
    [InlineData(15, 59, NasdaqPreparationClockPhase.PreparationIntervalElapsed, NasdaqWindowClockPhase.InsideTradingWindow, true, true)]
    [InlineData(16, 0, NasdaqPreparationClockPhase.PreparationIntervalElapsed, NasdaqWindowClockPhase.InsideTradingWindow, false, true)]
    [InlineData(16, 15, NasdaqPreparationClockPhase.PreparationIntervalElapsed, NasdaqWindowClockPhase.InsideTradingWindow, false, true)]
    [InlineData(16, 30, NasdaqPreparationClockPhase.PreparationIntervalElapsed, NasdaqWindowClockPhase.AtTradingWindowEnd, false, false)]
    [InlineData(16, 31, NasdaqPreparationClockPhase.PreparationIntervalElapsed, NasdaqWindowClockPhase.AfterTradingWindow, false, false)]
    public void ClassifiesExactHalfOpenBoundaries(int hour, int minute, NasdaqPreparationClockPhase preparation,
        NasdaqWindowClockPhase phase, bool legacy, bool candidate)
    {
        var at = At(hour, minute);
        var result = NasdaqTradingWindowAlignment.Evaluate(at, at);
        Assert.Equal(NasdaqWindowEvidenceStatus.Observable, result.EvidenceStatus);
        Assert.Equal(preparation, result.PreparationPhase);
        Assert.Equal(phase, result.Candidate!.Phase);
        Assert.Equal(legacy, result.PublishedRuntime!.WithinNewSetupAndEntryTimeWindow);
        Assert.Equal(candidate, result.Candidate.WithinNewSetupAndEntryTimeWindow);
        Assert.Equal(At(13), result.PreparationUtc);
        Assert.Equal(At(13, 30), result.Candidate.StartUtc);
        Assert.Equal(At(16), result.PublishedRuntime.EndUtc);
        Assert.Equal(At(16, 30), result.Candidate.EndUtc);
        Assert.False(result.CanonicalAdmissionPermitted);
        Assert.False(result.OperationalSignal);
        Assert.False(result.TerminatesSession);
        Assert.False(result.ForcesPositionClose);
        Assert.False(result.DefinesPositionManagement);
    }

    [Fact]
    public void AdjacentTicksAndObservableEventBoundaryDoNotUseLaterCutoffAsEventTime()
    {
        var end = At(16, 30);
        var at = end.AddTicks(-1);
        var result = NasdaqTradingWindowAlignment.Evaluate(end.AddDays(1), at);
        Assert.True(result.Candidate!.WithinNewSetupAndEntryTimeWindow);
        Assert.Equal(at, result.EventUtc);
        Assert.Equal(NasdaqWindowClockPhase.AfterTradingWindow,
            NasdaqTradingWindowAlignment.Evaluate(end.AddTicks(1), end.AddTicks(1)).Candidate!.Phase);
    }

    [Theory]
    [InlineData(3, 7)]
    [InlineData(3, 8)]
    [InlineData(10, 31)]
    [InlineData(11, 1)]
    public void BogotaConversionStaysUtcMinusFiveAroundUsDst(int month, int day)
    {
        var utc = new DateTimeOffset(2026, month, day, 16, 30, 0, TimeSpan.Zero);
        var result = NasdaqTradingWindowAlignment.Evaluate(utc, utc);
        Assert.Equal(TimeSpan.FromHours(-5), result.EventBogota!.Value.Offset);
        Assert.Equal(new TimeOnly(11, 30), TimeOnly.FromDateTime(result.EventBogota.Value.DateTime));
        Assert.Equal(utc.AddHours(-3), result.Candidate!.StartUtc);
        Assert.Equal(utc, result.Candidate.EndUtc);
    }

    [Fact]
    public void MissingInvalidAndFutureEvidenceExposeNoEligibilityOrFutureTimestamp()
    {
        var cutoff = At(16, 15);
        var missing = NasdaqTradingWindowAlignment.Evaluate(cutoff, null);
        Assert.Equal(NasdaqWindowEvidenceStatus.MissingTimestamp, missing.EvidenceStatus);
        Assert.Null(missing.Candidate);
        var invalid = NasdaqTradingWindowAlignment.Evaluate(cutoff, cutoff.ToOffset(TimeSpan.FromHours(-5)));
        Assert.Equal(NasdaqWindowEvidenceStatus.InvalidTimestamp, invalid.EvidenceStatus);
        Assert.Null(invalid.EventUtc);
        var future = NasdaqTradingWindowAlignment.Evaluate(cutoff, cutoff.AddTicks(1));
        Assert.Equal(NasdaqWindowEvidenceStatus.EventNotYetObservable, future.EvidenceStatus);
        Assert.Null(future.EventUtc);
        Assert.Null(future.Candidate);
        Assert.Equal(JsonSerializer.Serialize(future),
            JsonSerializer.Serialize(NasdaqTradingWindowAlignment.Evaluate(cutoff, cutoff.AddDays(1))));
    }

    [Fact]
    public void CandidateAndLegacyAsOfDecisionsSurviveFutureCandleAppendAndMutation()
    {
        var at = At(16, 15);
        var provider = new MarketDataProviderId("fixture");
        var symbol = new MarketSymbol("US100");
        var m1 = new Timeframe(1, TimeframeUnit.Minute);
        Candle Candle(DateTimeOffset close, decimal price) =>
            new(provider, symbol, m1, close.AddMinutes(-1), close, price, price + 1, price - 1, price, null);
        var current = Candle(at, 100);
        var baseline = MoneyWayNasdaqStrategyDefinition.Instance;
        var sc = MoneyWayNasdaqM5StrictCloseCandidate.Definition;
        var sessions = MoneyWayNasdaqSessionCandidate.Definition;
        IReplayRuleEvaluator End(IReadOnlyList<IReplayRuleEvaluator> items) => items.Single(i => i.RuleId.Value == "NQ-TIME-002");

        foreach (var (definition, evaluator) in new (StrategyDefinition, IReplayRuleEvaluator)[]
                 {
                     (baseline, new MoneyWayNasdaqTradingWindowEndEvaluator()),
                     (sc, End(MoneyWayNasdaqM5StrictCloseCandidate.Evaluators())),
                     (sessions, End(MoneyWayNasdaqSessionCandidate.Evaluators()))
                 })
        {
            string Replay(Candle[] candles)
            {
                string? snapshot = null;
                new RunMultiTimeframeReplayUseCase().Execute([new CandleSeries(provider, symbol, m1, candles)], frame =>
                {
                    if (frame.AsOfUtc != at) return;
                    var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame);
                    var legacy = evaluator.Evaluate(context);
                    Assert.Equal(RuleEvaluationResult.Failed, legacy.Result);
                    var comparison = NasdaqTradingWindowAlignment.Evaluate(context.AsOfUtc, current.CloseTimeUtc);
                    Assert.True(comparison.Candidate!.WithinNewSetupAndEntryTimeWindow);
                    Assert.False(comparison.PublishedRuntime!.WithinNewSetupAndEntryTimeWindow);
                    snapshot = JsonSerializer.Serialize(comparison);
                });
                return Assert.IsType<string>(snapshot);
            }
            var expected = Replay([current]);
            Assert.Equal(expected, Replay([current, Candle(at.AddMinutes(1), 105)]));
            Assert.Equal(expected, Replay([current, Candle(at.AddMinutes(1), 999)]));
        }
        Assert.Null(new StrategyDefinitionCatalog().Find(baseline.StrategyId,
            new StrategyVersion(NasdaqTradingWindowAlignment.ConfigurationId)));
    }
}
