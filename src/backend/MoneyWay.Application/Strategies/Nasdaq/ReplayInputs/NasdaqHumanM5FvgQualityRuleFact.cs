using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical candidate-local review, retained even when rejection projects to Waiting.</summary>
public sealed record NasdaqHumanM5FvgQualityRuleFact : IReplayRuleFact
{
    internal NasdaqHumanM5FvgQualityRuleFact(NasdaqHumanM5FvgRuleFact candidate, NasdaqHumanM5FvgQualitySelection.Unique selection)
    { Candidate = candidate; Selection = selection; }
    public NasdaqHumanM5FvgRuleFact Candidate { get; }
    public NasdaqHumanM5FvgQualitySelection.Unique Selection { get; }
}
