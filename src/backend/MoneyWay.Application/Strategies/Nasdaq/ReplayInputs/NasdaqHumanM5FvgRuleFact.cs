using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact canonical candidate existence; establishes no quality or entry.</summary>
public sealed record NasdaqHumanM5FvgRuleFact : IReplayRuleFact
{
    internal NasdaqHumanM5FvgRuleFact(NasdaqHumanM5TriggerRuleFact trigger, NasdaqHumanM5FvgSelection.Unique selection)
    { Trigger = trigger; Selection = selection; }
    public NasdaqHumanM5TriggerRuleFact Trigger { get; }
    public NasdaqHumanM5FvgSelection.Unique Selection { get; }
}
