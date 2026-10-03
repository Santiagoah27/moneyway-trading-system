using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-selected H4 member identities for one post-completion rebuild; no market members or geometry are resolved.</summary>
public sealed class NasdaqHumanPostCompletionRebuiltCandidateObservation : IStrategyReplayInputObservation,
    IEquatable<NasdaqHumanPostCompletionRebuiltCandidateObservation>
{
    public NasdaqHumanPostCompletionRebuiltCandidateObservation(NasdaqPostCompletionRebuildPendingState pendingState,
        IEnumerable<DateTimeOffset> selectedMemberOpenTimesUtc, DateTimeOffset observedAtUtc, string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(pendingState);
        ArgumentNullException.ThrowIfNull(selectedMemberOpenTimesUtc);
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));
        var members = selectedMemberOpenTimesUtc.ToArray();
        if (members.Length == 0 || members.Any(item => item.Offset != TimeSpan.Zero)
            || members.Distinct().Count() != members.Length
            || !members.Contains(pendingState.MigrationCandle.OpenTimeUtc))
            throw new ArgumentException("Selected H4 members must be non-empty, distinct UTC identities including the migration candle.", nameof(selectedMemberOpenTimesUtc));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < pendingState.MigrationCandle.CloseTimeUtc
            || observedAtUtc < members.Max().AddHours(4))
            throw new ArgumentException("Observation must be UTC and cannot precede the migration or selected H4 member closes.", nameof(observedAtUtc));
        Context = pendingState.EvidenceContext;
        SelectedMemberOpenTimesUtc = Array.AsReadOnly(members.OrderBy(item => item).ToArray());
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqPostCompletionRebuildContext Context { get; }
    public StrategyId StrategyId => Context.Episode.PreviousCompletedEpisode.StrategyId;
    public StrategyVersion StrategyVersion => Context.Episode.PreviousCompletedEpisode.StrategyVersion;
    public MarketDataProviderId ProviderId => Context.Episode.PreviousCompletedEpisode.ProviderId;
    public MarketSymbol Symbol => Context.Episode.PreviousCompletedEpisode.Symbol;
    public Timeframe Timeframe => Context.Episode.PreviousCompletedEpisode.Timeframe;
    public IReadOnlyList<DateTimeOffset> SelectedMemberOpenTimesUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }

    public bool Equals(NasdaqHumanPostCompletionRebuiltCandidateObservation? other) => other is not null
        && Context == other.Context && ObservedAtUtc == other.ObservedAtUtc && SourceReference == other.SourceReference
        && SelectedMemberOpenTimesUtc.SequenceEqual(other.SelectedMemberOpenTimesUtc);
    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanPostCompletionRebuiltCandidateObservation);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Context); hash.Add(ObservedAtUtc); hash.Add(SourceReference);
        foreach (var member in SelectedMemberOpenTimesUtc) hash.Add(member);
        return hash.ToHashCode();
    }
}
