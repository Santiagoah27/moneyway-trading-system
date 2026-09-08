using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class MoneyWayNasdaqSessionLiquidityEvaluatorScenarioTests
{
    private static readonly DateOnly TradingDay = new(2026, 1, 15);
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly TimeZoneInfo Bogota = ResolveBogota();

    [Fact]
    public void CanonicalBacktestDistinguishesUnavailableEvaluationFromMissingRequiredCoverage()
    {
        var liquidityRuleId = new RuleId("NQ-LIQ-001");
        var otherMissingRuleId = new RuleId("NQ-H4-001");
        var report = Facade().Execute(
            MoneyWayNasdaqStrategyDefinition.Instance,
            [new CandleSeries(Provider, Symbol, Hour, CompleteSessionCandles())]);

        var beforeIndex = report.OutcomeRun.StrategyRun.StrategyObservations
            .Select((observation, index) => (observation, index))
            .Single(item => item.observation.AsOfUtc == LocalUtc(TradingDay, 6))
            .index;
        var afterIndex = report.OutcomeRun.StrategyRun.StrategyObservations
            .Select((observation, index) => (observation, index))
            .Single(item => item.observation.AsOfUtc == LocalUtc(TradingDay, 7))
            .index;

        AssertFrame(beforeIndex, RuleEvaluationResult.DataUnavailable);
        AssertFrame(afterIndex, RuleEvaluationResult.Passed);
        Assert.Equal(0, report.ReadyCount);
        Assert.Equal(report.FrameCount, report.DataUnavailableCount);
        Assert.Equal(report.FrameCount, report.IncompleteRequiredCoverageCount);
        Assert.Equal(10, report.MissingRequiredRules.Count);
        Assert.DoesNotContain(report.MissingRequiredRules, item => item.RuleId == liquidityRuleId);
        Assert.Contains(report.MissingRequiredRules, item => item.RuleId == otherMissingRuleId);

        void AssertFrame(int index, RuleEvaluationResult expectedResult)
        {
            var observation = report.OutcomeRun.StrategyRun.StrategyObservations[index];
            var outcome = report.OutcomeRun.Outcomes[index];
            var evaluation = observation.Evaluations.Single(item => item.RuleId == liquidityRuleId);

            Assert.Equal(expectedResult, evaluation.Result);
            Assert.False(outcome.HasCompleteRequiredCoverage);
            Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
            Assert.DoesNotContain(liquidityRuleId, outcome.MissingRequiredRuleIds);
            Assert.Contains(otherMissingRuleId, outcome.MissingRequiredRuleIds);
            Assert.Equal(10, outcome.MissingRequiredRuleIds.Count);
        }
    }

    private static GenerateCanonicalMultiTimeframeBacktestUseCase Facade() =>
        new(new(new(new RunMultiTimeframeReplayUseCase(), new(), new(MoneyWayReplayRuleEvaluators.GetAll())), new()),
            new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase());

    private static Candle[] CompleteSessionCandles()
    {
        var candles = new List<Candle>();
        for (var hour = 17; hour < 24; hour++)
        {
            candles.Add(CandleAt(TradingDay.AddDays(-1), hour, hour == 17 ? 500m : 200m + hour, 50m));
        }

        candles.Add(CandleAt(TradingDay, 0, 210m, 40m));
        candles.Add(CandleAt(TradingDay, 1, 220m, 10m));
        candles.Add(CandleAt(TradingDay, 2, 700m, 70m));
        candles.Add(CandleAt(TradingDay, 3, 300m, 60m));
        candles.Add(CandleAt(TradingDay, 4, 310m, 50m));
        candles.Add(CandleAt(TradingDay, 5, 320m, 40m));
        candles.Add(CandleAt(TradingDay, 6, 330m, 20m));
        return candles.ToArray();
    }

    private static Candle CandleAt(DateOnly day, int hour, decimal high, decimal low)
    {
        var open = LocalUtc(day, hour);
        var close = open.AddHours(1);
        var middle = (high + low) / 2m;
        return new(Provider, Symbol, Hour, open, close, middle, high, low, middle, null);
    }

    private static DateTimeOffset LocalUtc(DateOnly day, int hour)
    {
        var local = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Unspecified);
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
