using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence selection only; Unique is neither a verdict nor entry eligibility.</summary>
public abstract class NasdaqHumanWickfillScenario2PostSweepReversalSelection
{
    private NasdaqHumanWickfillScenario2PostSweepReversalSelection(IEnumerable<NasdaqHumanWickfillScenario2PostSweepReversalObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanWickfillScenario2PostSweepReversalObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqHumanWickfillScenario2PostSweepReversalObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanWickfillScenario2PostSweepReversalSelection
    {
        internal Missing(IEnumerable<NasdaqHumanWickfillScenario2PostSweepReversalObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqHumanWickfillScenario2PostSweepReversalSelection
    {
        internal Unique(IEnumerable<NasdaqHumanWickfillScenario2PostSweepReversalObservation> support,
            IEnumerable<NasdaqHumanWickfillScenario2PostSweepReversalObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanWickfillScenario2PostSweepReversalObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanWickfillScenario2PostSweepReversalObservation> SupportingObservations { get; }
        public NasdaqHumanWickfillScenario2PostSweepReversalObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHumanWickfillScenario2PostSweepReversalSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanWickfillScenario2PostSweepReversalObservation> alternatives,
            IEnumerable<NasdaqHumanWickfillScenario2PostSweepReversalObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanWickfillScenario2PostSweepReversalObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanWickfillScenario2PostSweepReversalObservation> Alternatives { get; }
    }
}
