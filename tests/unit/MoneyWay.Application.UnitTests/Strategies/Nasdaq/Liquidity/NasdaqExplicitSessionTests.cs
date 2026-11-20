using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.Liquidity;

public sealed class NasdaqExplicitSessionTests
{
    private readonly NasdaqSessionLiquidityCalculator calculator = new();
    internal static readonly MarketDataProviderId Provider = new("synthetic-session-fixture");
    internal static readonly MarketSymbol Symbol = new("US100");
    internal static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    internal static DateTimeOffset Start(DateOnly d) => new(d.AddDays(-1).ToDateTime(new TimeOnly(22, 0)), TimeSpan.Zero);
    internal static Candle Bar(DateTimeOffset open, Timeframe tf, int minutes, decimal high = 110, decimal low = 90) => new(Provider, Symbol, tf, open, open.AddMinutes(minutes), 100, high, low, 100, null);
    internal static CandleSeries Complete(DateOnly d, int minutes = 60)
    {
        var tf = minutes == 60 ? Hour : new Timeframe(minutes, TimeframeUnit.Minute);
        return new(Provider, Symbol, tf, Enumerable.Range(0, 900 / minutes).Select(i => Bar(Start(d).AddMinutes(i * minutes), tf, minutes)));
    }
    internal static StrategyReplayContext Context(CandleSeries source, DateTimeOffset cutoff, bool candidate = true)
    {
        var marker = new CandleSeries(source.ProviderId, source.Symbol, new(1, TimeframeUnit.Minute), [new(source.ProviderId, source.Symbol, new(1, TimeframeUnit.Minute), cutoff.AddMinutes(-1), cutoff, 100, 100, 100, 100, null)]);
        var cursor = new MultiTimeframeCandleReplayCursor(new[] { source, marker });
        while (cursor.TryAdvance(out var frame)) if (frame!.AsOfUtc == cutoff) return new CreateStrategyReplayContextUseCase().Execute(candidate ? MoneyWayNasdaqSessionCandidate.Definition : MoneyWayNasdaqM5StrictCloseCandidate.Definition, frame);
        throw new InvalidOperationException();
    }

    [Theory]
    [InlineData("2026-01-01")]
    [InlineData("2026-03-01")]
    [InlineData("2024-02-29")]
    [InlineData("2024-03-01")]
    [InlineData("2026-06-23")]
    [InlineData("2026-12-31")]
    public void ExplicitBogotaDateOwnershipIsStableAcrossCalendarBoundaries(string date)
    {
        var d = DateOnly.Parse(date); var source = Complete(d); var cutoff = Start(d).AddHours(15);
        var sessions = calculator.CalculateExplicitSessions(source, d, cutoff);
        var asia = sessions[0]; var london = sessions[1];
        Assert.Equal(Start(d), asia.StartUtc); Assert.Equal(Start(d).AddHours(9), asia.EndUtc); Assert.Equal(asia.EndUtc, london.StartUtc); Assert.Equal(cutoff, london.EndUtc);
        Assert.All(sessions, s => { Assert.True(s.IsComplete); Assert.Equal(d, s.OperatingDate); Assert.Equal(cutoff, s.AvailableAtUtc); Assert.Equal("AuditedExactH1", s.SourceAuthority); });
        Assert.Equal(d.AddDays(-1), DateOnly.FromDateTime(asia.StartUtc.AddHours(-5).DateTime));
        Assert.Equal(new TimeOnly(17, 0), TimeOnly.FromDateTime(asia.StartUtc.AddHours(-5).DateTime));
        Assert.Equal(new TimeOnly(2, 0), TimeOnly.FromDateTime(asia.EndUtc.AddHours(-5).DateTime));
        Assert.Equal(new TimeOnly(8, 0), TimeOnly.FromDateTime(london.EndUtc.AddHours(-5).DateTime));
    }

    [Theory]
    [InlineData(60, 9, 6)]
    [InlineData(30, 18, 12)]
    [InlineData(5, 108, 72)]
    [InlineData(1, 540, 360)]
    public void FullIntervalsAreIncludedExactlyOnceWithExplicitSourceAuthority(int minutes, int asiaCount, int londonCount)
    {
        var d = new DateOnly(2026, 6, 23); var source = Complete(d, minutes); var end = Start(d).AddHours(15);
        var extended = new CandleSeries(Provider, Symbol, source.Timeframe, source.Candles.Prepend(Bar(Start(d).AddMinutes(-minutes), source.Timeframe, minutes, 999, -999)).Append(Bar(end, source.Timeframe, minutes, 999, -999)));
        var s = calculator.CalculateExplicitSessions(extended, d, end.AddMinutes(minutes));
        Assert.Equal(asiaCount, s[0].ExpectedCount); Assert.Equal(londonCount, s[1].ExpectedCount);
        Assert.Equal(asiaCount, s[0].SourceCandles.Count); Assert.Equal(londonCount, s[1].SourceCandles.Count);
        Assert.Equal(Start(d), s[0].SourceCandles[0].OpenTimeUtc); Assert.Equal(Start(d).AddHours(9).AddMinutes(-minutes), s[0].SourceCandles[^1].OpenTimeUtc);
        Assert.Equal(Start(d).AddHours(9), s[1].SourceCandles[0].OpenTimeUtc); Assert.Equal(end.AddMinutes(-minutes), s[1].SourceCandles[^1].OpenTimeUtc);
        Assert.Empty(s[0].SourceCandles.Intersect(s[1].SourceCandles));
        Assert.All(s, x => { Assert.Equal(110, x.High); Assert.Equal(90, x.Low); Assert.Equal(x.SourceCandles.Count, x.HighCandles.Count); Assert.Equal(x.SourceCandles.Count, x.LowCandles.Count); });
        Assert.Equal(minutes == 60 ? "AuditedExactH1" : "ExplicitNonAuthoritativeComparison", s[0].SourceAuthority);
    }

    [Theory]
    [InlineData(8, false, false)]
    [InlineData(9, true, false)]
    [InlineData(14, true, false)]
    [InlineData(15, true, true)]
    public void OnlyCompletedObservableSessionsExposeFinalReferences(int hours, bool asia, bool london)
    {
        var d = new DateOnly(2026, 6, 23); var source = Complete(d); var cutoff = Start(d).AddHours(hours);
        var s = calculator.CalculateExplicitSessions(source, d, cutoff);
        Assert.Equal(asia, s[0].IsComplete); Assert.Equal(london, s[1].IsComplete);
        foreach (var x in s.Where(x => !x.IsComplete)) { Assert.Null(x.High); Assert.Null(x.Low); Assert.Null(x.AvailableAtUtc); Assert.Empty(x.HighCandles); }
        Assert.All(s.SelectMany(x => x.SourceCandles), c => Assert.True(c.CloseTimeUtc <= cutoff));
        Assert.Equal(asia && london, calculator.Calculate(Context(source, cutoff)).IsAvailable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(14)]
    public void MissingCandlesPreventOnlyTheirSessionReferenceAndCanonicalPair(int missingIndex)
    {
        var d = new DateOnly(2026, 6, 23); var source = Complete(d); var end = Start(d).AddHours(15);
        var missing = new CandleSeries(Provider, Symbol, Hour, source.Candles.Where((_, i) => i != missingIndex));
        var result = calculator.CalculateExplicitSessions(missing, d, end);
        Assert.Equal("IncompleteCoverage", result[missingIndex < 9 ? 0 : 1].CoverageStatus);
        Assert.Single(result[missingIndex < 9 ? 0 : 1].MissingOpensUtc); Assert.False(calculator.Calculate(Context(missing, end)).IsAvailable);
    }

    [Fact]
    public void UniqueExactDecimalExtremesAndTiesKeepEverySourceIdentity()
    {
        var d = new DateOnly(2026, 6, 23); var source = Complete(d);
        var changed = new CandleSeries(Provider, Symbol, Hour, source.Candles.Select((c, i) => Bar(c.OpenTimeUtc, Hour, 60, i is 1 or 3 ? 110.00000000000000000000000001m : 110, i == 2 ? 89.99999999999999999999999999m : 90)));
        var s = calculator.CalculateExplicitSessions(changed, d, Start(d).AddHours(15))[0];
        Assert.Equal(110.00000000000000000000000001m, s.High); Assert.Equal(89.99999999999999999999999999m, s.Low);
        Assert.Equal(new[] { Start(d).AddHours(1), Start(d).AddHours(3) }, s.HighCandles.Select(c => c.OpenTimeUtc)); Assert.Equal(Start(d).AddHours(2), Assert.Single(s.LowCandles).OpenTimeUtc);
    }

    [Fact]
    public void CrossingIntervalsAndMissingWholeSessionsCannotBecomeConfirmedExtremes()
    {
        var d = new DateOnly(2026, 6, 23); var source = Complete(d); var end = Start(d).AddHours(15);
        var crossing = new CandleSeries(Provider, Symbol, Hour, source.Candles.Select((c, i) => i == 8 ? Bar(c.OpenTimeUtc, Hour, 90) : c).Where((_, i) => i != 9));
        Assert.All(calculator.CalculateExplicitSessions(crossing, d, end), s => { Assert.False(s.IsComplete); Assert.Equal("InvalidSourceIntervals", s.CoverageStatus); Assert.Null(s.High); });
        var empty = new CandleSeries(Provider, Symbol, Hour, []); Assert.All(calculator.CalculateExplicitSessions(empty, d, end), s => Assert.Equal("IncompleteCoverage", s.CoverageStatus));
    }

    [Fact]
    public void CandleAndSeriesContractsRejectDuplicatesContradictionsAndWrongIdentity()
    {
        var d = new DateOnly(2026, 6, 23); var s = Complete(d); var c = s.Candles[0];
        Assert.Throws<ArgumentException>(() => new CandleSeries(Provider, Symbol, Hour, [c, c]));
        Assert.Throws<ArgumentException>(() => new CandleSeries(new("other"), Symbol, Hour, s.Candles));
        Assert.Throws<ArgumentException>(() => new CandleSeries(Provider, new("USTEC"), Hour, s.Candles));
        Assert.Throws<ArgumentException>(() => Bar(c.OpenTimeUtc, Hour, 60, 99, 90));
        Assert.Throws<ArgumentException>(() => calculator.CalculateExplicitSessions(new(Provider, Symbol, new(60, TimeframeUnit.Minute), []), d, Start(d).AddHours(15)));
        Assert.Throws<ArgumentException>(() => calculator.CalculateExplicitSessions(s, d, Start(d).ToOffset(TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void LegacySevenAmAndCandidateEightAmRemainDistinctAndSourceBindingUsesCorrectWindow()
    {
        var d = new DateOnly(2026, 6, 23); var source = Complete(d); var seven = Start(d).AddHours(14); var eight = seven.AddHours(1);
        Assert.True(calculator.Calculate(Context(source, seven, false)).IsAvailable); Assert.False(calculator.Calculate(Context(source, seven)).IsAvailable);
        var atEight = Context(source, eight); var calculation = calculator.Calculate(atEight);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(atEight, calculation, NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh, eight, "synthetic:session reference");
        Assert.Equal(15, reference.SourceCandles.Count); Assert.Equal(eight, reference.SourceCandles[^1].CloseTimeUtc);
        Assert.Throws<ArgumentException>(() => new NasdaqLiquidityTakeReference.SessionLevel(Context(source, eight, false), calculation, NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh, eight, "synthetic:wrong version"));
        Assert.False(calculator.Calculate(Context(Complete(d, 30), eight)).IsAvailable);
    }

    [Fact]
    public void EveryEarlierPrefixAndRepeatedResultIsIndependentOfFutureCandles()
    {
        var d = new DateOnly(2026, 6, 23); var source = Complete(d); var future = new CandleSeries(Provider, Symbol, Hour, source.Candles.Append(Bar(Start(d).AddHours(15), Hour, 60, 999, -999)));
        foreach (var hours in new[] { 8, 9, 14, 15, 16 })
        {
            var cutoff = Start(d).AddHours(hours).AddTicks(-1); var prefix = new CandleSeries(Provider, Symbol, Hour, future.Candles.Where(c => c.CloseTimeUtc <= cutoff));
            var expected = JsonSerializer.Serialize(calculator.CalculateExplicitSessions(prefix, d, cutoff));
            Assert.Equal(expected, JsonSerializer.Serialize(calculator.CalculateExplicitSessions(future, d, cutoff)));
            Assert.Equal(expected, JsonSerializer.Serialize(calculator.CalculateExplicitSessions(prefix, d, cutoff)));
        }
    }
}
