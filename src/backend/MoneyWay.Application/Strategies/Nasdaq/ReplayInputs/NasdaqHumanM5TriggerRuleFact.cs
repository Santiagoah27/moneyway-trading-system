using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Owning-rule canonical first trigger output; establishes no later FVG, signal or entry.</summary>
public sealed record NasdaqHumanM5TriggerRuleFact : IReplayRuleFact
{
    internal NasdaqHumanM5TriggerRuleFact(NasdaqHumanM5TriggerSelection.Unique selection) => Selection = selection;
    public NasdaqHumanM5TriggerSelection.Unique Selection { get; }
}
