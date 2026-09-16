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

    [Fact]
    public void InvalidMembershipValueFollowsRepositoryValidationConventions()
    {
        var constructor = typeof(CorrectionTurnLifecycleResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();

        var exception = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([Array.Empty<Candle>(), false, true, (CorrectionTurnCurrentCandleMembership)99,
                CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart]));

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
                (CorrectionTurnLifecycleDisposition)99])).InnerException);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([new[] { candle }, false, true, CorrectionTurnCurrentCandleMembership.NewTurn,
                CorrectionTurnLifecycleDisposition.ActiveCorrection])).InnerException);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([Array.Empty<Candle>(), true, true, CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart])).InnerException);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([new[] { candle }, false, true, CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                CorrectionTurnLifecycleDisposition.StructureInvalidated])).InnerException);
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
