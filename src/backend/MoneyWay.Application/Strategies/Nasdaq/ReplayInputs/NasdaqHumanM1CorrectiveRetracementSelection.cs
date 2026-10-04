using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public abstract class NasdaqHumanM1CorrectiveRetracementSelection
{
    private NasdaqHumanM1CorrectiveRetracementSelection(IEnumerable<NasdaqHumanM1CorrectiveRetracementObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanM1CorrectiveRetracementObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqHumanM1CorrectiveRetracementObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanM1CorrectiveRetracementSelection
    {
        internal Missing(IEnumerable<NasdaqHumanM1CorrectiveRetracementObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqHumanM1CorrectiveRetracementSelection
    {
        internal Unique(IEnumerable<NasdaqHumanM1CorrectiveRetracementObservation> support,
            IEnumerable<NasdaqHumanM1CorrectiveRetracementObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanM1CorrectiveRetracementObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM1CorrectiveRetracementObservation> SupportingObservations { get; }
        public NasdaqHumanM1CorrectiveRetracementObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHumanM1CorrectiveRetracementSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanM1CorrectiveRetracementObservation> alternatives,
            IEnumerable<NasdaqHumanM1CorrectiveRetracementObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanM1CorrectiveRetracementObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanM1CorrectiveRetracementObservation> Alternatives { get; }
    }
}
