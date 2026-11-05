using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence selection only. Unique establishes neither SL Passed nor trade readiness.</summary>
public abstract class NasdaqHumanWickfillScenario2StopLossSelection
{
    private NasdaqHumanWickfillScenario2StopLossSelection(IEnumerable<NasdaqHumanWickfillScenario2StopLossObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanWickfillScenario2StopLossObservation>(unavailable.ToArray());
    public IReadOnlyList<NasdaqHumanWickfillScenario2StopLossObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanWickfillScenario2StopLossSelection
    {
        internal Missing(IEnumerable<NasdaqHumanWickfillScenario2StopLossObservation> unavailable) : base(unavailable) { }
    }
    public sealed class Unique : NasdaqHumanWickfillScenario2StopLossSelection
    {
        internal Unique(IEnumerable<NasdaqHumanWickfillScenario2StopLossObservation> support,
            IEnumerable<NasdaqHumanWickfillScenario2StopLossObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanWickfillScenario2StopLossObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanWickfillScenario2StopLossObservation> SupportingObservations { get; }
        public NasdaqHumanWickfillScenario2StopLossObservation Fact => SupportingObservations[0];
    }
    public sealed class Conflict : NasdaqHumanWickfillScenario2StopLossSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanWickfillScenario2StopLossObservation> alternatives,
            IEnumerable<NasdaqHumanWickfillScenario2StopLossObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanWickfillScenario2StopLossObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanWickfillScenario2StopLossObservation> Alternatives { get; }
    }
}
