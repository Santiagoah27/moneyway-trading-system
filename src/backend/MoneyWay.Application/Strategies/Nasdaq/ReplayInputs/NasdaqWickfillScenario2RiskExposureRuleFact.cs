using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Source-documented WF2 planned loss comparison, not a position, order or economic outcome.</summary>
public sealed record NasdaqWickfillScenario2RiskExposureRuleFact : IReplayRuleFact
{
    internal NasdaqWickfillScenario2RiskExposureRuleFact(NasdaqWickfillScenario2RiskExposureSelection selection)
    {
        Selection = selection;
        (RiskRatio, IsWithinLimit) = NasdaqRiskExposureRuleFact.Compare(Exposure);
    }
    public NasdaqWickfillScenario2RiskExposureSelection Selection { get; }
    public NasdaqHumanWickfillScenario2StopLossRuleFact StopLoss => Selection.SupportingObservations[0].StopLoss;
    public NasdaqRiskExposure Exposure => Selection.SupportingObservations[0].Exposure;
    public decimal? RiskRatio { get; }
    public bool RatioExceedsDecimalRange => RiskRatio is null;
    public bool IsWithinLimit { get; }
    public decimal Limit => NasdaqRiskExposureRuleFact.MaximumRiskRatio;
    public DateTimeOffset EffectiveAtUtc => Selection.SupportingObservations[0].EffectiveAtUtc;
}
