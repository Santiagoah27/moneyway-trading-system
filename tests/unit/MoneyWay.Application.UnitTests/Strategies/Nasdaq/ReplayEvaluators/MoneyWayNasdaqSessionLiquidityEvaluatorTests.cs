using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqSessionLiquidityEvaluatorTests
{
    private static readonly DateOnly TradingDay = new(2026, 1, 15);
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly TimeZoneInfo Bogota = ResolveBogota();
    private readonly NasdaqSessionLiquidityCalculator calculator = new();
    private readonly MoneyWayNasdaqSessionLiquidityEvaluator evaluator;

    public MoneyWayNasdaqSessionLiquidityEvaluatorTests()
    {
        evaluator = new(calculator);
    }

    [Fact]
    public void IdentityExactlyMatchesBuiltInRuleDefinitionAndDependencyIsRequired()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var rule = definition.Rules.Single(item => item.RuleId.Value == "NQ-LIQ-001");

        Assert.Equal(definition.StrategyId, evaluator.StrategyId);
        Assert.Equal(definition.Version, evaluator.StrategyVersion);
        Assert.Equal(rule.RuleId, evaluator.RuleId);
        Assert.Equal(("Session liquidity levels", "Liquidity", 50, true, RuleDefinitionStatus.Confirmed, "docs/strategies/nasdaq/rule-catalog.md"),
            (rule.Name, rule.Stage, rule.Sequence, rule.IsRequired, rule.DefinitionStatus, rule.SourceReference));
        Assert.Throws<ArgumentNullException>(() => new MoneyWayNasdaqSessionLiquidityEvaluator(null!));
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    [Fact]
    public void ExactHourNotConfiguredMapsToDataUnavailable()
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var asOf = LocalUtc(TradingDay, 7);
        var context = ContextAt(asOf, MoneyWayNasdaqStrategyDefinition.Instance,
            Series(minute, CandleAtUtc(asOf.AddMinutes(-1), asOf, 100m, 90m, minute)));

        var decision = evaluator.Evaluate(context);

        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.Equal(NasdaqSessionLiquidityCalculator.ExactHourNotConfiguredReason, decision.Reason);
        Assert.Null(decision.EvidenceReference);
    }

    [Fact]
    public void SixtyMinutesDoesNotSubstituteForExactOneHour()
    {
        var sixtyMinutes = new Timeframe(60, TimeframeUnit.Minute);
        var context = ContextAt(LocalUtc(TradingDay, 7), MoneyWayNasdaqStrategyDefinition.Instance,
            Series(sixtyMinutes, CandleAt(TradingDay, 6, 100m, 90m, sixtyMinutes)));

        var decision = evaluator.Evaluate(context);

        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.Equal(NasdaqSessionLiquidityCalculator.ExactHourNotConfiguredReason, decision.Reason);
    }

    [Fact]
    public void BeforeSessionCompletionMapsToDataUnavailableRatherThanWaitingOrFailed()
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var asOf = LocalUtc(TradingDay, 6, 59);
        var context = ContextAt(
            asOf,
            MoneyWayNasdaqStrategyDefinition.Instance,
            Series(Hour, CompleteSessionCandles(TradingDay)),
            Series(minute, CandleAtUtc(asOf.AddMinutes(-1), asOf, 100m, 90m, minute)));

        var decision = evaluator.Evaluate(context);

        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.NotEqual(RuleEvaluationResult.Waiting, decision.Result);
        Assert.NotEqual(RuleEvaluationResult.Failed, decision.Result);
        Assert.Equal(NasdaqSessionLiquidityCalculator.SessionNotCompletedReason, decision.Reason);
    }

    [Fact]
    public void MissingSessionCandleMapsToDataUnavailableWithoutPartialSuccess()
    {
        var missing = LocalUtc(TradingDay, 4);
        var candles = CompleteSessionCandles(TradingDay)
            .Where(candle => candle.OpenTimeUtc != missing)
            .ToArray();
        var context = ContextAt(LocalUtc(TradingDay, 7), MoneyWayNasdaqStrategyDefinition.Instance, Series(Hour, candles));

        var decision = evaluator.Evaluate(context);

        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.Equal(NasdaqSessionLiquidityCalculator.IncompleteSessionDataReason, decision.Reason);
    }

    [Fact]
    public void CompleteBoundaryDataMapsToPassedWithoutTransportingLevelsInReason()
    {
        var candles = CompleteSessionCandles(TradingDay)
            .Append(CandleAt(TradingDay, 7, 9_000m, -9_000m))
            .ToArray();
        var context = ContextAt(LocalUtc(TradingDay, 8), MoneyWayNasdaqStrategyDefinition.Instance, Series(Hour, candles));

        var levels = calculator.Calculate(context).Levels;
        var decision = evaluator.Evaluate(context);

        Assert.Equal(new NasdaqSessionLiquidityLevels(TradingDay, 500m, 10m, 700m, 20m), levels);
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        Assert.Equal(NasdaqSessionLiquidityCalculator.AvailableReason, decision.Reason);
        Assert.DoesNotContain("AsiaHigh=", decision.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("LondonHigh=", decision.Reason, StringComparison.Ordinal);
        Assert.Null(decision.EvidenceReference);
    }

    [Fact]
    public void ContextUseCaseRetainsDefinitionMetadataAuthority()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var context = ContextAt(LocalUtc(TradingDay, 7), definition, Series(Hour, CompleteSessionCandles(TradingDay)));

        var observation = new EvaluateStrategyReplayContextUseCase([evaluator]).Execute(definition, context);
        var evaluation = Assert.Single(observation.Evaluations);

        Assert.Equal(new RuleId("NQ-LIQ-001"), evaluation.RuleId);
        Assert.Equal(50, evaluation.Sequence);
        Assert.True(evaluation.IsRequired);
        Assert.Equal(RuleDefinitionStatus.Confirmed, evaluation.DefinitionStatus);
        Assert.Equal(context.AsOfUtc, evaluation.EvaluatedAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, evaluation.Result);
    }

    [Fact]
    public void DecisionsAreFutureIsolatedRepeatableAndIndependentOfCallOrderAndUnrelatedTimeframes()
    {
        var asOf = LocalUtc(TradingDay, 7);
        var nextDay = TradingDay.AddDays(1);
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var fiveMinutes = new Timeframe(5, TimeframeUnit.Minute);
        var fourHours = new Timeframe(4, TimeframeUnit.Hour);
        var first = ContextAt(
            asOf,
            MoneyWayNasdaqStrategyDefinition.Instance,
            Series(Hour, CompleteSessionCandles(TradingDay).Append(CandleAt(TradingDay, 7, 1_000m, -1_000m)).ToArray()));
        var second = ContextAt(
            asOf,
            MoneyWayNasdaqStrategyDefinition.Instance,
            Series(Hour, CompleteSessionCandles(TradingDay).Append(CandleAt(TradingDay, 7, 9_000m, -9_000m)).ToArray()),
            Series(minute, CandleAtUtc(asOf.AddMinutes(-1), asOf, 90_000m, -90_000m, minute)),
            Series(fiveMinutes, CandleAtUtc(asOf.AddMinutes(-5), asOf, 80_000m, -80_000m, fiveMinutes)),
            Series(fourHours, CandleAtUtc(asOf.AddHours(-4), asOf, 70_000m, -70_000m, fourHours)));
        var next = ContextAt(LocalUtc(nextDay, 7), MoneyWayNasdaqStrategyDefinition.Instance,
            Series(Hour, CompleteSessionCandles(nextDay)));

        var nextFirst = evaluator.Evaluate(next);
        var firstAfterNext = evaluator.Evaluate(first);
        var secondDecision = evaluator.Evaluate(second);
        var firstAgain = evaluator.Evaluate(first);
        var nextAgain = evaluator.Evaluate(next);

        AssertDecisionEqual(firstAfterNext, secondDecision);
        AssertDecisionEqual(firstAfterNext, firstAgain);
        AssertDecisionEqual(nextFirst, nextAgain);
    }

    [Fact]
    public void RejectsMismatchedStrategyContext()
    {
        var other = new StrategyDefinition(new("other"), new("v1"), "Other", "test",
            [new(new("R"), "Rule", "stage", 1, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
        var context = ContextAt(LocalUtc(TradingDay, 7), other, Series(Hour, CompleteSessionCandles(TradingDay)));

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(context));
    }

    private static void AssertDecisionEqual(ReplayRuleEvaluationDecision expected, ReplayRuleEvaluationDecision actual)
    {
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(expected.Reason, actual.Reason);
        Assert.Equal(expected.EvidenceReference, actual.EvidenceReference);
    }

    private static Candle[] CompleteSessionCandles(DateOnly tradingDay)
    {
        var candles = new List<Candle>();
        for (var hour = 17; hour < 24; hour++)
        {
            candles.Add(CandleAt(tradingDay.AddDays(-1), hour, hour == 17 ? 500m : 200m + hour, 50m));
        }

        candles.Add(CandleAt(tradingDay, 0, 210m, 40m));
        candles.Add(CandleAt(tradingDay, 1, 220m, 10m));
        candles.Add(CandleAt(tradingDay, 2, 700m, 70m));
        candles.Add(CandleAt(tradingDay, 3, 300m, 60m));
        candles.Add(CandleAt(tradingDay, 4, 310m, 50m));
        candles.Add(CandleAt(tradingDay, 5, 320m, 40m));
        candles.Add(CandleAt(tradingDay, 6, 330m, 20m));
        return candles.ToArray();
    }

    private static StrategyReplayContext ContextAt(
        DateTimeOffset asOfUtc,
        StrategyDefinition definition,
        params CandleSeries[] series)
    {
        StrategyReplayContext? context = null;
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        var factory = new CreateStrategyReplayContextUseCase();

        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc)
            {
                context = factory.Execute(definition, frame);
            }
        }

        return context ?? throw new InvalidOperationException("The requested replay timestamp was not produced.");
    }

    private static CandleSeries Series(Timeframe timeframe, params Candle[] candles) =>
        new(Provider, Symbol, timeframe, candles);

    private static Candle CandleAt(
        DateOnly day,
        int hour,
        decimal high,
        decimal low,
        Timeframe? timeframe = null)
    {
        var open = LocalUtc(day, hour);
        return CandleAtUtc(open, open.AddHours(1), high, low, timeframe ?? Hour);
    }

    private static Candle CandleAtUtc(
        DateTimeOffset open,
        DateTimeOffset close,
        decimal high,
        decimal low,
        Timeframe timeframe)
    {
        var middle = (high + low) / 2m;
        return new(Provider, Symbol, timeframe, open, close, middle, high, low, middle, null);
    }

    private static DateTimeOffset LocalUtc(DateOnly day, int hour, int minute = 0)
    {
        var local = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Unspecified);
        return new(TimeZoneInfo.ConvertTimeToUtc(local, Bogota), TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveBogota()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId("America/Bogota", out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
