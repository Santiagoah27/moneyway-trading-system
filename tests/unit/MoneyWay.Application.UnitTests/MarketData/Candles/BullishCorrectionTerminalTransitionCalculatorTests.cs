using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class BullishCorrectionTerminalTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly BullishCorrectionTerminalObservationCalculator observationCalculator = new();
    private readonly BullishCorrectionTerminalTransitionCalculator transitionCalculator = new();

    [Theory]
    [InlineData(110, 100)]
    [InlineData(100, 110)]
    [InlineData(100, 100)]
    public void NoTerminalEventContinuesExistingCorrectionRegardlessOfBodyDirection(decimal open, decimal close)
    {
        var result = Evaluate(Candle(open, 119, 95, close), ceiling: 120, priorHl: 90);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.ContinueExistingCorrection, result.TransitionKind);
        Assert.Equal((120m, 120m), (result.PreviousCeiling, result.ResultingCeiling));
        Assert.False(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBullishCorrection);
    }

    [Fact]
    public void ResetWithBearishBodyStartsNewBullishCorrectionFromObservedCeiling()
    {
        var result = Evaluate(Candle(115, 121, 100, 110), ceiling: 120, priorHl: 90);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection, result.TransitionKind);
        Assert.Equal((120m, 121m), (result.PreviousCeiling, result.ResultingCeiling));
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.True(result.DoesCurrentCandleStartNewBullishCorrection);
    }

    [Theory]
    [InlineData(110, 110)]
    [InlineData(100, 110)]
    public void ResetWithNeutralOrBullishBodyAwaitsNewCorrectionStart(decimal open, decimal close)
    {
        var result = Evaluate(Candle(open, 121, 95, close), ceiling: 120, priorHl: 90);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart, result.TransitionKind);
        Assert.Equal(121m, result.ResultingCeiling);
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBullishCorrection);
    }

    [Fact]
    public void InvalidationWithoutResetTerminatesBullishStructure()
    {
        var result = Evaluate(Candle(100, 119, 80, 89), ceiling: 120, priorHl: 90);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.InvalidateBullishStructure, result.TransitionKind);
        Assert.Equal((120m, 120m), (result.PreviousCeiling, result.ResultingCeiling));
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBullishCorrection);
    }

    [Theory]
    [InlineData(100, 89)]
    [InlineData(89, 89)]
    [InlineData(80, 89)]
    public void CollisionInvalidatesForEveryExactBodyDirection(decimal open, decimal close)
    {
        var observation = Observe(Candle(open, 121, 75, close), ceiling: 120, priorHl: 90);

        Assert.True(observation.WasNewHighResetObserved);
        Assert.True(observation.WasPriorHlInvalidationObserved);

        var result = transitionCalculator.Evaluate(observation);

        Assert.Equal(BullishCorrectionTerminalTransitionKind.InvalidateBullishStructure, result.TransitionKind);
        Assert.NotEqual(BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection, result.TransitionKind);
        Assert.Equal((120m, 121m), (result.PreviousCeiling, result.ResultingCeiling));
        Assert.True(result.WasPreviousCandidateDiscarded);
        Assert.False(result.DoesCurrentCandleStartNewBullishCorrection);
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var observation = Observe(Candle(100, 121, 80, 89), ceiling: 120, priorHl: 90);

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
        var constructor = typeof(BullishCorrectionTerminalTransitionResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 3);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([(BullishCorrectionTerminalTransitionKind)99, 120m, 120m]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void ResultDoesNotExposeValidatedStructureStopLossOrWorkflowOutcome()
    {
        var properties = typeof(BullishCorrectionTerminalTransitionResult)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain("ValidatedHh", properties);
        Assert.DoesNotContain("ValidatedLh", properties);
        Assert.DoesNotContain("StopLoss", properties);
        Assert.DoesNotContain("RuleEvaluation", properties);
        Assert.DoesNotContain("StrategyVerdict", properties);
    }

    private BullishCorrectionTerminalTransitionResult Evaluate(
        Candle candle,
        decimal ceiling,
        decimal priorHl) =>
        transitionCalculator.Evaluate(Observe(candle, ceiling, priorHl));

    private BullishCorrectionTerminalObservationResult Observe(
        Candle candle,
        decimal ceiling,
        decimal priorHl)
    {
        var context = ContextAt(candle.CloseTimeUtc, Series(candle));
        return observationCalculator.EvaluateCurrentBoundary(context, candle.Timeframe, ceiling, priorHl);
    }

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
