using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence resolution only. Unique is not a strategy verdict or entry eligibility.</summary>
public abstract class NasdaqHumanWickfillScenario2ContextSelection
{
    private NasdaqHumanWickfillScenario2ContextSelection(IEnumerable<NasdaqHumanWickfillScenario2ContextObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanWickfillScenario2ContextObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqHumanWickfillScenario2ContextObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanWickfillScenario2ContextSelection
    {
        internal Missing(IEnumerable<NasdaqHumanWickfillScenario2ContextObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqHumanWickfillScenario2ContextSelection
    {
        internal Unique(IEnumerable<NasdaqHumanWickfillScenario2ContextObservation> support,
            IEnumerable<NasdaqHumanWickfillScenario2ContextObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanWickfillScenario2ContextObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanWickfillScenario2ContextObservation> SupportingObservations { get; }
        public NasdaqHumanWickfillScenario2ContextObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHumanWickfillScenario2ContextSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanWickfillScenario2ContextObservation> alternatives,
            IEnumerable<NasdaqHumanWickfillScenario2ContextObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanWickfillScenario2ContextObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanWickfillScenario2ContextObservation> Alternatives { get; }
    }
}
