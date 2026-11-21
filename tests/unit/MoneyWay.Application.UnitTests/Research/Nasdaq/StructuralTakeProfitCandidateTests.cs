using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class StructuralTakeProfitCandidateTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe H1 = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 6, 23, 13, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 6, 23);

    [Theory]
    [InlineData(M5FvgDirection.Bullish, 120, M5StructuralPivotKind.High)]
    [InlineData(M5FvgDirection.Bearish, 80, M5StructuralPivotKind.Low)]
    public void NearestSwingRetainsEqualPricesAndPriorReachWithoutRejectingTargets(
        M5FvgDirection direction, int price, M5StructuralPivotKind kind)
    {
        var series = Swings(direction);
        var close = Close(55, direction, 115.25m);
        var result = Target(series, close, direction);
        Assert.Equal(StructuralTargetOutcome.TiedTargetsIdentified, result.Outcome);
        Assert.Equal(price, result.TargetPrice);
        Assert.Equal(4.75m, result.RewardDistancePoints);
        Assert.Equal(2, result.SelectedIds.Count);
        var selected = result.Candidates.Where(c => result.SelectedIds.Contains(c.Id)).ToArray();
        Assert.All(selected, candidate => Assert.Equal(kind, candidate.Kind));
        Assert.All(selected, c => Assert.Equal(StructuralTargetInteraction.ReachedInClosedM5, c.PriorInteraction!.Status));
        Assert.NotEqual(selected[0].Id, selected[1].Id);
        Assert.Contains(result.Candidates, c => c.Reason == StructuralTargetReason.WrongSide && c.DistancePoints < 0);
        Assert.Contains(result.Candidates, c => c.Reason == StructuralTargetReason.EqualToReference && c.DistancePoints == 0);
        Assert.Contains(result.Candidates, c => c.Reason == StructuralTargetReason.Eligible && c.DistancePoints > 4.75m);
        Assert.Same(close, result.CloseReference.Candle);
    }

    [Fact]
    public void RightBarConfirmationAndBetweenBarsUseTheObservableM1Cutoff()
    {
        var series = Swings(M5FvgDirection.Bullish);
        var early = Target(series, Close(14), M5FvgDirection.Bullish);
        Assert.Empty(early.Candidates);
        Assert.Equal(StructuralTargetOutcome.InsufficientEvidence, early.Outcome);
        var exact = Target(series, Close(15), M5FvgDirection.Bullish);
        var candidate = Assert.Single(exact.Candidates);
        Assert.Equal(Start.AddMinutes(15), candidate.AvailableAtUtc);
        Assert.Equal(Start.AddMinutes(5), candidate.SourceTimestampUtc);
        var between = Target(series, Close(18), M5FvgDirection.Bullish);
        Assert.Equal(exact.TargetPrice, between.TargetPrice);
        Assert.Equal(exact.AsOfUtc, Assert.Single(between.Candidates).PriorInteraction!.CoveredThroughUtc);
    }

    [Fact]
    public void FutureAppendMutationAndRepeatPreserveTheExactTargetSnapshot()
    {
        var full = Swings(M5FvgDirection.Bullish);
        var close = Close(18);
        var prefix = Series(M5, full.Candles.Take(3));
        var mutated = Series(M5, prefix.Candles.Append(Bar(M5, Start.AddMinutes(15), 1m, 999m)));
        string Run(CandleSeries series) => JsonSerializer.Serialize(Target(series, close, M5FvgDirection.Bullish));
        Assert.Equal(Run(prefix), Run(full));
        Assert.Equal(Run(prefix), Run(mutated));
        Assert.Equal(Run(full), Run(full));
    }

    [Fact]
    public void MissingOrNonfavorableM5EvidenceHasAnExplicitOutcome()
    {
        var noBars = Target(Series(M5, []), Close(15), M5FvgDirection.Bullish);
        Assert.Equal(StructuralTargetOutcome.InsufficientEvidence, noBars.Outcome);
        Assert.Equal("NoClosedM5Candles", noBars.ReasonCode);
        var flat = Series(M5, Enumerable.Range(0, 3).Select(i => Bar(M5, Start.AddMinutes(i * 5), 100, 120)));
        Assert.Equal(StructuralTargetOutcome.NoHistoricalReference, Target(flat, Close(15), M5FvgDirection.Bullish).Outcome);
        var full = Swings(M5FvgDirection.Bullish);
        var gap = Target(Series(M5, full.Candles.Take(3)), Close(20), M5FvgDirection.Bullish);
        Assert.Single(gap.PivotSourceGaps);
        Assert.Empty(gap.Candidates);
        Assert.Equal(StructuralTargetOutcome.InsufficientEvidence, gap.Outcome);
        var wrongSide = Target(full, Close(45, price: 130), M5FvgDirection.Bullish);
        Assert.Equal(StructuralTargetOutcome.NoFavorableReference, wrongSide.Outcome);
        Assert.Null(wrongSide.TargetPrice);
    }

    [Theory]
    [InlineData(M5FvgDirection.Bullish, 120, M5StructuralPivotKind.High)]
    [InlineData(M5FvgDirection.Bearish, 80, M5StructuralPivotKind.Low)]
    public void ExactH1AsiaAndLondonRemainDistinctEvenWhenTheyShareAPrice(
        M5FvgDirection direction, int level, M5StructuralPivotKind kind)
    {
        var close = Close(5, direction, 115.25m);
        var result = Sessions(close, direction, Hours());
        Assert.Equal(StructuralTargetOutcome.TiedTargetsIdentified, result.Outcome);
        Assert.Equal(level, result.TargetPrice);
        Assert.Equal(4.75m, result.RewardDistancePoints);
        Assert.Equal(2, result.SelectedIds.Count);
        Assert.Equal(new[] { "Asia", "London" }, result.Candidates.Select(c => c.Session!.Session));
        Assert.All(result.Candidates, c =>
        {
            Assert.Equal(kind, c.Kind);
            Assert.Equal(close.CloseTimeUtc, c.AvailableAtUtc);
            Assert.Equal("1h", c.Timeframe);
            Assert.NotEmpty(c.Session!.ExtremeCandles);
        });
    }

    [Fact]
    public void SessionBoundaryCoverageAndAvailabilityAreDiagnosedSeparately()
    {
        var calculator = new NasdaqSessionLiquidityCalculator();
        var hour = Hours();
        var early = Close(-31);
        var earlyResult = Sessions(early, M5FvgDirection.Bullish, hour);
        var london = earlyResult.Candidates.Single(c => c.Session!.Session == "London");
        Assert.Equal(StructuralTargetReason.NotYetObservable, london.Reason);
        Assert.Null(london.Price);
        Assert.Null(london.AvailableAtUtc);
        Assert.Empty(london.Session!.ExtremeCandles);

        var close = Close(5);
        var incomplete = Series(H1, hour.Candles.Skip(1));
        var result = Sessions(close, M5FvgDirection.Bullish, incomplete);
        var asia = result.Candidates.Single(c => c.Session!.Session == "Asia");
        Assert.Equal(StructuralTargetReason.SourceGap, asia.Reason);
        Assert.Single(asia.Session!.MissingObservableOpensUtc);
        Assert.Single(result.SelectedIds);

        var evaluated = calculator.CalculateExplicitSessions(hour, Day, close.CloseTimeUtc);
        var m5 = Series(M5, []);
        var missingTimestamp = evaluated[0] with { EvaluatedAtUtc = default };
        var futureSnapshot = evaluated[1] with { EvaluatedAtUtc = close.CloseTimeUtc.AddHours(1), High = 900m };
        var unavailable = NasdaqStructuralTakeProfitCandidate.Sessions(new(close), M5FvgDirection.Bullish, m5,
            [futureSnapshot, missingTimestamp], "fixture", []);
        Assert.Equal(StructuralTargetOutcome.ReferencesUnavailable, unavailable.Outcome);
        Assert.Equal(StructuralTargetReason.InsufficientEvidence, unavailable.Candidates[0].Reason);
        Assert.Equal(StructuralTargetReason.NotYetObservable, unavailable.Candidates[1].Reason);
        Assert.All(unavailable.Candidates, c => Assert.Null(c.Price));
    }

    [Fact]
    public void PriorInteractionDistinguishesDocumentedClosureFromAnUnexplainedGap()
    {
        var m5 = Series(M5,
            [Bar(M5, Start.AddMinutes(-30), 89, 119), Bar(M5, Start.AddMinutes(-20), 89, 119)]);
        var calculator = new NasdaqSessionLiquidityCalculator();
        var close = Close(-15);
        var london = calculator.CalculateExplicitSessions(Hours(), Day, close.CloseTimeUtc).Single(s => s.Session == "London");
        StructuralTargetInteractionEvidence Interaction(IReadOnlyList<M5FvgClosure> closures) =>
            Assert.Single(NasdaqStructuralTakeProfitCandidate.Sessions(new(close), M5FvgDirection.Bullish, m5,
                [london], "fixture", closures).Candidates).PriorInteraction!;
        var gap = Interaction([]);
        Assert.Equal(StructuralTargetInteraction.UnknownDueToSourceGap, gap.Status);
        Assert.Null(Assert.Single(gap.Gaps).ClosureEvidence);
        var documented = Interaction([new(Start.AddMinutes(-25), Start.AddMinutes(-20), "documented closure fixture")]);
        Assert.Equal(StructuralTargetInteraction.NotReachedInCoveredM5, documented.Status);
        Assert.Equal("documented closure fixture", Assert.Single(documented.Gaps).ClosureEvidence);
    }

    [Fact]
    public void PairingUsesExactDecimalGeometryWithoutARatioThresholdAndKeepsMissingSl()
    {
        var direction = M5FvgDirection.Bullish;
        var m5 = Swings(direction);
        var close = Close(45, direction, 115.25m);
        var stop = Stop(m5, close, direction);
        var target = NasdaqStructuralTakeProfitCandidate.Swings(stop, m5, []);
        var targetId = target.SelectedIds[0];
        var pivot = new M5StructuralPivot("stop", M5StructuralPivotKind.Low, 107.65m, Start, Start.AddMinutes(5),
            Start.AddMinutes(10), stop.Profile.Id, false);
        var protective = stop with
        {
            Selected = new(pivot, [], M5StructuralRelation.HigherLow),
            DistancePoints = 7.60m,
            Outcome = M5StructuralStopOutcome.StructuralReferenceIdentified
        };
        var pair = CompareNasdaqStructuralTakeProfitUseCase.Pair(targetId, target, protective);
        Assert.Equal(0.625m, pair.HypotheticalRewardRisk);
        Assert.Equal(pivot.Id, pair.StopPivotId);
        Assert.Equal(107.65m, pair.StopPrice);
        var nonprotective = CompareNasdaqStructuralTakeProfitUseCase.Pair(targetId, target,
            protective with { Outcome = M5StructuralStopOutcome.ReferenceNotProtective, DistancePoints = null });
        Assert.Equal(pair.TargetId, nonprotective.TargetId);
        Assert.Equal(4.75m, nonprotective.RewardDistancePoints);
        Assert.Equal(pivot.Id, nonprotective.StopPivotId);
        Assert.Null(nonprotective.HypotheticalRewardRisk);
        var missing = CompareNasdaqStructuralTakeProfitUseCase.Pair(targetId, target,
            stop with { Selected = null, DistancePoints = null, Outcome = M5StructuralStopOutcome.NoConfirmedHigherLow });
        Assert.Null(missing.StopPivotId);
        Assert.Null(missing.HypotheticalRewardRisk);
    }

    private static StructuralTargetEvaluation Sessions(Candle close, M5FvgDirection direction, CandleSeries hours) =>
        NasdaqStructuralTakeProfitCandidate.Sessions(new(close), direction, Series(M5, []),
            new NasdaqSessionLiquidityCalculator().CalculateExplicitSessions(hours, Day, close.CloseTimeUtc), "fixture", []);

    private static StructuralTargetEvaluation Target(CandleSeries m5, Candle close, M5FvgDirection direction) =>
        NasdaqStructuralTakeProfitCandidate.Swings(Stop(m5, close, direction), m5, []);

    private static M5StructuralStopObservation Stop(CandleSeries m5, Candle close, M5FvgDirection direction) =>
        new EvaluateM5StructuralStopLossCandidateUseCase().Execute(m5, close, direction, new(1, 1), "fixture");

    private static CandleSeries Swings(M5FvgDirection direction)
    {
        decimal[] highs = [108, 120, 108, 120, 108, 125, 108, 115.25m, 108, 114, 108];
        return Series(M5, highs.Select((high, i) => direction == M5FvgDirection.Bullish
            ? Bar(M5, Start.AddMinutes(5 * i), 100, high)
            : Bar(M5, Start.AddMinutes(5 * i), 200 - high, 100)));
    }

    private static CandleSeries Hours() => Series(H1, Enumerable.Range(0, 15)
        .Select(i => Bar(H1, Start.AddMinutes(-30).AddHours(i - 15), 80, 120)));

    private static CandleSeries Series(Timeframe timeframe, IEnumerable<Candle> candles) => new(Provider, Symbol, timeframe, candles);

    private static Candle Bar(Timeframe tf, DateTimeOffset at, decimal low, decimal high) =>
        new(Provider, Symbol, tf, at, tf == H1 ? at.AddHours(1) : at.AddMinutes(5), low, high, low, low, null);

    private static Candle Close(int minute, M5FvgDirection direction = M5FvgDirection.Bullish, decimal price = 115.25m)
    {
        var close = direction == M5FvgDirection.Bullish ? price : 200 - price;
        var open = direction == M5FvgDirection.Bullish ? close - 1 : close + 1;
        return new(Provider, Symbol, M1, Start.AddMinutes(minute - 1), Start.AddMinutes(minute), open,
            Math.Max(open, close), Math.Min(open, close), close, null);
    }
}
