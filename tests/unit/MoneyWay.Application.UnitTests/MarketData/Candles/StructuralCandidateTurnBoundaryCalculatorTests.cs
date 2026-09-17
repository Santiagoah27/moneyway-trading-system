using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class StructuralCandidateTurnBoundaryCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly StrategyDefinition Definition = new(
        new("synthetic"), new("v1"), "Synthetic", "test",
        [new(new("R-1"), "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);

    private readonly StructuralCandidateTurnBoundaryCalculator calculator = new();

    [Theory]
    [InlineData(95, 100, 90, 99)]
    [InlineData(95, 100, 90, 100)]
    [InlineData(95, 101, 90, 99)]
    public void BullishNonConfirmingBoundaryContinuesOldTurn(int open, int high, int low, int close)
    {
        var current = Current(open, high, low, close);
        var oldTurn = LowerTurn();

        var result = Bullish(current, oldTurn);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn, result.Kind);
        Assert.False(result.CandidateValidation.IsValidated);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.ExistingTurn, result.Lifecycle.CurrentCandleMembership);
        Assert.Equal(2, result.Lifecycle.ResultingTurnCandles.Count);
        Assert.Same(current, result.Lifecycle.ResultingTurnCandles[1]);
        Assert.Single(oldTurn);
    }

    [Theory]
    [InlineData(105, 100, 108, 105)]
    [InlineData(105, 100, 108, 100)]
    [InlineData(105, 99, 108, 100)]
    public void BearishNonConfirmingBoundaryContinuesOldTurn(int open, int low, int high, int close)
    {
        var current = Current(open, high, low, close);
        var result = Bearish(current);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn, result.Kind);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.ExistingTurn, result.Lifecycle.CurrentCandleMembership);
        Assert.Same(current, result.Lifecycle.ResultingTurnCandles[^1]);
    }

    [Fact]
    public void StrictConfirmationsWithoutOriginExtensionPreserveOldGeometry()
    {
        var bullishCandle = Current(101, 104, 99, 102);
        var bearishCandle = Current(99, 101, 96, 98);
        var bullish = Bullish(bullishCandle);
        var bearish = Bearish(bearishCandle);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.ConfirmCandidate, bullish.Kind);
        Assert.Equal(StructuralCandidateTurnBoundaryKind.ConfirmCandidate, bearish.Kind);
        Assert.True(bullish.CandidateValidation.IsValidated);
        Assert.True(bearish.CandidateValidation.IsValidated);
        Assert.Equal((90m, 80m), (bullish.CandidateValidation.StructuralPrice, bullish.CandidateValidation.ProtectionAnchor));
        Assert.Equal((110m, 120m), (bearish.CandidateValidation.StructuralPrice, bearish.CandidateValidation.ProtectionAnchor));
        Assert.Empty(bullish.Lifecycle.ResultingTurnCandles);
        Assert.Empty(bearish.Lifecycle.ResultingTurnCandles);
        Assert.Null(bullish.NewStructuralExtreme);
        Assert.Null(bearish.NewStructuralExtreme);
    }

    [Fact]
    public void BullishCompoundCorrectiveCandleValidatesAndStartsNextTurnOnce()
    {
        var current = Current(104, 106, 96, 103);
        var oldTurn = LowerTurn();
        var result = Bullish(current, oldTurn);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndStartNextCorrection, result.Kind);
        Assert.True(result.CandidateValidation.IsValidated);
        Assert.Equal((90m, 80m), (result.CandidateValidation.StructuralPrice, result.CandidateValidation.ProtectionAnchor));
        Assert.Equal((106m, 90m), (result.NewStructuralExtreme, result.NewActivePairCandidatePrice));
        Assert.Equal(106m, result.ResultingOriginExtreme);
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, result.Lifecycle.Disposition);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NewTurn, result.Lifecycle.CurrentCandleMembership);
        Assert.Same(current, Assert.Single(result.Lifecycle.ResultingTurnCandles));
        Assert.DoesNotContain(oldTurn, candle => ReferenceEquals(candle, current));
    }

    [Fact]
    public void BearishCompoundCorrectiveCandleValidatesAndStartsNextTurnOnce()
    {
        var current = Current(97, 100, 94, 98);
        var result = Bearish(current);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndStartNextCorrection, result.Kind);
        Assert.True(result.CandidateValidation.IsValidated);
        Assert.Equal((94m, 110m), (result.NewStructuralExtreme, result.NewActivePairCandidatePrice));
        Assert.Equal(94m, result.ResultingOriginExtreme);
        Assert.Same(current, Assert.Single(result.Lifecycle.ResultingTurnCandles));
    }

    [Theory]
    [InlineData(101, 106, 100, 103, false)]
    [InlineData(103, 106, 100, 103, false)]
    [InlineData(99, 100, 94, 98, true)]
    [InlineData(98, 100, 94, 98, true)]
    public void CompoundNonCorrectiveBodyAwaitsWithEmptyNextTurn(
        int open, int high, int low, int close, bool bearish)
    {
        var current = Current(open, high, low, close);
        var result = bearish ? Bearish(current) : Bullish(current);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndAwaitNextCorrection, result.Kind);
        Assert.True(result.CandidateValidation.IsValidated);
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, result.Lifecycle.Disposition);
        Assert.Empty(result.Lifecycle.ResultingTurnCandles);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NoCorrectionTurn, result.Lifecycle.CurrentCandleMembership);
        Assert.NotNull(result.NewStructuralExtreme);
    }

    [Theory]
    [InlineData(99, 106, 95, 98, false, true)]
    [InlineData(98, 106, 95, 98, false, false)]
    [InlineData(100, 108, 94, 101, true, true)]
    [InlineData(102, 105, 94, 101, true, false)]
    public void NonConfirmingResetUsesExistingTerminalDisposition(
        int open, int high, int low, int close, bool bearish, bool starts)
    {
        var current = Current(open, high, low, close);
        var result = bearish ? Bearish(current) : Bullish(current);

        Assert.Equal(starts
            ? StructuralCandidateTurnBoundaryKind.ResetAndStartNewCorrection
            : StructuralCandidateTurnBoundaryKind.ResetAndAwaitCorrectionStart, result.Kind);
        Assert.False(result.CandidateValidation.IsValidated);
        Assert.Equal(starts ? 1 : 0, result.Lifecycle.ResultingTurnCandles.Count);
        Assert.True(result.Lifecycle.WasPreviousTurnTerminated);
        Assert.Null(result.NewStructuralExtreme);
    }

    [Theory]
    [InlineData(100, 104, 88, 89, false)]
    [InlineData(100, 106, 88, 89, false)]
    [InlineData(105, 112, 96, 111, true)]
    [InlineData(105, 112, 94, 111, true)]
    public void InvalidationDominatesAnyResetWithoutConfirmingCandidate(
        int open, int high, int low, int close, bool bearish)
    {
        var current = Current(open, high, low, close);
        var result = bearish ? Bearish(current) : Bullish(current);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.StructureInvalidated, result.Kind);
        Assert.False(result.CandidateValidation.IsValidated);
        Assert.Equal(CorrectionTurnLifecycleDisposition.StructureInvalidated, result.Lifecycle.Disposition);
        Assert.Empty(result.Lifecycle.ResultingTurnCandles);
        Assert.Null(result.NewStructuralExtreme);
    }

    [Fact]
    public void InvalidOrderedReferencesAreRejectedBeforeAConfirmationInvalidationCollision()
    {
        var current = Current(104, 106, 85, 103);
        var context = ContextAt(current.CloseTimeUtc, Series(current));

        Assert.Throws<ArgumentException>(() => calculator.EvaluateCurrentBoundary(
            context, Minute, 100, 100, 105, LowerTurn(), StructuralCandidateExtremeSide.Lower));
        Assert.Throws<ArgumentException>(() => calculator.EvaluateCurrentBoundary(
            context, Minute, 100, 100, 95, UpperTurn(), StructuralCandidateExtremeSide.Upper));
    }

    [Fact]
    public void DuplicateOhlcValuesRemainDistinctAndRepeatedInputsRemainEquivalent()
    {
        var current = Current(95, 100, 90, 99);
        var turn = LowerTurn();
        var duplicate = new Candle(Provider, Symbol, Minute, Start.AddMinutes(-2), Start.AddMinutes(-1),
            turn[0].Open, turn[0].High, turn[0].Low, turn[0].Close, null);
        var oldTurn = new[] { turn[0], duplicate };

        var first = Bullish(current, oldTurn);
        var second = Bullish(current, oldTurn);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn, first.Kind);
        Assert.Equal(3, first.Lifecycle.ResultingTurnCandles.Count);
        Assert.Same(oldTurn[0], first.Lifecycle.ResultingTurnCandles[0]);
        Assert.Same(duplicate, first.Lifecycle.ResultingTurnCandles[1]);
        Assert.Same(current, first.Lifecycle.ResultingTurnCandles[2]);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.CandidateValidation.StructuralPrice, second.CandidateValidation.StructuralPrice);
        Assert.Equal(first.Lifecycle.CurrentCandleMembership, second.Lifecycle.CurrentCandleMembership);
        Assert.Equal(2, oldTurn.Length);
    }

    [Fact]
    public void FutureCandleInReplaySeriesDoesNotChangeCurrentBoundaryDecision()
    {
        var current = Current(104, 106, 96, 103);
        var future = new Candle(Provider, Symbol, Minute, Start.AddMinutes(1), Start.AddMinutes(2),
            103, 150, 70, 120, null);
        var turn = LowerTurn();
        var withoutFuture = calculator.EvaluateCurrentBoundary(
            ContextAt(current.CloseTimeUtc, Series(current)), Minute, 100, 90, 105, turn,
            StructuralCandidateExtremeSide.Lower);
        var withFuture = calculator.EvaluateCurrentBoundary(
            ContextAt(current.CloseTimeUtc, Series(current, future)), Minute, 100, 90, 105, turn,
            StructuralCandidateExtremeSide.Lower);

        Assert.Equal(withoutFuture.Kind, withFuture.Kind);
        Assert.Equal(withoutFuture.CandidateValidation.StructuralPrice, withFuture.CandidateValidation.StructuralPrice);
        Assert.Equal(withoutFuture.NewStructuralExtreme, withFuture.NewStructuralExtreme);
        Assert.Equal(withoutFuture.Lifecycle.CurrentCandleMembership, withFuture.Lifecycle.CurrentCandleMembership);
        Assert.Throws<ArgumentException>(() => calculator.EvaluateCurrentBoundary(
            ContextAt(current.CloseTimeUtc, Series(current, future)), Minute, 100, 90, 105,
            [turn[0], future], StructuralCandidateExtremeSide.Lower));
    }

    private StructuralCandidateTurnBoundaryResult Bullish(Candle current, IReadOnlyList<Candle>? turn = null) =>
        calculator.EvaluateCurrentBoundary(ContextAt(current.CloseTimeUtc, Series(current)), Minute,
            100, 90, 105, turn ?? LowerTurn(), StructuralCandidateExtremeSide.Lower);

    private StructuralCandidateTurnBoundaryResult Bearish(Candle current, IReadOnlyList<Candle>? turn = null) =>
        calculator.EvaluateCurrentBoundary(ContextAt(current.CloseTimeUtc, Series(current)), Minute,
            100, 110, 95, turn ?? UpperTurn(), StructuralCandidateExtremeSide.Upper);

    private static IReadOnlyList<Candle> LowerTurn() =>
        [new(Provider, Symbol, Minute, Start.AddMinutes(-2), Start.AddMinutes(-1), 90, 95, 80, 92, null)];

    private static IReadOnlyList<Candle> UpperTurn() =>
        [new(Provider, Symbol, Minute, Start.AddMinutes(-2), Start.AddMinutes(-1), 110, 120, 100, 108, null)];

    private static Candle Current(decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start, Start.AddMinutes(1), open, high, low, close, null);

    private static CandleSeries Series(params Candle[] candles) => new(Provider, Symbol, Minute, candles);

    private static StrategyReplayContext ContextAt(DateTimeOffset asOfUtc, params CandleSeries[] series)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        var factory = new CreateStrategyReplayContextUseCase();
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc) return factory.Execute(Definition, frame);
        }

        throw new InvalidOperationException("Requested replay boundary was not produced.");
    }
}
