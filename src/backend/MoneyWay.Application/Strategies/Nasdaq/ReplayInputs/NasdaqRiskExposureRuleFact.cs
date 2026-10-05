using System.Numerics;
using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Auditable ratio for documented exposure; neither sizing nor execution is established.</summary>
public sealed record NasdaqRiskExposureRuleFact : IReplayRuleFact
{
    public const decimal MaximumRiskRatio = 0.01m;

    internal NasdaqRiskExposureRuleFact(NasdaqRiskExposureSelection.Unique selection)
    {
        Selection = selection;
        // Decimal division is an audit projection. Compare exact supplied decimals without rounding the boundary.
        var exposure = selection.Fact.Exposure;
        try { RiskRatio = exposure.ReportedMaximumLoss / exposure.AccountBasisAmount; }
        catch (OverflowException) { RiskRatio = null; }
        var (loss, lossScale) = Exact(exposure.ReportedMaximumLoss);
        var (basis, basisScale) = Exact(exposure.AccountBasisAmount);
        IsWithinLimit = loss * 100 * BigInteger.Pow(10, basisScale) <= basis * BigInteger.Pow(10, lossScale);
    }

    public NasdaqRiskExposureSelection.Unique Selection { get; }
    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility => Selection.Fact.PreEntryEligibility;
    public NasdaqHumanStructuralStopLossRuleFact StopLoss => Selection.Fact.StopLoss;
    public NasdaqRiskExposure Exposure => Selection.Fact.Exposure;
    /// <summary>Decimal audit projection; null only when the exact ratio exceeds the decimal range. Exact operands remain in Exposure.</summary>
    public decimal? RiskRatio { get; }
    public bool RatioExceedsDecimalRange => RiskRatio is null;
    public decimal Limit => MaximumRiskRatio;
    public bool IsWithinLimit { get; }
    public DateTimeOffset EffectiveAtUtc => Selection.Fact.EffectiveAtUtc;

    private static (BigInteger Value, int Scale) Exact(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (new BigInteger((uint)bits[0]) + (new BigInteger((uint)bits[1]) << 32)
            + (new BigInteger((uint)bits[2]) << 64), (bits[3] >> 16) & 0xff);
    }
}
