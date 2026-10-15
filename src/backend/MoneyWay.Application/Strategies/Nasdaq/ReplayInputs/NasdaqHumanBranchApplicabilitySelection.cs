using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence only. No selection result changes rule applicability or grants progression.</summary>
public abstract class NasdaqHumanBranchApplicabilitySelection
{
    private NasdaqHumanBranchApplicabilitySelection() { }

    public sealed class Missing : NasdaqHumanBranchApplicabilitySelection
    {
        internal Missing() { }
    }

    public sealed class Unique : NasdaqHumanBranchApplicabilitySelection
    {
        internal Unique(IEnumerable<NasdaqHumanBranchApplicabilityObservation> support) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanBranchApplicabilityObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanBranchApplicabilityObservation> SupportingObservations { get; }
        public NasdaqHumanBranchApplicabilityObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHumanBranchApplicabilitySelection
    {
        internal Conflict(IEnumerable<NasdaqHumanBranchApplicabilityObservation> alternatives) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanBranchApplicabilityObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanBranchApplicabilityObservation> Alternatives { get; }
    }
}
