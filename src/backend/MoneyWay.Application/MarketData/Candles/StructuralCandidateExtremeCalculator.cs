namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Replaces a caller-supplied candidate structural extreme using exact decimal price comparison.
/// It does not detect candidates, candles, turns, or structural coordinates.
/// </summary>
public sealed class StructuralCandidateExtremeCalculator
{
    public StructuralCandidateExtremeResult Evaluate(
        decimal currentCandidateExtreme,
        decimal observedCandidateExtreme,
        StructuralCandidateExtremeSide side)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The structural candidate extreme side is not supported.");
        }

        var resultingExtreme = side == StructuralCandidateExtremeSide.Lower
            ? decimal.Min(currentCandidateExtreme, observedCandidateExtreme)
            : decimal.Max(currentCandidateExtreme, observedCandidateExtreme);

        return new StructuralCandidateExtremeResult(currentCandidateExtreme, resultingExtreme, side);
    }
}
