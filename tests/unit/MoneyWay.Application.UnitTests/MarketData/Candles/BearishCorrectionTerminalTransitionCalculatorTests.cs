using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class BearishCorrectionTerminalTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly BearishCorrectionTerminalObservationCalculator observationCalculator = new();
    private readonly BearishCorrectionTerminalTransitionCalculator transitionCalculator = new();

    [Theory]
    [InlineData(90, 95)]
    [InlineData(95, 90)]
    [InlineData(90, 90)]
    public void NoTerminalEventContinuesExistingCorrectionRegardlessOfBodyDirection(decimal open, decimal close)
    {
        var result = Evaluate(Candle(open, 100, 81, close), floor: 80, priorLh: 105);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.ContinueExistingCorrection, result.TransitionKind);
        Assert.Equal((80m, 80m), (result.PreviousFloor, result.ResultingFloor));
        Assert.False(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBearishCorrection);
    }

    [Fact]
    public void ResetWithBullishBodyStartsNewBearishCorrectionFromObservedFloor()
    {
        var result = Evaluate(Candle(90, 100, 79, 95), floor: 80, priorLh: 105);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection, result.TransitionKind);
        Assert.Equal((80m, 79m), (result.PreviousFloor, result.ResultingFloor));
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.True(result.DoesCurrentCandleStartNewBearishCorrection);
    }

    [Theory]
    [InlineData(90, 90)]
    [InlineData(95, 90)]
    public void ResetWithNeutralOrBearishBodyAwaitsNewCorrectionStart(decimal open, decimal close)
    {
        var result = Evaluate(Candle(open, 100, 79, close), floor: 80, priorLh: 105);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart, result.TransitionKind);
        Assert.Equal(79m, result.ResultingFloor);
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBearishCorrection);
    }

    [Fact]
    public void InvalidationWithoutResetTerminatesBearishStructure()
    {
        var result = Evaluate(Candle(100, 111, 90, 106), floor: 80, priorLh: 105);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure, result.TransitionKind);
        Assert.Equal((80m, 80m), (result.PreviousFloor, result.ResultingFloor));
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBearishCorrection);
    }

    [Theory]
    [InlineData(100, 106)]
    [InlineData(106, 106)]
    [InlineData(110, 106)]
    public void CollisionInvalidatesForEveryExactBodyDirection(decimal open, decimal close)
    {
        var observation = Observe(Candle(open, 111, 79, close), floor: 80, priorLh: 105);

        Assert.True(observation.WasNewLowResetObserved);
        Assert.True(observation.WasPriorLhInvalidationObserved);

        var result = transitionCalculator.Evaluate(observation);

        Assert.Equal(BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure, result.TransitionKind);
        Assert.NotEqual(BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection, result.TransitionKind);
        Assert.Equal((80m, 79m), (result.PreviousFloor, result.ResultingFloor));
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBearishCorrection);
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var observation = Observe(Candle(100, 111, 79, 106), floor: 80, priorLh: 105);

        var first = transitionCalculator.Evaluate(observation);
        var second = transitionCalculator.Evaluate(observation);

        Assert.Equal(first, second);
    }

    [Fact]
    public void NullObservationFollowsRepositoryValidationConventions() =>
        Assert.Throws<ArgumentNullException>(() => transitionCalculator.Evaluate(null!));

    [Fact]
    public void ResultRejectsUnsupportedTransitionKind()
    {
        var constructor = ResultConstructor();

        var exception = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([(BearishCorrectionTerminalTransitionKind)99, 80m, 80m]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void ResultRejectsFloorIncrease()
    {
        var constructor = ResultConstructor();

        var exception = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure, 80m, 81m]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void ResultDoesNotExposeValidatedStructureStopLossOrWorkflowOutcome()
    {
        var properties = typeof(BearishCorrectionTerminalTransitionResult)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain("ValidatedLl", properties);
        Assert.DoesNotContain("ValidatedHl", properties);
        Assert.DoesNotContain("StructuralLow", properties);
        Assert.DoesNotContain("StopLoss", properties);
        Assert.DoesNotContain("RuleEvaluation", properties);
        Assert.DoesNotContain("StrategyVerdict", properties);
    }

    private BearishCorrectionTerminalTransitionResult Evaluate(
        Candle candle,
        decimal floor,
        decimal priorLh) =>
        transitionCalculator.Evaluate(Observe(candle, floor, priorLh));

    private BearishCorrectionTerminalObservationResult Observe(
        Candle candle,
        decimal floor,
        decimal priorLh)
    {
        var context = ContextAt(candle.CloseTimeUtc, Series(candle));
        return observationCalculator.EvaluateCurrentBoundary(context, candle.Timeframe, floor, priorLh);
    }

    private static ConstructorInfo ResultConstructor() =>
        typeof(BearishCorrectionTerminalTransitionResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 3);

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

    private static CandleSeries Series(params Candle[] candles) =>
        new(Provider, Symbol, candles[0].Timeframe, candles);

    private static Candle Candle(decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start, Start.AddMinutes(1), open, high, low, close, null);

    private static readonly StrategyDefinition Definition = new(
        new("synthetic"),
        new("v1"),
        "Synthetic",
        "test",
        [new(new("R-1"), "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
}
