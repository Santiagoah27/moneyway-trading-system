using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public sealed record NasdaqHumanH4ContextRuleFact : IReplayRuleFact
{
    internal NasdaqHumanH4ContextRuleFact(NasdaqHumanH4ContextSelection.Unique selection) { Selection = selection; }
    public NasdaqHumanH4ContextSelection.Unique Selection { get; }
}
