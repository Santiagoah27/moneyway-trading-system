using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class CorrectionStartTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("provider");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly CorrectionBoundaryObservationCalculator observationCalculator = new();
    private readonly CorrectionStartTransitionCalculator calculator = new();

    [Theory]
    [InlineData(100, 110, false)]
    [InlineData(100, 100, false)]
    [InlineData(110, 100, true)]
    public void CeilingStartsOnlyOnFirstBearishBody(int open, int close, bool starts)
    {
        var candle = Candle(0, open, 120, 90, close);

        AssertTransition(Evaluate(120m, candle, CorrectionOriginExtremeSide.Ceiling), candle, starts, 120m, false);
    }

    [Theory]
    [InlineData(110, 100, false)]
    [InlineData(100, 100, false)]
    [InlineData(100, 110, true)]
    public void FloorStartsOnlyOnFirstBullishBody(int open, int close, bool starts)
    {
        var candle = Candle(0, open, 120, 90, close);

        AssertTransition(Evaluate(90m, candle, CorrectionOriginExtremeSide.Floor), candle, starts, 90m, false);
    }

    [Theory]
    [InlineData(100, 110)]
    [InlineData(100, 100)]
    public void CeilingExtensionWhileWaitingUpdatesExtremeWithoutStarting(int open, int close)
    {
        var candle = Candle(0, open, 121, 90, close);

        AssertTransition(Evaluate(120m, candle, CorrectionOriginExtremeSide.Ceiling), candle, false, 121m, true);
    }

    [Theory]
    [InlineData(110, 100)]
    [InlineData(100, 100)]
    public void FloorExtensionWhileWaitingUpdatesExtremeWithoutStarting(int open, int close)
    {
        var candle = Candle(0, open, 120, 89, close);

        AssertTransition(Evaluate(90m, candle, CorrectionOriginExtremeSide.Floor), candle, false, 89m, true);
    }

    [Fact]
    public void EqualExtremesDoNotCountAsExtensionsOnEitherSide()
    {
        var ceilingCandle = Candle(0, 100, 120, 90, 110);
        var floorCandle = Candle(0, 110, 120, 90, 100);

        AssertTransition(Evaluate(120m, ceilingCandle, CorrectionOriginExtremeSide.Ceiling), ceilingCandle, false, 120m, false);
        AssertTransition(Evaluate(90m, floorCandle, CorrectionOriginExtremeSide.Floor), floorCandle, false, 90m, false);
    }

    [Fact]
    public void SimultaneousExtensionAndOppositeBodyStartsFromUpdatedExtreme()
    {
        var ceilingCandle = Candle(0, 110, 121, 90, 100);
        var floorCandle = Candle(0, 100, 120, 89, 110);

        AssertTransition(Evaluate(120m, ceilingCandle, CorrectionOriginExtremeSide.Ceiling), ceilingCandle, true, 121m, true);
        AssertTransition(Evaluate(90m, floorCandle, CorrectionOriginExtremeSide.Floor), floorCandle, true, 89m, true);
    }

    [Fact]
    public void ExactDojiNeverStartsOnEitherSideEvenWithNewExtreme()
    {
        var candle = Candle(0, 100, 121, 89, 100);

        AssertTransition(Evaluate(120m, candle, CorrectionOriginExtremeSide.Ceiling), candle, false, 121m, true);
        AssertTransition(Evaluate(90m, candle, CorrectionOriginExtremeSide.Floor), candle, false, 89m, true);
    }

    [Fact]
    public void SmallestNonzeroDecimalBodyStartsWithoutSizeThreshold()
    {
        var ceilingCandle = Candle(0, 1m, 2m, 0m, 0.9999999999999999999999999999m);
        var floorCandle = Candle(0, 1m, 2m, 0m, 1.0000000000000000000000000001m);

        AssertTransition(Evaluate(2m, ceilingCandle, CorrectionOriginExtremeSide.Ceiling), ceilingCandle, true, 2m, false);
        AssertTransition(Evaluate(0m, floorCandle, CorrectionOriginExtremeSide.Floor), floorCandle, true, 0m, false);
    }

    [Fact]
    public void RepeatedIdenticalInputReturnsEqualTransition()
    {
        var candle = Candle(0, 110, 121, 90, 100);
        var observation = observationCalculator.Evaluate(120m, candle, CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal(calculator.Evaluate(observation, candle), calculator.Evaluate(observation, candle));
    }

    [Theory]
    [InlineData(CorrectionOriginExtremeSide.Ceiling, 120, 100, 110, 110, 100)]
    [InlineData(CorrectionOriginExtremeSide.Floor, 90, 110, 100, 100, 110)]
    public void WaitingHistoryIsExcludedAndOnlyCurrentStartCandleIsIdentified(
        CorrectionOriginExtremeSide side,
        int extreme,
        int waitingOpen,
        int waitingClose,
        int startingOpen,
        int startingClose)
    {
        var waitingCandles = new List<Candle>
        {
            Candle(0, waitingOpen, 120, 90, waitingClose),
            Candle(1, 100, 120, 90, 100),
        };
        var originalWaiting = waitingCandles.ToArray();
        var currentExtreme = (decimal)extreme;

        foreach (var waitingCandle in waitingCandles)
        {
            var waiting = Evaluate(currentExtreme, waitingCandle, side);
            Assert.Null(waiting.FirstTurnCandle);
            Assert.Equal(CorrectionTurnCurrentCandleMembership.NoCorrectionTurn, waiting.CurrentCandleMembership);
            currentExtreme = waiting.ResultingExtreme;
        }

        var startingCandle = Candle(2, startingOpen, 120, 90, startingClose);
        var started = Evaluate(currentExtreme, startingCandle, side);

        Assert.Equal(originalWaiting, waitingCandles);
        Assert.Same(startingCandle, started.FirstTurnCandle);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NewTurn, started.CurrentCandleMembership);
    }

    [Theory]
    [InlineData(CorrectionOriginExtremeSide.Ceiling, 120, 100, 110, 110, 100)]
    [InlineData(CorrectionOriginExtremeSide.Floor, 90, 110, 100, 100, 110)]
    public void NormalAndPostResetWaitingUseIdenticalTransition(
        CorrectionOriginExtremeSide side,
        int extreme,
        int waitingOpen,
        int waitingClose,
        int startingOpen,
        int startingClose)
    {
        var waitingCandle = Candle(0, waitingOpen, 120, 90, waitingClose);
        var startingCandle = Candle(1, startingOpen, 120, 90, startingClose);

        // The calculator has no history or reset flag: both sources of waiting use these same observations.
        var normalWaiting = Evaluate(extreme, waitingCandle, side);
        var postResetWaiting = Evaluate(extreme, waitingCandle, side);
        var normalStart = Evaluate(normalWaiting.ResultingExtreme, startingCandle, side);
        var postResetStart = Evaluate(postResetWaiting.ResultingExtreme, startingCandle, side);

        Assert.Equal(normalWaiting, postResetWaiting);
        Assert.Equal(normalStart, postResetStart);
        Assert.Equal(CorrectionStartTransitionKind.AwaitCorrectionStart, normalWaiting.TransitionKind);
        Assert.Equal(CorrectionStartTransitionKind.StartCorrection, normalStart.TransitionKind);
    }

    [Fact]
    public void NullInputsAreRejected()
    {
        var candle = Candle(0, 110, 120, 90, 100);
        var observation = observationCalculator.Evaluate(120m, candle, CorrectionOriginExtremeSide.Ceiling);

        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!, candle));
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(observation, null!));
    }

    private CorrectionStartTransitionResult Evaluate(decimal extreme, Candle candle, CorrectionOriginExtremeSide side) =>
        calculator.Evaluate(observationCalculator.Evaluate(extreme, candle, side), candle);

    private static void AssertTransition(
        CorrectionStartTransitionResult result,
        Candle candle,
        bool starts,
        decimal resultingExtreme,
        bool wasExtremeUpdated)
    {
        Assert.Equal(starts ? CorrectionStartTransitionKind.StartCorrection : CorrectionStartTransitionKind.AwaitCorrectionStart, result.TransitionKind);
        Assert.Equal(starts ? CorrectionTurnCurrentCandleMembership.NewTurn : CorrectionTurnCurrentCandleMembership.NoCorrectionTurn, result.CurrentCandleMembership);
        Assert.Equal(resultingExtreme, result.ResultingExtreme);
        Assert.Equal(wasExtremeUpdated, result.WasExtremeUpdated);
        if (starts) Assert.Same(candle, result.FirstTurnCandle);
        else Assert.Null(result.FirstTurnCandle);
    }

    private static Candle Candle(int minute, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start.AddMinutes(minute), Start.AddMinutes(minute + 1), open, high, low, close, null);
}
