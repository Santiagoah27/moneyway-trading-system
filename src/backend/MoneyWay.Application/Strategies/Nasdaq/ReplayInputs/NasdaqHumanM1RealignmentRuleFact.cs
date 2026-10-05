using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical selected realignment for one exact pullback; no signal or entry is established.</summary>
public sealed record NasdaqHumanM1RealignmentRuleFact : IReplayRuleFact
{
    internal NasdaqHumanM1RealignmentRuleFact(NasdaqHumanM1CorrectiveRetracementRuleFact pullback,
        NasdaqHumanM1RealignmentSelection.Unique selection)
    {
        Pullback = pullback;
        Selection = selection;
    }

    public NasdaqHumanM1CorrectiveRetracementRuleFact Pullback { get; }
    public NasdaqHumanM1RealignmentSelection.Unique Selection { get; }
}
