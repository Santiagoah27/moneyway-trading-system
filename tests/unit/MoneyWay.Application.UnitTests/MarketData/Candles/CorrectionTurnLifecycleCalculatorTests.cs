using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class CorrectionTurnLifecycleCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly CorrectionTurnLifecycleCalculator calculator = new();
    private readonly BullishCorrectionTerminalObservationCalculator bullishObservationCalculator = new();
    private readonly BullishCorrectionTerminalTransitionCalculator bullishTransitionCalculator = new();
    private readonly BearishCorrectionTerminalObservationCalculator bearishObservationCalculator = new();
    private readonly BearishCorrectionTerminalTransitionCalculator bearishTransitionCalculator = new();
    private readonly CorrectionBoundaryObservationCalculator boundaryObservationCalculator = new();
    private readonly CorrectionStartTransitionCalculator startTransitionCalculator = new();

    [Fact]
    public void BullishContinuationAppendsCurrentCandleAndPreservesOrder()
    {
        var (existing, current) = Turn(Candle(2, 100, 119, 95, 110));
        var transition = BullishTransition(existing, current, ceiling: 120, priorHl: 90);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.ContinueExistingCorrection, transition.TransitionKind);
        Assert.Equal([existing[0], existing[1], current], result.ResultingTurnCandles);
        Assert.True(result.IsCorrectionTurnActive);
        Assert.False(result.WasPreviousTurnTerminated);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.ExistingTurn, result.CurrentCandleMembership);
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, result.Disposition);
    }

    [Fact]
    public void BullishResetAndStartCreatesNewTurnFromCurrentCandleOnly()
    {
        var (existing, current) = Turn(Candle(2, 110, 121, 95, 100));
        var transition = BullishTransition(existing, current, ceiling: 120, priorHl: 90);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection, transition.TransitionKind);
        Assert.Equal([current], result.ResultingTurnCandles);
        Assert.True(result.IsCorrectionTurnActive);
        Assert.True(result.WasPreviousTurnTerminated);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NewTurn, result.CurrentCandleMembership);
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, result.Disposition);
    }

    [Fact]
    public void BullishResetAndAwaitTerminatesOldTurnWithoutCurrentMembership()
    {
        var (existing, current) = Turn(Candle(2, 100, 121, 95, 110));
        var transition = BullishTransition(existing, current, ceiling: 120, priorHl: 90);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart, transition.TransitionKind);
        Assert.Empty(result.ResultingTurnCandles);
        AssertInactiveTerminatedWithoutMembership(result, CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart);
    }

    [Fact]
    public void BullishInvalidationExcludesCurrentCandleFromDestroyedTurn()
    {
        var (existing, current) = Turn(Candle(2, 100, 119, 80, 89));
        var transition = BullishTransition(existing, current, ceiling: 120, priorHl: 90);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.InvalidateBullishStructure, transition.TransitionKind);
        Assert.Empty(result.ResultingTurnCandles);
        AssertInactiveTerminatedWithoutMembership(result, CorrectionTurnLifecycleDisposition.StructureInvalidated);
    }

    [Fact]
    public void BullishCollisionCompositionAppliesCalculatedInvalidationMembership()
    {
        var (existing, current) = Turn(Candle(2, 100, 121, 80, 89));
        var observation = BullishObservation(existing, current, ceiling: 120, priorHl: 90);
        var transition = bullishTransitionCalculator.Evaluate(observation);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.True(observation.WasNewHighResetObserved);
        Assert.True(observation.WasPriorHlInvalidationObserved);
        Assert.Equal(BullishCorrectionTerminalTransitionKind.InvalidateBullishStructure, transition.TransitionKind);
        Assert.Empty(result.ResultingTurnCandles);
        AssertInactiveTerminatedWithoutMembership(result, CorrectionTurnLifecycleDisposition.StructureInvalidated);
    }

    [Fact]
    public void BearishContinuationAppendsCurrentCandle()
    {
        var (existing, current) = Turn(Candle(2, 95, 100, 81, 90));
        var transition = BearishTransition(existing, current, floor: 80, priorLh: 105);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.ContinueExistingCorrection, transition.TransitionKind);
        Assert.Equal([existing[0], existing[1], current], result.ResultingTurnCandles);
        Assert.True(result.IsCorrectionTurnActive);
        Assert.False(result.WasPreviousTurnTerminated);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.ExistingTurn, result.CurrentCandleMembership);
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, result.Disposition);
    }

    [Fact]
    public void BearishResetAndStartCreatesNewTurnFromCurrentCandleOnly()
    {
        var (existing, current) = Turn(Candle(2, 90, 100, 79, 95));
        var transition = BearishTransition(existing, current, floor: 80, priorLh: 105);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection, transition.TransitionKind);
        Assert.Equal([current], result.ResultingTurnCandles);
        Assert.True(result.IsCorrectionTurnActive);
        Assert.True(result.WasPreviousTurnTerminated);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NewTurn, result.CurrentCandleMembership);
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, result.Disposition);
    }

    [Fact]
    public void BearishResetAndAwaitTerminatesOldTurnWithoutCurrentMembership()
    {
        var (existing, current) = Turn(Candle(2, 95, 100, 79, 90));
        var transition = BearishTransition(existing, current, floor: 80, priorLh: 105);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart, transition.TransitionKind);
        Assert.Empty(result.ResultingTurnCandles);
        AssertInactiveTerminatedWithoutMembership(result, CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart);
    }

    [Fact]
    public void BearishInvalidationExcludesCurrentCandleFromDestroyedTurn()
    {
        var (existing, current) = Turn(Candle(2, 100, 111, 81, 106));
        var transition = BearishTransition(existing, current, floor: 80, priorLh: 105);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure, transition.TransitionKind);
        Assert.Empty(result.ResultingTurnCandles);
        AssertInactiveTerminatedWithoutMembership(result, CorrectionTurnLifecycleDisposition.StructureInvalidated);
    }

    [Fact]
    public void BearishCollisionCompositionAppliesCalculatedInvalidationMembership()
    {
        var (existing, current) = Turn(Candle(2, 100, 111, 79, 106));
        var observation = BearishObservation(existing, current, floor: 80, priorLh: 105);
        var transition = bearishTransitionCalculator.Evaluate(observation);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.True(observation.WasNewLowResetObserved);
        Assert.True(observation.WasPriorLhInvalidationObserved);
        Assert.Equal(BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure, transition.TransitionKind);
        Assert.Empty(result.ResultingTurnCandles);
        AssertInactiveTerminatedWithoutMembership(result, CorrectionTurnLifecycleDisposition.StructureInvalidated);
    }

    [Fact]
    public void EqualOhlcCandlesArePreservedByIdentityAndNotDeduplicated()
    {
        var first = Candle(0, 100, 110, 90, 105);
        var second = Candle(1, 100, 110, 90, 105);
        var current = Candle(2, 100, 110, 90, 105);
        IReadOnlyList<Candle> existing = [first, second];
        var transition = BullishTransition(existing, current, ceiling: 120, priorHl: 80);

        var result = calculator.Evaluate(existing, current, transition);

        Assert.Equal(3, result.ResultingTurnCandles.Count);
        Assert.Same(first, result.ResultingTurnCandles[0]);
        Assert.Same(second, result.ResultingTurnCandles[1]);
        Assert.Same(current, result.ResultingTurnCandles[2]);
    }

    [Fact]
    public void EvaluationDoesNotMutateInputAndReturnsReadOnlySnapshot()
    {
        var (existingSnapshot, current) = Turn(Candle(2, 100, 119, 95, 110));
        var existing = existingSnapshot.ToList();
        var transition = BullishTransition(existing, current, ceiling: 120, priorHl: 90);

        var result = calculator.Evaluate(existing, current, transition);
        existing.Clear();

        Assert.Equal(3, result.ResultingTurnCandles.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<Candle>)result.ResultingTurnCandles).Add(Candle(3, 100, 110, 90, 105)));
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var (existing, current) = Turn(Candle(2, 90, 100, 79, 95));
        var transition = BearishTransition(existing, current, floor: 80, priorLh: 105);

        var first = calculator.Evaluate(existing, current, transition);
        var second = calculator.Evaluate(existing, current, transition);

        Assert.Equal(first.IsCorrectionTurnActive, second.IsCorrectionTurnActive);
        Assert.Equal(first.WasPreviousTurnTerminated, second.WasPreviousTurnTerminated);
        Assert.Equal(first.CurrentCandleMembership, second.CurrentCandleMembership);
        Assert.Equal(first.ResultingTurnCandles, second.ResultingTurnCandles);
        Assert.Equal(first.Disposition, second.Disposition);
    }

    [Fact]
    public void NormalPreStartCanBeRepresentedWithoutFabricatingATerminalTransition()
    {
        var result = CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart();

        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, result.Disposition);
        Assert.False(result.IsCorrectionTurnActive);
        Assert.False(result.WasPreviousTurnTerminated);
        Assert.Empty(result.ResultingTurnCandles);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NoCorrectionTurn, result.CurrentCandleMembership);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<Candle>)result.ResultingTurnCandles).Add(Candle(0, 100, 110, 90, 105)));
    }

    [Fact]
    public void EmptyAwaitingAndInvalidatedTurnsHaveDistinctDispositions()
    {
        var (existing, awaitingCandle) = Turn(Candle(2, 100, 121, 95, 110));
        var invalidatingCandle = Candle(2, 100, 119, 80, 89);

        var awaiting = calculator.Evaluate(existing, awaitingCandle, BullishTransition(existing, awaitingCandle, 120, 90));
        var invalidated = calculator.Evaluate(existing, invalidatingCandle, BullishTransition(existing, invalidatingCandle, 120, 90));

        Assert.False(awaiting.IsCorrectionTurnActive);
        Assert.False(invalidated.IsCorrectionTurnActive);
        Assert.Empty(awaiting.ResultingTurnCandles);
        Assert.Empty(invalidated.ResultingTurnCandles);
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, awaiting.Disposition);
        Assert.Equal(CorrectionTurnLifecycleDisposition.StructureInvalidated, invalidated.Disposition);
    }

    [Theory]
    [InlineData(CorrectionOriginExtremeSide.Ceiling, 120, 100, 110, 110, 100)]
    [InlineData(CorrectionOriginExtremeSide.Floor, 80, 100, 90, 90, 100)]
    public void NormalPreStartExcludesWaitingCandlesAndStartsWithOnlyFirstOppositeBody(
        CorrectionOriginExtremeSide side,
        int extreme,
        int waitingOpen,
        int waitingClose,
        int startOpen,
        int startClose)
    {
        var waiting = Candle(0, waitingOpen, 120, 80, waitingClose);
        var doji = Candle(1, 100, 120, 80, 100);
        var start = Candle(2, startOpen, 120, 80, startClose);
        var initial = CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart();

        var afterWaiting = calculator.Evaluate(initial, waiting, StartTransition(extreme, waiting, side));
        var afterDoji = calculator.Evaluate(afterWaiting, doji, StartTransition(extreme, doji, side));
        var afterStart = calculator.Evaluate(afterDoji, start, StartTransition(extreme, start, side));

        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, afterWaiting.Disposition);
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, afterDoji.Disposition);
        Assert.False(afterWaiting.IsCorrectionTurnActive);
        Assert.False(afterDoji.IsCorrectionTurnActive);
        Assert.Empty(afterWaiting.ResultingTurnCandles);
        Assert.Empty(afterDoji.ResultingTurnCandles);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NoCorrectionTurn, afterDoji.CurrentCandleMembership);
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, afterStart.Disposition);
        Assert.True(afterStart.IsCorrectionTurnActive);
        Assert.False(afterStart.WasPreviousTurnTerminated);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NewTurn, afterStart.CurrentCandleMembership);
        Assert.Single(afterStart.ResultingTurnCandles);
        Assert.Same(start, afterStart.ResultingTurnCandles[0]);
    }

    [Fact]
    public void ResetAndAwaitThenWaitDojiAndStartExcludesDestroyedAndWaitingCandles()
    {
        var (existing, reset) = Turn(Candle(2, 100, 121, 95, 110));
        var resetTransition = BullishTransition(existing, reset, ceiling: 120, priorHl: 90);
        var afterReset = calculator.Evaluate(existing, reset, resetTransition);
        var waiting = Candle(3, 100, 121, 95, 110);
        var doji = Candle(4, 100, 121, 95, 100);
        var start = Candle(5, 110, 122, 95, 100);

        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, afterReset.Disposition);
        Assert.Empty(afterReset.ResultingTurnCandles);
        var afterWaiting = calculator.Evaluate(afterReset, waiting, StartTransition(121, waiting, CorrectionOriginExtremeSide.Ceiling));
        var afterDoji = calculator.Evaluate(afterWaiting, doji, StartTransition(121, doji, CorrectionOriginExtremeSide.Ceiling));
        var startTransition = StartTransition(121, start, CorrectionOriginExtremeSide.Ceiling);
        var afterStart = calculator.Evaluate(afterDoji, start, startTransition);

        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, afterWaiting.Disposition);
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, afterDoji.Disposition);
        Assert.Empty(afterWaiting.ResultingTurnCandles);
        Assert.Empty(afterDoji.ResultingTurnCandles);
        Assert.Equal(122m, startTransition.ResultingExtreme);
        Assert.Equal(CorrectionTurnLifecycleDisposition.ActiveCorrection, afterStart.Disposition);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NewTurn, afterStart.CurrentCandleMembership);
        Assert.Single(afterStart.ResultingTurnCandles);
        Assert.Same(start, afterStart.ResultingTurnCandles[0]);
    }

    [Theory]
    [InlineData(CorrectionOriginExtremeSide.Ceiling, 120, 100, 110, 110, 100, 121)]
    [InlineData(CorrectionOriginExtremeSide.Floor, 80, 100, 90, 90, 100, 79)]
    public void ExtremeUpdatesRemainInTransitionWhileLifecycleOnlyChangesMembership(
        CorrectionOriginExtremeSide side,
        int extreme,
        int waitingOpen,
        int waitingClose,
        int startOpen,
        int startClose,
        int updatedExtreme)
    {
        var waiting = side == CorrectionOriginExtremeSide.Ceiling
            ? Candle(0, waitingOpen, updatedExtreme, 80, waitingClose)
            : Candle(0, waitingOpen, 120, updatedExtreme, waitingClose);
        var start = side == CorrectionOriginExtremeSide.Ceiling
            ? Candle(1, startOpen, updatedExtreme + 1, 80, startClose)
            : Candle(1, startOpen, 120, updatedExtreme - 1, startClose);
        var waitingTransition = StartTransition(extreme, waiting, side);
        var afterWaiting = calculator.Evaluate(CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart(), waiting, waitingTransition);
        var startingTransition = StartTransition(waitingTransition.ResultingExtreme, start, side);
        var afterStart = calculator.Evaluate(afterWaiting, start, startingTransition);

        Assert.True(waitingTransition.WasExtremeUpdated);
        Assert.Equal(updatedExtreme, waitingTransition.ResultingExtreme);
        Assert.Empty(afterWaiting.ResultingTurnCandles);
        Assert.True(startingTransition.WasExtremeUpdated);
        Assert.Equal(side == CorrectionOriginExtremeSide.Ceiling ? updatedExtreme + 1 : updatedExtreme - 1,
            startingTransition.ResultingExtreme);
        Assert.Single(afterStart.ResultingTurnCandles);
        Assert.Same(start, afterStart.ResultingTurnCandles[0]);
    }

    [Fact]
    public void InvalidatedLifecycleRejectsBothStartTransitionKindsWithoutMutation()
    {
        var (existing, invalidationCandle) = Turn(Candle(2, 100, 119, 80, 89));
        var invalidated = calculator.Evaluate(existing, invalidationCandle,
            BullishTransition(existing, invalidationCandle, ceiling: 120, priorHl: 90));
        var waiting = Candle(3, 100, 120, 95, 110);
        var start = Candle(3, 110, 120, 95, 100);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(invalidated, waiting,
            StartTransition(120, waiting, CorrectionOriginExtremeSide.Ceiling)));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(invalidated, start,
            StartTransition(120, start, CorrectionOriginExtremeSide.Ceiling)));
        Assert.Equal(CorrectionTurnLifecycleDisposition.StructureInvalidated, invalidated.Disposition);
        Assert.Empty(invalidated.ResultingTurnCandles);
        Assert.Equal(2, existing.Count);
    }

    [Fact]
    public void ActiveLifecycleRejectsStartTransitionAndCannotAddTheStartCandleTwice()
    {
        var start = Candle(0, 110, 120, 90, 100);
        var transition = StartTransition(120, start, CorrectionOriginExtremeSide.Ceiling);
        var active = calculator.Evaluate(CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart(), start, transition);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(active, start, transition));
        Assert.Single(active.ResultingTurnCandles);
        Assert.Same(start, active.ResultingTurnCandles[0]);
    }

    [Fact]
    public void ResetCandleCannotBeProcessedAgainAsPreStart()
    {
        var (existing, reset) = Turn(Candle(2, 100, 121, 95, 110));
        var afterReset = calculator.Evaluate(existing, reset, BullishTransition(existing, reset, 120, 90));

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(afterReset, reset,
            StartTransition(121, reset, CorrectionOriginExtremeSide.Ceiling)));
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, afterReset.Disposition);
        Assert.Empty(afterReset.ResultingTurnCandles);
    }

    [Fact]
    public void StartTransitionMustIdentifyTheSameCurrentCandle()
    {
        var first = Candle(0, 110, 120, 90, 100);
        var other = Candle(0, 110, 120, 90, 100);
        var transition = StartTransition(120, first, CorrectionOriginExtremeSide.Ceiling);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(
            CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart(), other, transition));
    }

    [Fact]
    public void RepeatedPreStartEvaluationIsDeterministicAndDoesNotMutateInput()
    {
        var waiting = Candle(0, 100, 120, 90, 110);
        var initial = CorrectionTurnLifecycleResult.CreateAwaitingCorrectionStart();
        var transition = StartTransition(120, waiting, CorrectionOriginExtremeSide.Ceiling);

        var first = calculator.Evaluate(initial, waiting, transition);
        var second = calculator.Evaluate(initial, waiting, transition);

        Assert.Equal(first.Disposition, second.Disposition);
        Assert.Equal(first.IsCorrectionTurnActive, second.IsCorrectionTurnActive);
        Assert.Equal(first.CurrentCandleMembership, second.CurrentCandleMembership);
        Assert.Equal(first.ResultingTurnCandles, second.ResultingTurnCandles);
        Assert.Empty(initial.ResultingTurnCandles);
        Assert.Equal(CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, initial.Disposition);
    }

    [Fact]
    public void InvalidMembershipValueFollowsRepositoryValidationConventions()
    {
        var constructor = typeof(CorrectionTurnLifecycleResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();

        var exception = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([Array.Empty<Candle>(), false, true, (CorrectionTurnCurrentCandleMembership)99,
                CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, null]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void InvalidDispositionOrContradictoryStateIsRejected()
    {
        var constructor = typeof(CorrectionTurnLifecycleResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        var candle = Candle(0, 100, 110, 90, 105);

        Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([Array.Empty<Candle>(), false, true, CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                (CorrectionTurnLifecycleDisposition)99, null])).InnerException);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([new[] { candle }, false, true, CorrectionTurnCurrentCandleMembership.NewTurn,
                CorrectionTurnLifecycleDisposition.ActiveCorrection, candle])).InnerException);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([Array.Empty<Candle>(), true, true, CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, candle])).InnerException);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([new[] { candle }, false, true, CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                CorrectionTurnLifecycleDisposition.StructureInvalidated, candle])).InnerException);
    }

    [Fact]
    public void NullInputsFollowRepositoryValidationConventions()
    {
        var (existing, current) = Turn(Candle(2, 100, 119, 95, 110));
        var bullish = BullishTransition(existing, current, ceiling: 120, priorHl: 90);

        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!, current, bullish));
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(existing, null!, bullish));
        Assert.Throws<ArgumentNullException>(() =>
            calculator.Evaluate(existing, current, (BullishCorrectionTerminalTransitionResult)null!));
        Assert.Throws<ArgumentNullException>(() =>
            calculator.Evaluate(existing, current, (BearishCorrectionTerminalTransitionResult)null!));
    }

    private BullishCorrectionTerminalTransitionResult BullishTransition(
        IReadOnlyList<Candle> existing,
        Candle current,
        decimal ceiling,
        decimal priorHl) =>
        bullishTransitionCalculator.Evaluate(BullishObservation(existing, current, ceiling, priorHl));

    private BullishCorrectionTerminalObservationResult BullishObservation(
        IReadOnlyList<Candle> existing,
        Candle current,
        decimal ceiling,
        decimal priorHl)
    {
        var context = ContextAt(current.CloseTimeUtc, Series([.. existing, current]));
        return bullishObservationCalculator.EvaluateCurrentBoundary(context, Minute, ceiling, priorHl);
    }

    private BearishCorrectionTerminalTransitionResult BearishTransition(
        IReadOnlyList<Candle> existing,
        Candle current,
        decimal floor,
        decimal priorLh) =>
        bearishTransitionCalculator.Evaluate(BearishObservation(existing, current, floor, priorLh));

    private CorrectionStartTransitionResult StartTransition(
        decimal extreme,
        Candle current,
        CorrectionOriginExtremeSide side) =>
        startTransitionCalculator.Evaluate(boundaryObservationCalculator.Evaluate(extreme, current, side), current);

    private BearishCorrectionTerminalObservationResult BearishObservation(
        IReadOnlyList<Candle> existing,
        Candle current,
        decimal floor,
        decimal priorLh)
    {
        var context = ContextAt(current.CloseTimeUtc, Series([.. existing, current]));
        return bearishObservationCalculator.EvaluateCurrentBoundary(context, Minute, floor, priorLh);
    }

    private static void AssertInactiveTerminatedWithoutMembership(
        CorrectionTurnLifecycleResult result,
        CorrectionTurnLifecycleDisposition disposition)
    {
        Assert.False(result.IsCorrectionTurnActive);
        Assert.True(result.WasPreviousTurnTerminated);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NoCorrectionTurn, result.CurrentCandleMembership);
        Assert.Equal(disposition, result.Disposition);
    }

    private static (IReadOnlyList<Candle> Existing, Candle Current) Turn(Candle current) =>
        ([Candle(0, 100, 110, 90, 105), Candle(1, 105, 115, 95, 100)], current);

    private static StrategyReplayContext ContextAt(DateTimeOffset asOfUtc, CandleSeries series)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([series]);
        var factory = new CreateStrategyReplayContextUseCase();
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc) return factory.Execute(Definition, frame);
        }

        throw new InvalidOperationException("Requested replay boundary was not produced.");
    }

    private static CandleSeries Series(params Candle[] candles) => new(Provider, Symbol, Minute, candles);

    private static Candle Candle(int minute, decimal open, decimal high, decimal low, decimal close) =>
        new(
            Provider,
            Symbol,
            Minute,
            Start.AddMinutes(minute),
            Start.AddMinutes(minute + 1),
            open,
            high,
            low,
            close,
            null);

    private static readonly StrategyDefinition Definition = new(
        new("synthetic"),
        new("v1"),
        "Synthetic",
        "test",
        [new(new("R-1"), "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
}
