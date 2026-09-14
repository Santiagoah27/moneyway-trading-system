using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.StructuralBreaks;

public sealed class StructuralCandidateValidationCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FourHours = new(4, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly StructuralCandidateValidationCalculator _calculator = new();

    [Fact]
    public void EvaluateCurrentBoundary_LowerCandidateWithStrictUpperClose_IsValidated()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99m, 102m, 98m, 100.0000001m)));

        var result = Evaluate(context, 100m, LowerTurn(), StructuralCandidateExtremeSide.Lower);

        Assert.True(result.IsValidated);
        Assert.Equal(StructuralBreakDirection.Upper, result.BreakObservation.Direction);
        Assert.Equal(context.AsOfUtc, result.BreakObservation.AsOfUtc);
    }

    [Fact]
    public void EvaluateCurrentBoundary_LowerCandidateWithEqualClose_IsNotValidated()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99m, 101m, 98m, 100m)));

        var result = Evaluate(context, 100m, LowerTurn(), StructuralCandidateExtremeSide.Lower);

        Assert.False(result.IsValidated);
    }

    [Fact]
    public void EvaluateCurrentBoundary_LowerCandidateWithUpperWickOnly_IsNotValidated()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99m, 101m, 98m, 100m)));

        var result = Evaluate(context, 100m, LowerTurn(), StructuralCandidateExtremeSide.Lower);

        Assert.True(result.BreakObservation.Candle!.High > 100m);
        Assert.False(result.IsValidated);
    }

    [Fact]
    public void EvaluateCurrentBoundary_ValidatedLowerCandidate_RetainsCandidateTurnGeometryOnly()
    {
        var confirmingCandle = Candle(FourHours, Start, Start.AddHours(4), 70m, 130m, 60m, 120m);
        var candidateTurn = LowerTurn();
        var context = ContextAt(Start.AddHours(4), Series(confirmingCandle));

        var result = Evaluate(context, 100m, candidateTurn, StructuralCandidateExtremeSide.Lower);

        Assert.True(result.IsValidated);
        Assert.Equal(90m, result.StructuralPrice);
        Assert.Equal(80m, result.ProtectionAnchor);
        Assert.NotEqual(confirmingCandle.Open, result.StructuralPrice);
        Assert.NotEqual(confirmingCandle.Low, result.ProtectionAnchor);
    }

    [Fact]
    public void EvaluateCurrentBoundary_UpperCandidateWithStrictLowerClose_IsValidated()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 101m, 102m, 98m, 99.9999999m)));

        var result = Evaluate(context, 100m, UpperTurn(), StructuralCandidateExtremeSide.Upper);

        Assert.True(result.IsValidated);
        Assert.Equal(StructuralBreakDirection.Lower, result.BreakObservation.Direction);
    }

    [Fact]
    public void EvaluateCurrentBoundary_UpperCandidateWithEqualClose_IsNotValidated()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 101m, 102m, 99m, 100m)));

        var result = Evaluate(context, 100m, UpperTurn(), StructuralCandidateExtremeSide.Upper);

        Assert.False(result.IsValidated);
    }

    [Fact]
    public void EvaluateCurrentBoundary_UpperCandidateWithLowerWickOnly_IsNotValidated()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 101m, 102m, 99m, 100m)));

        var result = Evaluate(context, 100m, UpperTurn(), StructuralCandidateExtremeSide.Upper);

        Assert.True(result.BreakObservation.Candle!.Low < 100m);
        Assert.False(result.IsValidated);
    }

    [Fact]
    public void EvaluateCurrentBoundary_ValidatedUpperCandidate_RetainsCandidateTurnGeometryOnly()
    {
        var confirmingCandle = Candle(FourHours, Start, Start.AddHours(4), 130m, 140m, 60m, 80m);
        var candidateTurn = UpperTurn();
        var context = ContextAt(Start.AddHours(4), Series(confirmingCandle));

        var result = Evaluate(context, 100m, candidateTurn, StructuralCandidateExtremeSide.Upper);

        Assert.True(result.IsValidated);
        Assert.Equal(110m, result.StructuralPrice);
        Assert.Equal(120m, result.ProtectionAnchor);
        Assert.NotEqual(confirmingCandle.Open, result.StructuralPrice);
        Assert.NotEqual(confirmingCandle.High, result.ProtectionAnchor);
    }

    [Fact]
    public void EvaluateCurrentBoundary_BeforeCausalClose_RemainsUnvalidated()
    {
        var context = ContextAt(
            Start.AddMinutes(1),
            Series(Candle(Minute, Start, Start.AddMinutes(1), 99m, 100m, 98m, 99m)),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99m, 102m, 98m, 101m)));

        var result = Evaluate(context, 100m, LowerTurn(), StructuralCandidateExtremeSide.Lower);

        Assert.False(result.IsValidated);
        Assert.Null(result.BreakObservation.Candle);
        Assert.Equal(Start.AddMinutes(1), result.BreakObservation.AsOfUtc);
    }

    [Fact]
    public void EvaluateCurrentBoundary_FutureCandleCannotChangeEarlierEvaluation()
    {
        var current = Candle(Minute, Start, Start.AddMinutes(1), 99m, 100m, 98m, 99m);
        var future = Candle(Minute, Start.AddMinutes(1), Start.AddMinutes(2), 99m, 102m, 98m, 101m);
        var withoutFuture = ContextAt(Start.AddMinutes(1), Series(current));
        var withFuture = ContextAt(Start.AddMinutes(1), Series(current, future));

        var expected = _calculator.EvaluateCurrentBoundary(
            withoutFuture,
            Minute,
            100m,
            LowerTurn(),
            StructuralCandidateExtremeSide.Lower);
        var actual = _calculator.EvaluateCurrentBoundary(
            withFuture,
            Minute,
            100m,
            LowerTurn(),
            StructuralCandidateExtremeSide.Lower);

        Assert.False(actual.IsValidated);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EvaluateCurrentBoundary_ConfirmingCandleIsNotIncludedInCandidateGeometry()
    {
        var confirmingCandle = Candle(FourHours, Start, Start.AddHours(4), 70m, 130m, 60m, 120m);
        var context = ContextAt(Start.AddHours(4), Series(confirmingCandle));

        var result = Evaluate(context, 100m, LowerTurn(), StructuralCandidateExtremeSide.Lower);

        Assert.DoesNotContain(confirmingCandle, LowerTurn());
        Assert.Equal(90m, result.StructuralPrice);
        Assert.Equal(80m, result.ProtectionAnchor);
    }

    [Fact]
    public void EvaluateCurrentBoundary_RepeatedEvaluation_IsDeterministic()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99m, 102m, 98m, 101m)));
        var candidateTurn = LowerTurn();

        var first = Evaluate(context, 100m, candidateTurn, StructuralCandidateExtremeSide.Lower);
        var second = Evaluate(context, 100m, candidateTurn, StructuralCandidateExtremeSide.Lower);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EvaluateCurrentBoundary_InvalidInputs_FollowRepositoryConventions()
    {
        var context = ContextAt(
            Start.AddHours(4),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99m, 102m, 98m, 101m)));

        Assert.Throws<ArgumentNullException>(() => _calculator.EvaluateCurrentBoundary(
            null!, FourHours, 100m, LowerTurn(), StructuralCandidateExtremeSide.Lower));
        Assert.Throws<ArgumentNullException>(() => _calculator.EvaluateCurrentBoundary(
            context, null!, 100m, LowerTurn(), StructuralCandidateExtremeSide.Lower));
        Assert.Throws<ArgumentNullException>(() => _calculator.EvaluateCurrentBoundary(
            context, FourHours, 100m, null!, StructuralCandidateExtremeSide.Lower));
        Assert.Throws<ArgumentException>(() => _calculator.EvaluateCurrentBoundary(
            context, FourHours, 100m, [], StructuralCandidateExtremeSide.Lower));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.EvaluateCurrentBoundary(
            context, FourHours, 100m, LowerTurn(), (StructuralCandidateExtremeSide)99));
    }

    private StructuralCandidateValidationResult Evaluate(
        StrategyReplayContext context,
        decimal referenceLevel,
        IReadOnlyList<Candle> candidateTurn,
        StructuralCandidateExtremeSide side) =>
        _calculator.EvaluateCurrentBoundary(context, FourHours, referenceLevel, candidateTurn, side);

    private static IReadOnlyList<Candle> LowerTurn() =>
        [Candle(Minute, Start.AddHours(-2), Start.AddHours(-2).AddMinutes(1), 90m, 100m, 80m, 95m)];

    private static IReadOnlyList<Candle> UpperTurn() =>
        [Candle(Minute, Start.AddHours(-2), Start.AddHours(-2).AddMinutes(1), 110m, 120m, 100m, 105m)];

    private static StrategyReplayContext ContextAt(DateTimeOffset asOfUtc, params CandleSeries[] series)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        var factory = new CreateStrategyReplayContextUseCase();
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc)
            {
                return factory.Execute(Definition, frame);
            }
        }

        throw new InvalidOperationException("Requested replay boundary was not produced.");
    }

    private static CandleSeries Series(params Candle[] candles) =>
        new(Provider, Symbol, candles[0].Timeframe, candles);

    private static Candle Candle(
        Timeframe timeframe,
        DateTimeOffset open,
        DateTimeOffset close,
        decimal openPrice,
        decimal high,
        decimal low,
        decimal closePrice) =>
        new(Provider, Symbol, timeframe, open, close, openPrice, high, low, closePrice, null);

    private static readonly StrategyDefinition Definition = new(
        new("synthetic"),
        new("v1"),
        "Synthetic",
        "test",
        [new(new("R-1"), "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
}
