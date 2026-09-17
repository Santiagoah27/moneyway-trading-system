using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class StructuralCandidateLifecycleCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly StrategyDefinition Definition = new(
        new("synthetic"), new("v1"), "Synthetic", "test",
        [new(new("R-1"), "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);

    private readonly StructuralCandidateLifecycleCalculator calculator = new();
    private readonly StructuralCandidateTurnBoundaryCalculator boundaryCalculator = new();
    private readonly CorrectionTurnLifecycleCalculator turnLifecycleCalculator = new();
    private readonly CorrectionBoundaryObservationCalculator observationCalculator = new();
    private readonly CorrectionStartTransitionCalculator startTransitionCalculator = new();

    [Fact]
    public void ContinueConsumesExistingMembershipWithoutAppendingTwice()
    {
        var old = LowerStart();
        var current = Current(95, 100, 90, 99);
        var active = StartActive(old, CorrectionOriginExtremeSide.Ceiling, 105);
        var boundary = Bullish(current, active.ResultingTurnCandles);

        var result = calculator.Evaluate(active, boundary);
        var repeated = calculator.Evaluate(active, boundary);

        Assert.Equal(StructuralCandidateLifecycleStatus.Active, result.CandidateStatus);
        Assert.Same(boundary.Lifecycle, result.Lifecycle);
        Assert.Same(old, result.Lifecycle.ResultingTurnCandles[0]);
        Assert.Same(current, result.Lifecycle.ResultingTurnCandles[1]);
        Assert.Equal(2, result.Lifecycle.ResultingTurnCandles.Count);
        Assert.Null(result.ValidatedCandidate);
        Assert.Empty(result.CompletedCandidateTurnCandles);
        Assert.Equal(result.CandidateStatus, repeated.CandidateStatus);
        Assert.Equal(result.Lifecycle.ResultingTurnCandles, repeated.Lifecycle.ResultingTurnCandles);
        Assert.Single(active.ResultingTurnCandles);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(result.Lifecycle, boundary));
    }

    [Fact]
    public void ContinuePreservesTwoPriorCandleIdentitiesEvenWhenOhlcMatches()
    {
        var first = LowerStart();
        var second = new Candle(Provider, Symbol, Minute, Start.AddMinutes(-1), Start,
            first.Open, first.High, first.Low, first.Close, null);
        var initial = StartActive(first, CorrectionOriginExtremeSide.Ceiling, 105);
        var secondBoundary = Bullish(second, initial.ResultingTurnCandles);
        var twoCandleState = calculator.Evaluate(initial, secondBoundary);
        var current = Current(95, 100, 90, 99);
        var boundary = Bullish(current, twoCandleState.Lifecycle.ResultingTurnCandles);

        var result = calculator.Evaluate(twoCandleState.Lifecycle, boundary);

        Assert.Equal(3, result.Lifecycle.ResultingTurnCandles.Count);
        Assert.Same(first, result.Lifecycle.ResultingTurnCandles[0]);
        Assert.Same(second, result.Lifecycle.ResultingTurnCandles[1]);
        Assert.Same(current, result.Lifecycle.ResultingTurnCandles[2]);
        Assert.Single(initial.ResultingTurnCandles);
        Assert.Equal(2, twoCandleState.Lifecycle.ResultingTurnCandles.Count);
    }

    [Fact]
    public void ContinueThenCompoundConfirmationValidatesOnlyTheCompletedTwoCandleTurn()
    {
        var first = LowerStart();
        var second = new Candle(Provider, Symbol, Minute, Start.AddMinutes(-1), Start,
            91, 98, 85, 92, null);
        var initial = StartActive(first, CorrectionOriginExtremeSide.Ceiling, 105);
        var continued = calculator.Evaluate(initial, Bullish(second, initial.ResultingTurnCandles));
        var confirming = Current(104, 106, 96, 103);
        var boundary = Bullish(confirming, continued.Lifecycle.ResultingTurnCandles);

        var result = calculator.Evaluate(continued.Lifecycle, boundary);

        Assert.Equal(StructuralCandidateLifecycleStatus.Validated, result.CandidateStatus);
        Assert.Equal(90m, result.ValidatedCandidate!.StructuralPrice);
        Assert.Equal(80m, result.ValidatedCandidate.ProtectionAnchor);
        Assert.Equal(2, result.CompletedCandidateTurnCandles.Count);
        Assert.Same(first, result.CompletedCandidateTurnCandles[0]);
        Assert.Same(second, result.CompletedCandidateTurnCandles[1]);
        Assert.Same(confirming, Assert.Single(result.Lifecycle.ResultingTurnCandles));
        Assert.DoesNotContain(result.CompletedCandidateTurnCandles, candle => ReferenceEquals(candle, confirming));
    }

    [Fact]
    public void ConfirmOnlyPreservesOldGeometryAndAwaitsWithoutAnInventedExtreme()
    {
        var old = LowerStart();
        var current = Current(101, 104, 99, 102);
        var active = StartActive(old, CorrectionOriginExtremeSide.Ceiling, 105);
        var boundary = Bullish(current, active.ResultingTurnCandles);

        var result = calculator.Evaluate(active, boundary);

        Assert.Equal(StructuralCandidateLifecycleStatus.Validated, result.CandidateStatus);
        Assert.Same(boundary.CandidateValidation, result.ValidatedCandidate);
        Assert.Equal(90m, result.ValidatedCandidate!.StructuralPrice);
        Assert.Equal(80m, result.ValidatedCandidate.ProtectionAnchor);
        Assert.Same(old, Assert.Single(result.CompletedCandidateTurnCandles));
        Assert.Same(boundary.PreviousCandidateTurnCandles, result.CompletedCandidateTurnCandles);
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, result.Lifecycle.Disposition);
        Assert.Empty(result.Lifecycle.ResultingTurnCandles);
        Assert.Null(result.NewStructuralExtreme);
        Assert.Null(result.NewActivePairCandidatePrice);
    }

    [Fact]
    public void BullishCompoundConfirmationPreservesOldCandidateAndStartsNextTurnOnce()
    {
        var old = LowerStart();
        var current = Current(104, 106, 96, 103);
        var active = StartActive(old, CorrectionOriginExtremeSide.Ceiling, 105);
        var boundary = Bullish(current, active.ResultingTurnCandles);

        var result = calculator.Evaluate(active, boundary);

        Assert.Equal(StructuralCandidateLifecycleStatus.Validated, result.CandidateStatus);
        Assert.Equal((90m, 80m), (result.ValidatedCandidate!.StructuralPrice, result.ValidatedCandidate.ProtectionAnchor));
        Assert.Same(old, Assert.Single(result.CompletedCandidateTurnCandles));
        Assert.Equal((106m, 90m), (result.NewStructuralExtreme, result.NewActivePairCandidatePrice));
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, result.Lifecycle.Disposition);
        Assert.Same(current, Assert.Single(result.Lifecycle.ResultingTurnCandles));
        Assert.Same(boundary.Lifecycle, result.Lifecycle);
    }

    [Fact]
    public void BearishCompoundConfirmationPreservesMirroredRolloverAndNextTurn()
    {
        var old = UpperStart();
        var current = Current(97, 100, 94, 98);
        var active = StartActive(old, CorrectionOriginExtremeSide.Floor, 95);
        var boundary = Bearish(current, active.ResultingTurnCandles);

        var result = calculator.Evaluate(active, boundary);

        Assert.Equal(StructuralCandidateLifecycleStatus.Validated, result.CandidateStatus);
        Assert.Equal((110m, 120m), (result.ValidatedCandidate!.StructuralPrice, result.ValidatedCandidate.ProtectionAnchor));
        Assert.Same(old, Assert.Single(result.CompletedCandidateTurnCandles));
        Assert.Equal((94m, 110m), (result.NewStructuralExtreme, result.NewActivePairCandidatePrice));
        Assert.Same(current, Assert.Single(result.Lifecycle.ResultingTurnCandles));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompoundAwaitKeepsNextTurnEmptyAndOnlyLaterCandleCanStartIt(bool bearish)
    {
        var old = bearish ? UpperStart() : LowerStart();
        var current = bearish ? Current(99, 100, 94, 98) : Current(101, 106, 100, 103);
        var side = bearish ? CorrectionOriginExtremeSide.Floor : CorrectionOriginExtremeSide.Ceiling;
        var active = StartActive(old, side, bearish ? 95 : 105);
        var boundary = bearish
            ? Bearish(current, active.ResultingTurnCandles)
            : Bullish(current, active.ResultingTurnCandles);

        var result = calculator.Evaluate(active, boundary);

        Assert.Equal(StructuralCandidateLifecycleStatus.Validated, result.CandidateStatus);
        Assert.Same(old, Assert.Single(result.CompletedCandidateTurnCandles));
        Assert.NotNull(result.NewStructuralExtreme);
        Assert.NotNull(result.NewActivePairCandidatePrice);
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, result.Lifecycle.Disposition);
        Assert.Empty(result.Lifecycle.ResultingTurnCandles);

        var later = bearish
            ? new Candle(Provider, Symbol, Minute, Start.AddMinutes(1), Start.AddMinutes(2), 97, 100, 93, 98, null)
            : new Candle(Provider, Symbol, Minute, Start.AddMinutes(1), Start.AddMinutes(2), 107, 108, 100, 106, null);
        var laterObservation = observationCalculator.Evaluate(result.ResultingOriginExtreme, later, side);
        var laterTransition = startTransitionCalculator.Evaluate(laterObservation, later);
        var next = turnLifecycleCalculator.Evaluate(result.Lifecycle, later, laterTransition);

        Assert.Same(later, Assert.Single(next.ResultingTurnCandles));
        var sameCandleTransition = startTransitionCalculator.Evaluate(
            observationCalculator.Evaluate(result.ResultingOriginExtreme, current, side), current);
        Assert.Throws<ArgumentException>(() => turnLifecycleCalculator.Evaluate(result.Lifecycle, current, sameCandleTransition));
    }

    [Theory]
    [InlineData(99, 106, 95, 98, StructuralCandidateLifecycleStatus.Discarded, CorrectionTurnLifecycleDisposition.ActiveCorrection)]
    [InlineData(98, 106, 95, 98, StructuralCandidateLifecycleStatus.Discarded, CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart)]
    [InlineData(100, 106, 88, 89, StructuralCandidateLifecycleStatus.StructureInvalidated, CorrectionTurnLifecycleDisposition.StructureInvalidated)]
    public void NonConfirmingTerminalResultsDestroyCandidateWithoutRollover(
        int open, int high, int low, int close,
        StructuralCandidateLifecycleStatus status, CorrectionTurnLifecycleDisposition disposition)
    {
        var old = LowerStart();
        var current = Current(open, high, low, close);
        var active = StartActive(old, CorrectionOriginExtremeSide.Ceiling, 105);
        var boundary = Bullish(current, active.ResultingTurnCandles);

        var result = calculator.Evaluate(active, boundary);

        Assert.Equal(status, result.CandidateStatus);
        Assert.Equal(disposition, result.Lifecycle.Disposition);
        Assert.Null(result.ValidatedCandidate);
        Assert.Empty(result.CompletedCandidateTurnCandles);
        Assert.Null(result.NewStructuralExtreme);
        Assert.Null(result.NewActivePairCandidatePrice);
        if (disposition == CorrectionTurnLifecycleDisposition.ActiveCorrection)
            Assert.Same(current, Assert.Single(result.Lifecycle.ResultingTurnCandles));
        else
            Assert.Empty(result.Lifecycle.ResultingTurnCandles);

        if (disposition == CorrectionTurnLifecycleDisposition.StructureInvalidated)
        {
            var later = new Candle(Provider, Symbol, Minute, Start.AddMinutes(1), Start.AddMinutes(2),
                105, 108, 99, 104, null);
            var transition = startTransitionCalculator.Evaluate(
                observationCalculator.Evaluate(result.ResultingOriginExtreme, later, CorrectionOriginExtremeSide.Ceiling), later);
            Assert.Throws<ArgumentException>(() => turnLifecycleCalculator.Evaluate(result.Lifecycle, later, transition));
        }
    }

    [Fact]
    public void InactiveOrUnrelatedContinuingLifecycleCannotConsumeBoundary()
    {
        var current = Current(95, 100, 90, 99);
        var old = LowerStart();
        var active = StartActive(old, CorrectionOriginExtremeSide.Ceiling, 105);
        var boundary = Bullish(current, active.ResultingTurnCandles);
        var unrelated = StartActive(new Candle(Provider, Symbol, Minute, Start.AddMinutes(-2), Start.AddMinutes(-1),
            93, 95, 80, 90, null), CorrectionOriginExtremeSide.Ceiling, 105);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart(), boundary));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(unrelated, boundary));
    }

    [Theory]
    [InlineData(101, 104, 99, 102)]
    [InlineData(99, 106, 95, 98)]
    public void TerminalBoundaryFromAnotherTurnCannotBeAttachedToActiveLifecycle(
        int open, int high, int low, int close)
    {
        var old = LowerStart();
        var current = Current(open, high, low, close);
        var active = StartActive(old, CorrectionOriginExtremeSide.Ceiling, 105);
        var other = new Candle(Provider, Symbol, Minute, old.OpenTimeUtc, old.CloseTimeUtc,
            old.Open, old.High, old.Low, old.Close, null);
        var boundary = Bullish(current, [other]);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(active, boundary));
        Assert.Same(other, Assert.Single(boundary.PreviousCandidateTurnCandles));
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)boundary.PreviousCandidateTurnCandles).Add(current));
    }

    private CorrectionTurnLifecycleResult StartActive(Candle candle, CorrectionOriginExtremeSide side, decimal extreme)
    {
        var observation = observationCalculator.Evaluate(extreme, candle, side);
        var transition = startTransitionCalculator.Evaluate(observation, candle);
        return turnLifecycleCalculator.Evaluate(CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart(), candle, transition);
    }

    private StructuralCandidateTurnBoundaryResult Bullish(Candle current, IReadOnlyList<Candle> turn) =>
        boundaryCalculator.EvaluateCurrentBoundary(ContextAt(current), Minute, 100, 90, 105, turn,
            StructuralCandidateExtremeSide.Lower);

    private StructuralCandidateTurnBoundaryResult Bearish(Candle current, IReadOnlyList<Candle> turn) =>
        boundaryCalculator.EvaluateCurrentBoundary(ContextAt(current), Minute, 100, 110, 95, turn,
            StructuralCandidateExtremeSide.Upper);

    private static Candle LowerStart() => new(Provider, Symbol, Minute, Start.AddMinutes(-2), Start.AddMinutes(-1),
        92, 95, 80, 90, null);

    private static Candle UpperStart() => new(Provider, Symbol, Minute, Start.AddMinutes(-2), Start.AddMinutes(-1),
        108, 120, 100, 110, null);

    private static Candle Current(decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start, Start.AddMinutes(1), open, high, low, close, null);

    private static StrategyReplayContext ContextAt(Candle current)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, Minute, [current])]);
        Assert.True(cursor.TryAdvance(out var frame));
        return new CreateStrategyReplayContextUseCase().Execute(Definition, frame!);
    }
}
