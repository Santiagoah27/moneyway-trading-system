using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed immutable evidence outcomes; only Unique exposes an authoritative membership.</summary>
public abstract class NasdaqHumanPostCompletionActiveExtremeObservationSelection
{
    private NasdaqHumanPostCompletionActiveExtremeObservationSelection() { }

    public sealed class Missing : NasdaqHumanPostCompletionActiveExtremeObservationSelection
    {
        internal Missing() { }
    }

    public sealed class Unique : NasdaqHumanPostCompletionActiveExtremeObservationSelection
    {
        internal Unique(IEnumerable<DateTimeOffset> members, IEnumerable<NasdaqHumanPostCompletionActiveExtremeObservation> supporting)
        {
            SemanticMemberOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(members.ToArray());
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanPostCompletionActiveExtremeObservation>(supporting.ToArray());
        }

        public IReadOnlyList<DateTimeOffset> SemanticMemberOpenTimesUtc { get; }
        public IReadOnlyList<NasdaqHumanPostCompletionActiveExtremeObservation> SupportingObservations { get; }
    }

    public sealed class Conflict : NasdaqHumanPostCompletionActiveExtremeObservationSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanPostCompletionActiveExtremeObservation> supporting)
        {
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanPostCompletionActiveExtremeObservation>(supporting.ToArray());
        }

        public IReadOnlyList<NasdaqHumanPostCompletionActiveExtremeObservation> SupportingObservations { get; }
    }
}
