using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class LondonAsiaInteractionTests
{
    private static readonly MarketDataProviderId Provider = new("synthetic-interaction");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateOnly Day = new(2026, 6, 23);
    private static readonly DateTimeOffset Start = new(2026, 6, 22, 22, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset London = Start.AddHours(9);
    private static readonly DateTimeOffset End = London.AddHours(6);
    private readonly AnalyzeLondonAsiaInteractionUseCase calculator = new();
    private static Candle CandleAt(DateTimeOffset time, decimal high, decimal low, decimal close = 100, decimal open = 100) => new(Provider, Symbol, Hour, time, time.AddHours(1), open, high, low, close, null);
    private static Candle Bar(int index, decimal high = 105, decimal low = 95, decimal close = 100, decimal open = 100) => CandleAt(London.AddHours(index), high, low, close, open);
    private static CandleSeries Source(params Candle[] replacements) => new(Provider, Symbol, Hour,
        Enumerable.Range(0, 9).Select(i => CandleAt(Start.AddHours(i), 110, 90))
            .Concat(Enumerable.Range(0, 6).Select(i => replacements.FirstOrDefault(c => c.OpenTimeUtc == London.AddHours(i)) ?? Bar(i))));
    private LondonAsiaInteractionResult Run(CandleSeries source, DateTimeOffset? cutoff = null) => calculator.Execute(source, Day, cutoff ?? End, M5ResearchDataOrigin.Synthetic, "synthetic:explicit fixture");

    [Theory]
    [InlineData("105", "95", "100", "100", LondonAsiaCompletedClassification.NoAsiaExtremePenetrated, false, false)]
    [InlineData("110", "95", "100", "100", LondonAsiaCompletedClassification.NoAsiaExtremePenetrated, true, false)]
    [InlineData("105", "90", "100", "100", LondonAsiaCompletedClassification.NoAsiaExtremePenetrated, false, true)]
    [InlineData("111", "95", "100", "100", LondonAsiaCompletedClassification.AsiaHighOnlyPenetrated, false, false)]
    [InlineData("105", "89", "100", "100", LondonAsiaCompletedClassification.AsiaLowOnlyPenetrated, false, false)]
    [InlineData("111", "89", "100", "100", LondonAsiaCompletedClassification.BothAsiaExtremesPenetrated, false, false)]
    [InlineData("111", "95", "111", "100", LondonAsiaCompletedClassification.AsiaHighOnlyPenetrated, false, false)]
    [InlineData("105", "89", "89", "100", LondonAsiaCompletedClassification.AsiaLowOnlyPenetrated, false, false)]
    [InlineData("111", "95", "110", "100", LondonAsiaCompletedClassification.AsiaHighOnlyPenetrated, false, false)]
    [InlineData("105", "89", "90", "100", LondonAsiaCompletedClassification.AsiaLowOnlyPenetrated, false, false)]
    [InlineData("113", "111", "112", "112", LondonAsiaCompletedClassification.AsiaHighOnlyPenetrated, false, false)]
    [InlineData("89", "87", "88", "88", LondonAsiaCompletedClassification.AsiaLowOnlyPenetrated, false, false)]
    public void ExactIndependentOhlcRelationshipsHaveNoCrossingOrTradeInterpretation(string high, string low, string close, string open,
        LondonAsiaCompletedClassification expected, bool upperContact, bool lowerContact)
    {
        static decimal Number(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        var c = Bar(0, Number(high), Number(low), Number(close), Number(open)); var result = Run(Source(c));
        var first = result.Observations[0]; Assert.Equal(expected, result.CompletedClassification);
        Assert.Equal(upperContact, first.AsiaHigh.ExactExtremeContact); Assert.Equal(lowerContact, first.AsiaLow.ExactExtremeContact);
        Assert.Equal(c.Close > 110, first.AsiaHigh.CloseBeyond); Assert.Equal(c.Close < 90, first.AsiaLow.CloseBeyond);
        Assert.Equal(c.High > 110 && c.Close <= 110, first.AsiaHigh.PenetrationWithoutCloseBeyond);
        Assert.Equal(c.Low < 90 && c.Close >= 90, first.AsiaLow.PenetrationWithoutCloseBeyond);
        Assert.Equal(c.Open > 110, first.AsiaHigh.OpensBeyondReference); Assert.Equal(c.Open < 90, first.AsiaLow.OpensBeyondReference);
        Assert.Equal(Math.Max(0, c.High - 110), result.CompletedUpperMagnitude); Assert.Equal(Math.Max(0, 90 - c.Low), result.CompletedLowerMagnitude);
        Assert.False(result.CanonicalAdmissionPermitted); Assert.False(result.OperationalSignal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FirstPenetrationAndCloseBeyondAreIndependentWithCandleLevelOrderingOnly(bool upperFirst)
    {
        var r = Run(Source(Bar(upperFirst ? 0 : 1, 111, 95), Bar(upperFirst ? 1 : 0, 105, 89), Bar(2, 112, 95, 111), Bar(3, 105, 88, 89)));
        Assert.Equal(London.AddHours(upperFirst ? 0 : 1), r.FirstUpperPenetration!.Candle.OpenTimeUtc);
        Assert.Equal(London.AddHours(upperFirst ? 1 : 0), r.FirstLowerPenetration!.Candle.OpenTimeUtc);
        Assert.Equal(London.AddHours(2), r.FirstUpperCloseBeyond!.Candle.OpenTimeUtc); Assert.Equal(London.AddHours(3), r.FirstLowerCloseBeyond!.Candle.OpenTimeUtc);
        Assert.Equal(upperFirst ? "UpperBeforeLowerAtCandleResolution" : "LowerBeforeUpperAtCandleResolution", r.FirstPenetrationOrdering);
        Assert.All(r.Observations, o => Assert.Equal(End, o.ObservableAtUtc));
        Assert.Equal(9, r.Asia.HighCandles.Count); Assert.Equal(9, r.Asia.LowCandles.Count); // tied Asia sources retained
    }

    [Fact]
    public void BothPenetrationsInOneCandleHaveUnknownOrderRegardlessOfCandleColor()
    {
        foreach (var close in new[] { 99m, 100m, 101m })
        {
            var r = Run(Source(Bar(0, 111, 89, close)));
            Assert.Equal("BothInSameCandle_OrderUnknown", r.FirstPenetrationOrdering); Assert.Equal("BothInSameCandle_OrderUnknown", r.Observations[0].Ordering);
        }
    }

    [Fact]
    public void ContactsAreSeparateFromStrictPenetrationAndFromEachOther()
    {
        var r = Run(Source(Bar(0, 110, 95), Bar(1, 105, 90)));
        Assert.Equal(LondonAsiaCompletedClassification.NoAsiaExtremePenetrated, r.CompletedClassification);
        Assert.Equal(London, r.FirstUpperExactContact!.Candle.OpenTimeUtc); Assert.Equal(London.AddHours(1), r.FirstLowerExactContact!.Candle.OpenTimeUtc);
        Assert.Null(r.FirstUpperPenetration); Assert.Null(r.FirstLowerPenetration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void MissingAsiaAbstainsInsteadOfReportingNoPenetration(int missing)
    {
        var s = Source(Bar(0, 111, 89)); var r = Run(new(Provider, Symbol, Hour, s.Candles.Where((_, i) => i != missing)));
        Assert.Equal("AsiaUnavailable", r.Status); Assert.Null(r.CompletedClassification); Assert.Empty(r.Observations);
        Assert.Null(r.ObservedLowerMagnitude); Assert.Contains("IncompleteCoverage", r.Reason);
        var none = Run(new(Provider, Symbol, Hour, s.Candles.Where(c => c.OpenTimeUtc >= London)));
        Assert.Equal("AsiaUnavailable", none.Status); Assert.Empty(none.Observations);
    }

    [Fact]
    public void PartialLondonReportsOnlyClosedObservationsNeverTheFinalClassification()
    {
        var source = Source(Bar(0, 111, 95), Bar(5, 105, 80));
        var beforeAsia = Run(source, London.AddTicks(-1)); Assert.Equal("AsiaUnavailable", beforeAsia.Status);
        var start = Run(source, London); Assert.Equal("Partial", start.Status); Assert.Empty(start.Observations); Assert.Null(start.CompletedClassification);
        var beforeClose = Run(source, London.AddHours(1).AddTicks(-1)); Assert.Empty(beforeClose.Observations);
        var closed = Run(source, London.AddHours(1)); Assert.Single(closed.Observations); Assert.NotNull(closed.FirstUpperPenetration);
        Assert.Null(closed.FirstLowerPenetration); Assert.Null(closed.CompletedLowerMagnitude); Assert.Null(closed.CompletedClassification);
        Assert.Equal(London.AddHours(1), closed.Observations[0].ObservableAtUtc);
        Assert.Equal(LondonAsiaCompletedClassification.BothAsiaExtremesPenetrated, Run(source).CompletedClassification);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void LondonHolesPreserveVisibleEventsButCannotMeanCompletedNoPenetration(int missing)
    {
        var s = Source(Bar(1, 105, 89)); var incomplete = new CandleSeries(Provider, Symbol, Hour, s.Candles.Where(c => c.OpenTimeUtc != London.AddHours(missing)));
        var r = Run(incomplete); Assert.Equal("LondonIncomplete", r.Status); Assert.Null(r.CompletedClassification); Assert.Null(r.CompletedLowerMagnitude);
        Assert.NotNull(r.FirstLowerPenetration); Assert.Equal(5, r.Observations.Count); Assert.Single(r.London.MissingOpensUtc);
        if (missing == 0) Assert.Equal("PartialWithMissingData", Run(incomplete, London.AddHours(2)).Status);
    }

    [Fact]
    public void ExactSessionEdgesExcludeTheLastAsiaCandleAndTheFirstPostLondonCandleFromInteractions()
    {
        var s = Source(Bar(0, 111, 95)); var outside = new CandleSeries(Provider, Symbol, Hour, s.Candles.Append(CandleAt(End, 999, 1)));
        var r = Run(outside, End.AddHours(1)); Assert.Equal(6, r.Observations.Count);
        Assert.Equal(London, r.Observations[0].Candle.OpenTimeUtc); Assert.Equal(End, r.Observations[^1].Candle.CloseTimeUtc);
        Assert.Equal(1, r.CompletedUpperMagnitude); Assert.Equal(0, r.CompletedLowerMagnitude);
    }

    [Fact]
    public void ExactDecimalPrecisionIsNotRoundedOrTolerated()
    {
        const decimal tiny = 0.00000000000000000000000001m;
        var r = Run(Source(Bar(0, 110 + tiny, 90 - tiny, 110 + tiny)));
        Assert.Equal(tiny, r.CompletedUpperMagnitude); Assert.Equal(tiny, r.CompletedLowerMagnitude);
        Assert.True(r.Observations[0].AsiaHigh.CloseBeyond); Assert.False(r.Observations[0].AsiaHigh.ExactExtremeContact);
    }

    [Fact]
    public void ExactSourceIdentityTimeframeAndResearchProvenanceAreRequired()
    {
        var s = Source();
        Assert.Throws<ArgumentException>(() => Run(new(Provider, Symbol, new(60, TimeframeUnit.Minute), [])));
        Assert.Throws<ArgumentException>(() => new CandleSeries(Provider, new("USTEC"), Hour, s.Candles));
        Assert.Throws<ArgumentException>(() => new CandleSeries(new("other"), Symbol, Hour, s.Candles));
        Assert.Throws<ArgumentException>(() => calculator.Execute(s, Day, End, M5ResearchDataOrigin.Synthetic, ""));
        Assert.Throws<ArgumentException>(() => Run(s, End.ToOffset(TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void EveryEarlierPrefixAndRepeatedEvaluationRemainsStableWithFutureSourceExtension()
    {
        var s = Source(Bar(0, 111, 95), Bar(5, 105, 80)); var future = new CandleSeries(Provider, Symbol, Hour, s.Candles.Append(CandleAt(End, 999, 1)));
        foreach (var cutoff in new[] { London.AddTicks(-1), London, London.AddHours(1).AddTicks(-1), London.AddHours(1), End.AddTicks(-1), End })
        {
            var prefix = new CandleSeries(Provider, Symbol, Hour, future.Candles.Where(c => c.CloseTimeUtc <= cutoff));
            var expected = JsonSerializer.Serialize(Run(prefix, cutoff)); Assert.Equal(expected, JsonSerializer.Serialize(Run(future, cutoff)));
            Assert.Equal(expected, JsonSerializer.Serialize(Run(prefix, cutoff)));
        }
        Assert.False(typeof(IReplayRuleFact).IsAssignableFrom(typeof(LondonAsiaInteractionResult)));
        Assert.False(typeof(IStrategyReplayInputObservation).IsAssignableFrom(typeof(LondonAsiaInteractionResult)));
        Assert.False(typeof(IReplayRuleEvaluator).IsAssignableFrom(typeof(AnalyzeLondonAsiaInteractionUseCase)));
    }
}
