using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed evidence selection; Conflict exposes support but never a winning fact or rule result.</summary>
public abstract class NasdaqHumanH4ContextSelection
{
    private NasdaqHumanH4ContextSelection() { }

    public sealed class Missing : NasdaqHumanH4ContextSelection
    {
        internal Missing() { }
    }

    public sealed class Unique : NasdaqHumanH4ContextSelection
    {
        internal Unique(NasdaqHumanH4ContextFact fact, IEnumerable<NasdaqHumanH4ContextObservation> supporting)
        {
            Fact = fact;
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanH4ContextObservation>(supporting.ToArray());
        }

        public NasdaqHumanH4ContextFact Fact { get; }
        public IReadOnlyList<NasdaqHumanH4ContextObservation> SupportingObservations { get; }
    }

    public sealed class Conflict : NasdaqHumanH4ContextSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanH4ContextObservation> supporting)
        {
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanH4ContextObservation>(supporting.ToArray());
        }

        public IReadOnlyList<NasdaqHumanH4ContextObservation> SupportingObservations { get; }
    }
}
