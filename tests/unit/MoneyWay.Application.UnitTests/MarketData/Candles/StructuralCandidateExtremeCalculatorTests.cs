using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class StructuralCandidateExtremeCalculatorTests
{
    private readonly StructuralCandidateExtremeCalculator _calculator = new();

    [Fact]
    public void Evaluate_LowerWithLowerObservedPrice_ReplacesCandidate()
    {
        var result = _calculator.Evaluate(100m, 90m, StructuralCandidateExtremeSide.Lower);

        Assert.Equal(100m, result.PreviousExtreme);
        Assert.Equal(90m, result.ResultingExtreme);
        Assert.Equal(StructuralCandidateExtremeSide.Lower, result.Side);
        Assert.True(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_LowerWithHigherObservedPrice_PreservesCandidate()
    {
        var result = _calculator.Evaluate(100m, 110m, StructuralCandidateExtremeSide.Lower);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_LowerWithEqualObservedPrice_PreservesCandidate()
    {
        var result = _calculator.Evaluate(100m, 100m, StructuralCandidateExtremeSide.Lower);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_UpperWithHigherObservedPrice_ReplacesCandidate()
    {
        var result = _calculator.Evaluate(100m, 110m, StructuralCandidateExtremeSide.Upper);

        Assert.Equal(100m, result.PreviousExtreme);
        Assert.Equal(110m, result.ResultingExtreme);
        Assert.Equal(StructuralCandidateExtremeSide.Upper, result.Side);
        Assert.True(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_UpperWithLowerObservedPrice_PreservesCandidate()
    {
        var result = _calculator.Evaluate(100m, 90m, StructuralCandidateExtremeSide.Upper);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_UpperWithEqualObservedPrice_PreservesCandidate()
    {
        var result = _calculator.Evaluate(100m, 100m, StructuralCandidateExtremeSide.Upper);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_MinimumPositiveDecimalDifference_ReplacesUpperCandidateWithoutTolerance()
    {
        var result = _calculator.Evaluate(100m, 100.0000001m, StructuralCandidateExtremeSide.Upper);

        Assert.Equal(100.0000001m, result.ResultingExtreme);
        Assert.True(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_MinimumNegativeDecimalDifference_ReplacesLowerCandidateWithoutTolerance()
    {
        var result = _calculator.Evaluate(100m, 99.9999999m, StructuralCandidateExtremeSide.Lower);

        Assert.Equal(99.9999999m, result.ResultingExtreme);
        Assert.True(result.WasReplaced);
    }

    [Fact]
    public void Evaluate_RepeatedEvaluation_ReturnsIdenticalResult()
    {
        var first = _calculator.Evaluate(100m, 90m, StructuralCandidateExtremeSide.Lower);
        var second = _calculator.Evaluate(100m, 90m, StructuralCandidateExtremeSide.Lower);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Evaluate_UnsupportedSide_UsesInputValidationConventions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Evaluate(
            100m,
            90m,
            (StructuralCandidateExtremeSide)99));
    }

    [Fact]
    public void Result_DoesNotExposeCandleIdentityOrTieBreakSemantics()
    {
        var propertyNames = typeof(StructuralCandidateExtremeResult)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain("Candle", propertyNames);
        Assert.DoesNotContain("Timestamp", propertyNames);
        Assert.DoesNotContain("Sequence", propertyNames);
    }
}
