using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical selected pullback for one exact M5 trigger with optional approved FVG support; establishes no realignment or entry.</summary>
public sealed record NasdaqHumanM1CorrectiveRetracementRuleFact : IReplayRuleFact
{
    internal NasdaqHumanM1CorrectiveRetracementRuleFact(NasdaqHumanM5TriggerRuleFact trigger, NasdaqHumanM5FvgQualityRuleFact? approvedQuality,
        NasdaqHumanM1CorrectiveRetracementSelection.Unique selection)
    {
        Trigger = trigger;
        ApprovedQuality = approvedQuality;
        Selection = selection;
    }

    public NasdaqHumanM5TriggerRuleFact Trigger { get; }
    public bool HasFvgConfirmation => ApprovedQuality is not null;
    public NasdaqHumanM5FvgQualityRuleFact? ApprovedQuality { get; }
    public NasdaqHumanM1CorrectiveRetracementSelection.Unique Selection { get; }
}
