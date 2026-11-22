using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public sealed record M5StructuralStopAssociation(string EpisodeId, M5SequenceConfiguration M5Configuration,
    M1RealignmentProfile M1Profile, M1ResearchAnchor Anchor, string ScanId, string? RealignmentId,
    DateTimeOffset? RealignmentConfirmedAtUtc, M1WindowPosition? WindowPosition, M5StructuralPivotProfile PivotProfile,
    string? EvaluationId, M5StructuralStopOutcome Outcome, string ReasonCode,
    IReadOnlyList<M5StructuralStopBinding> Bindings)
{
    public string? WindowConfigurationId =>
        M5Configuration.WindowOpen == NasdaqTradingWindowAlignment.Opening &&
        M5Configuration.WindowClose == NasdaqTradingWindowAlignment.Closing
            ? NasdaqTradingWindowAlignment.ConfigurationId : null;
}

public sealed record M5StructuralStopBinding(M1M5Binding Source, string? FvgStateAtRealignment, string? IfvgStateAtRealignment)
{
    public bool? ContextInvalidatedAtRealignment =>
        FvgStateAtRealignment == "Invalidated" || IfvgStateAtRealignment == "Invalidated" ? true :
        FvgStateAtRealignment is null or "UnknownDueToMissingData" || IfvgStateAtRealignment == "UnknownDueToMissingData"
            ? null : false;
}

public sealed record M5StructuralStopResearchReport(string SessionId, DateTimeOffset AsOfUtc,
    string Provider, string Instrument, string SourceProvenance, IReadOnlyList<M5StructuralPivotProfile> Profiles,
    IReadOnlyDictionary<string, M5StructuralStopObservation> Evaluations, IReadOnlyList<M5StructuralStopAssociation> Associations)
{
    public const string ResearchId = "EXP-NQ-M5-STRUCTURAL-SL-V1";
    public const string ResearchVersion = "1.0.0";
    public string CandidateId => ResearchId;
    public string Version => ResearchVersion;
    public string SelectionPolicy => "LatestConfirmedStructuralReference";
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public string ExecutableStopLossPolicy => "Unresolved";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public int DistinctEpisodes => Associations.Select(a => a.EpisodeId).Distinct(StringComparer.Ordinal).Count();
    public int DistinctM5Anchors => Associations.Select(a => a.Anchor.Id).Distinct(StringComparer.Ordinal).Count();
    public int DistinctM1RealignmentAnchors => Associations.Where(a => a.RealignmentConfirmedAtUtc is not null)
        .Select(a => (a.RealignmentConfirmedAtUtc, a.Anchor.Direction)).Distinct().Count();
    public int M5M1ConfigurationCombinations => Associations.Select(a => (a.EpisodeId, a.M5Configuration.Id, a.M1Profile))
        .Distinct().Count();
    public int StructuralConfigurationCombinations => Associations
        .Select(a => (a.EpisodeId, a.M5Configuration.Id, a.M1Profile, a.PivotProfile.Id)).Distinct().Count();
    public IReadOnlyDictionary<M5StructuralStopOutcome, int> DistinctEvaluationOutcomes => Evaluations.Values
        .GroupBy(e => e.Outcome).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
}

public sealed class CompareM5StructuralStopLossUseCase
{
    public M5StructuralStopResearchReport Execute(M1RealignmentReport realignment, CandleSeries m5,
        IReadOnlyList<M5StructuralPivotProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(realignment);
        ArgumentNullException.ThrowIfNull(m5);
        ArgumentNullException.ThrowIfNull(profiles);
        if (m5.ProviderId.Value != realignment.Provider || m5.Symbol.Value != realignment.Instrument ||
            m5.Timeframe != new Timeframe(5, TimeframeUnit.Minute) || realignment.AsOfUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Original M5 series must match the M1 realignment context.", nameof(m5));
        if (profiles.Count == 0 || profiles.Any(p => p.LeftBars <= 0 || p.RightBars <= 0) ||
            profiles.Distinct().Count() != profiles.Count)
            throw new ArgumentException("Select distinct positive M5 pivot profiles.", nameof(profiles));

        var orderedProfiles = Array.AsReadOnly(profiles.OrderBy(p => p.LeftBars).ThenBy(p => p.RightBars).ToArray());
        var scans = realignment.Scans.ToDictionary(s => s.Id, s =>
        {
            var firstIds = s.FirstRealignmentIds.ToHashSet(StringComparer.Ordinal);
            return (Scan: s, Attempts: s.Attempts.Where(a => firstIds.Contains(a.Id))
                .OrderBy(a => a.Id, StringComparer.Ordinal).ToArray());
        }, StringComparer.Ordinal);
        var evaluations = new SortedDictionary<string, M5StructuralStopObservation>(StringComparer.Ordinal);
        var associations = new List<M5StructuralStopAssociation>();
        var evaluate = new EvaluateM5StructuralStopLossCandidateUseCase();
        foreach (var source in realignment.Associations.Where(a => a.ScanId is not null)
                     .OrderBy(a => a.EpisodeId, StringComparer.Ordinal)
                     .ThenBy(a => a.M5Configuration.Id, StringComparer.Ordinal).ThenBy(a => a.Profile))
        {
            var (scan, attempts) = scans[source.ScanId!];
            if (attempts.Length == 0)
                Add(null);
            foreach (var attempt in attempts)
                Add(attempt);

            void Add(M1CountertrendAttempt? attempt)
            {
                var candle = attempt?.Realignment;
                if (candle is not null && (candle.CloseTimeUtc > realignment.AsOfUtc ||
                    scan.Anchor.Position(candle.OpenTimeUtc, candle.CloseTimeUtc) != M1WindowPosition.InsideWindow))
                    throw new ArgumentException("M1 realignment must be observable and within the source research window.", nameof(realignment));
                var bindings = source.Bindings.Select(b => new M5StructuralStopBinding(b,
                    ZoneState(b.FvgZoneTimelineId), ZoneState(b.IfvgZoneTimelineId))).ToArray();
                foreach (var profile in orderedProfiles)
                {
                    var outcome = scan.Outcome == "InsufficientEvidence"
                        ? M5StructuralStopOutcome.InsufficientEvidence : M5StructuralStopOutcome.WaitingForM1Realignment;
                    var reason = scan.Outcome;
                    string? id = null;
                    if (candle is not null)
                    {
                        id = FormattableString.Invariant($"{candle.CloseTimeUtc:O}/{scan.Anchor.Direction}/{profile.Id}");
                        if (!evaluations.TryGetValue(id, out var observation))
                        {
                            observation = evaluate.Execute(m5, candle, scan.Anchor.Direction, profile, realignment.SourceProvenance);
                            evaluations.Add(id, observation);
                        }
                        else if (observation.CloseReference.Candle.Open != candle.Open ||
                                 observation.CloseReference.Price != candle.Close ||
                                 observation.CloseReference.Candle.High != candle.High ||
                                 observation.CloseReference.Candle.Low != candle.Low)
                        {
                            throw new ArgumentException("Conflicting OHLC for the same M1 realignment close.", nameof(realignment));
                        }
                        outcome = observation.Outcome;
                        reason = observation.ReasonCode;
                    }
                    associations.Add(new(source.EpisodeId, source.M5Configuration, source.Profile, scan.Anchor, scan.Id,
                        attempt?.Id, candle?.CloseTimeUtc, attempt?.WindowPosition, profile, id, outcome, reason, Array.AsReadOnly(bindings)));
                }

                string? ZoneState(string? timelineId) => timelineId is not null && candle is not null
                    ? realignment.ZoneTimelines[timelineId].Zone.StateAt(candle.CloseTimeUtc) : null;
            }
        }
        return new(realignment.SessionId, realignment.AsOfUtc, realignment.Provider, realignment.Instrument,
            realignment.SourceProvenance, orderedProfiles,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, M5StructuralStopObservation>(evaluations),
            associations.AsReadOnly());
    }
}
