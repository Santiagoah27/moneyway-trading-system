using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-reviewed exact final-cluster members; neither selects authority nor derives geometry.</summary>
/// <remarks>Member timestamps inherit the event's market series. Source-candle close/horizon and contiguity
/// checks require the future resolver; AsOfUtc visibility remains owned by replay input bounding.</remarks>
public sealed class NasdaqHumanPostCompletionActiveExtremeObservation : IStrategyReplayInputObservation,
    IEquatable<NasdaqHumanPostCompletionActiveExtremeObservation>
{
    public NasdaqHumanPostCompletionActiveExtremeObservation(
        NasdaqPostCompletionActiveExtremeMembershipEvent membershipEvent,
        IEnumerable<DateTimeOffset> selectedMemberOpenTimesUtc,
        DateTimeOffset observedAtUtc,
        string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(membershipEvent);
        ArgumentNullException.ThrowIfNull(selectedMemberOpenTimesUtc);
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < membershipEvent.CorrectionStartCandle.CloseTimeUtc)
            throw new ArgumentException("Observation must be UTC and cannot precede the established turn close.", nameof(observedAtUtc));
        var members = selectedMemberOpenTimesUtc.ToArray();
        if (members.Length == 0 || members.Any(member => member.Offset != TimeSpan.Zero)
            || members.Distinct().Count() != members.Length)
            throw new ArgumentException("Members must be a non-empty set of distinct UTC H4 open timestamps.", nameof(selectedMemberOpenTimesUtc));
        MembershipEvent = membershipEvent;
        SelectedMemberOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(members.OrderBy(member => member).ToArray());
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqPostCompletionActiveExtremeMembershipEvent MembershipEvent { get; }
    public StrategyId StrategyId => MembershipEvent.Episode.PreviousCompletedEpisode.StrategyId;
    public StrategyVersion StrategyVersion => MembershipEvent.Episode.PreviousCompletedEpisode.StrategyVersion;
    public MarketDataProviderId ProviderId => MembershipEvent.Episode.PreviousCompletedEpisode.ProviderId;
    public MarketSymbol Symbol => MembershipEvent.Episode.PreviousCompletedEpisode.Symbol;
    public Timeframe Timeframe => MembershipEvent.Episode.PreviousCompletedEpisode.Timeframe;
    public IReadOnlyList<DateTimeOffset> SelectedMemberOpenTimesUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }

    public bool Equals(NasdaqHumanPostCompletionActiveExtremeObservation? other) => other is not null
        && MembershipEvent.Equals(other.MembershipEvent) && ObservedAtUtc == other.ObservedAtUtc
        && SourceReference == other.SourceReference && SelectedMemberOpenTimesUtc.SequenceEqual(other.SelectedMemberOpenTimesUtc);
    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanPostCompletionActiveExtremeObservation);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MembershipEvent); hash.Add(ObservedAtUtc); hash.Add(SourceReference);
        foreach (var member in SelectedMemberOpenTimesUtc) hash.Add(member);
        return hash.ToHashCode();
    }
}
