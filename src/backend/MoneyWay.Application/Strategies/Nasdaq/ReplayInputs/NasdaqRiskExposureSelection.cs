using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Documented exposure selection, with no runtime risk decision.</summary>
public abstract class NasdaqRiskExposureSelection
{
    private NasdaqRiskExposureSelection(IEnumerable<NasdaqRiskExposureObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqRiskExposureObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqRiskExposureObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqRiskExposureSelection
    {
        internal Missing(IEnumerable<NasdaqRiskExposureObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqRiskExposureSelection
    {
        internal Unique(IEnumerable<NasdaqRiskExposureObservation> support,
            IEnumerable<NasdaqRiskExposureObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqRiskExposureObservation>(support.ToArray());
        public IReadOnlyList<NasdaqRiskExposureObservation> SupportingObservations { get; }
        public NasdaqRiskExposureObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqRiskExposureSelection
    {
        internal Conflict(IEnumerable<NasdaqRiskExposureObservation> alternatives,
            IEnumerable<NasdaqRiskExposureObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqRiskExposureObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqRiskExposureObservation> Alternatives { get; }
    }
}
