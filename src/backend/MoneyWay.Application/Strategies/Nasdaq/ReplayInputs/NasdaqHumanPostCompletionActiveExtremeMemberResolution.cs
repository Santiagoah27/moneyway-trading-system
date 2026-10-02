using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed domain outcomes for exact historical membership, separate from rule evaluation.</summary>
public abstract class NasdaqHumanPostCompletionActiveExtremeMemberResolution
{
    private NasdaqHumanPostCompletionActiveExtremeMemberResolution(
        NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique selection)
    {
        Selection = selection;
    }

    public NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique Selection { get; }
    public NasdaqPostCompletionActiveExtremeMembershipEvent MembershipEvent => Selection.SupportingObservations[0].MembershipEvent;

    public sealed class Resolved : NasdaqHumanPostCompletionActiveExtremeMemberResolution
    {
        internal Resolved(NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique selection, IEnumerable<Candle> members)
            : base(selection) => SelectedMembers = new ReadOnlyCollection<Candle>(members.ToArray());

        public IReadOnlyList<Candle> SelectedMembers { get; }
    }

    public sealed class DataUnavailable : NasdaqHumanPostCompletionActiveExtremeMemberResolution
    {
        internal DataUnavailable(NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique selection,
            IEnumerable<DateTimeOffset> unavailableCandleOpenTimesUtc) : base(selection) =>
            UnavailableCandleOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(unavailableCandleOpenTimesUtc.Distinct().Order().ToArray());

        /// <summary>Exact required source identities, including the turn boundary if absent.</summary>
        public IReadOnlyList<DateTimeOffset> UnavailableCandleOpenTimesUtc { get; }
    }
}
