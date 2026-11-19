using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Retains computed market evidence even when competing triggers prevent progression.</summary>
public sealed record NasdaqM5StrictCloseDetectionFact : IReplayRuleFact
{
    internal NasdaqM5StrictCloseDetectionFact(IEnumerable<NasdaqHumanM5TriggerObservation> observations) => Observations = Array.AsReadOnly(observations.ToArray());
    public IReadOnlyList<NasdaqHumanM5TriggerObservation> Observations { get; }
}
