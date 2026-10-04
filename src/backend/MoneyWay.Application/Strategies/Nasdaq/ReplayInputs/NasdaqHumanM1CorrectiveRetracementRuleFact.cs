using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical selected pullback for one exact approved FVG; establishes no realignment or entry.</summary>
public sealed record NasdaqHumanM1CorrectiveRetracementRuleFact : IReplayRuleFact
{
    internal NasdaqHumanM1CorrectiveRetracementRuleFact(NasdaqHumanM5FvgQualityRuleFact approvedQuality,
        NasdaqHumanM1CorrectiveRetracementSelection.Unique selection)
    {
        ApprovedQuality = approvedQuality;
        Selection = selection;
    }

    public NasdaqHumanM5FvgQualityRuleFact ApprovedQuality { get; }
    public NasdaqHumanM1CorrectiveRetracementSelection.Unique Selection { get; }
}
