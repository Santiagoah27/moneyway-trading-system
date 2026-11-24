namespace MoneyWay.Application.Research.Nasdaq;

public enum NasdaqResearchChainOutcome
{
    WaitingForPreparationEvidence, H4ContextUnavailable, WaitingForLiquidityTake, LiquidityTakeObserved,
    WaitingForSCOrIFVG, WaitingForDirectionalFVG, WaitingForM1Countertrend, WaitingForM1Realignment,
    WaitingForStructuralReferences, ResearchChainComplete, ResearchChainCompleteWithInvalidatedContext,
    Ambiguous, InsufficientEvidence, DataUnavailable, MixedOutcomes, StructuralReferencesObserved,
    ContextInvalidatedAtRealignment, ResearchChainCompleteWithLimitedContext
}

public sealed record NasdaqResearchBranch(M1M5Binding Binding, M1M5ZoneEvidence Fvg, M1M5ZoneEvidence? Ifvg,
    string? StateAtRealignment, string StateAtCutoff, bool RealignmentBindingVerified);
public sealed record NasdaqResearchContextOutcome(IReadOnlyList<string> H4ConfigurationIds,
    NasdaqResearchChainOutcome Outcome, DateTimeOffset? CompletedAtUtc);
public sealed record NasdaqResearchGeometry(string ConfigurationId, string StopProfileId, string TargetProfileId,
    string? StopEvaluationId, string? TargetEvaluationId, string? RewardRiskComparisonId,
    NasdaqResearchChainOutcome DownstreamOutcome, DateTimeOffset? StructuralReferencesCompletedAtUtc,
    IReadOnlyList<NasdaqResearchContextOutcome> ContextOutcomes);
public sealed record NasdaqResearchChain(string EpisodeId, string M5ConfigurationId, M1RealignmentProfile M1Profile,
    string? ScanId, string? M1AnchorId, string? RealignmentId, DateTimeOffset? LiquidityTakeUtc,
    M5FirstConfirmation FirstConfirmationOutcome, IReadOnlyList<string> FirstConfirmationIds,
    DateTimeOffset? FirstConfirmationUtc, DateTimeOffset? DirectionalFvgUtc, DateTimeOffset? RealignmentUtc,
    string M5Reason, string M1Reason, IReadOnlyList<NasdaqResearchBranch> Branches,
    IReadOnlyList<NasdaqResearchGeometry> Geometry);

public sealed record NasdaqResearchChainSummary(int DistinctSessions, int DistinctEpisodes,
    int EpisodesWithObservedLiquidityTake, int DistinctSelectedConfirmationEvents, int DistinctSelectedConfirmationObservations,
    int DistinctM1Anchors, int DistinctRealignmentEvents, int DistinctRealignmentObservations,
    int DistinctStructuralReferenceCombinations, int DistinctPairsWithPositiveRewardRisk,
    int M5M1ConfigurationAssociations, int EvaluatedStructuralAssociations, int TotalResearchConfigurationAssociations,
    int GeometryCompletedAssociations, int CompleteH4ToM1Associations, int CompleteAssociationsWithInvalidationAtCutoff);

internal static class NasdaqResearchChainProjection
{
    internal static IReadOnlyList<NasdaqResearchChain> Build(M5ContextualSequenceReport sequence, M1RealignmentReport m1,
        StructuralTakeProfitResearchReport targets, NasdaqResearchPreparation preparation)
    {
        var sequences = sequence.Results.ToDictionary(s => (s.Episode.Id, s.Configuration.Id));
        var scans = m1.Scans.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var targetRows = targets.Associations.ToLookup(a =>
            (a.Source.EpisodeId, a.Source.M5Configuration.Id, a.Source.M1Profile, (string?)a.Source.ScanId));
        var stopProfiles = targets.Profiles.Select(p => p.PivotProfile).OfType<M5StructuralPivotProfile>().ToArray();
        var chains = new List<NasdaqResearchChain>();
        foreach (var association in m1.Associations)
        {
            var key = (association.EpisodeId, association.M5Configuration.Id, association.Profile, association.ScanId);
            var evidence = sequences[(key.EpisodeId, key.Id)];
            var scan = association.ScanId is { } scanId ? scans[scanId] : null;
            var records = targetRows[key].GroupBy(a => a.Source.RealignmentId).OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();
            if (records.Length == 0)
                chains.Add(Chain(null, null, []));
            foreach (var record in records)
                chains.Add(Chain(record.Key, record.First().Source.RealignmentConfirmedAtUtc, record.ToArray()));

            NasdaqResearchChain Chain(string? realignmentId, DateTimeOffset? realignedAt,
                IReadOnlyList<StructuralTargetAssociation> rows)
            {
                var observedRealignment = scan is not null && realignedAt is not null &&
                    realignmentId is not null && scan.FirstRealignmentUtc == realignedAt &&
                    scan.FirstRealignmentIds.Contains(realignmentId);
                var branches = association.Bindings.Distinct().OrderBy(b => b.ConfirmationId, StringComparer.Ordinal)
                    .ThenBy(b => b.FvgId, StringComparer.Ordinal).Select(b =>
                    {
                        var fvg = m1.ZoneTimelines[b.FvgZoneTimelineId];
                        var ifvg = b.IfvgZoneTimelineId is { } original ? m1.ZoneTimelines[original] : null;
                        var verified = observedRealignment && scan is not null && realignmentId is not null && realignedAt is { } at &&
                            BindingVerified(evidence, b, fvg, ifvg, scan.Id, realignmentId, at);
                        return new NasdaqResearchBranch(b, fvg.Zone, ifvg?.Zone,
                            realignedAt is { } t ? State(fvg.Zone, ifvg?.Zone, t) : null,
                            State(fvg.Zone, ifvg?.Zone, sequence.AsOfUtc), verified);
                    }).ToArray();
                var pending = Pending(evidence, scan);
                var context = realignedAt is { } t ? ContextAtRealignment(branches, t) : default;
                var geometry = rows.Count == 0
                    ? (from stop in stopProfiles from target in targets.Profiles select Geometry(stop.Id, target.Id, null)).ToArray()
                    : rows.Select(row => Geometry(row.Source.PivotProfile.Id, row.TargetProfile.Id, row)).ToArray();
                return new(key.EpisodeId, key.Id, key.Profile, key.ScanId, scan?.Anchor.Id, realignmentId,
                    evidence.Episode.Selection.Event?.AvailableAtUtc, evidence.FirstOutcome, evidence.FirstEligibleIds,
                    evidence.FirstEligibleUtc, evidence.CompletedAtUtc, realignedAt, evidence.SweepReason,
                    association.Outcome, branches, geometry);

                NasdaqResearchGeometry Geometry(string stopProfile, string targetProfile, StructuralTargetAssociation? row)
                {
                    var id = $"{evidence.Configuration.Id}/{association.Profile}/{stopProfile}/{targetProfile}";
                    var comparison = row?.RewardRiskComparisonId is { } pairId ? targets.RewardRiskComparisons[pairId] : null;
                    var complete = realignedAt is not null &&
                        comparison?.Pairs.Any(p => p.StopDistancePoints > 0 && p.RewardDistancePoints > 0) == true;
                    var stage = complete ? NasdaqResearchChainOutcome.StructuralReferencesObserved :
                        realignedAt is not null ? NasdaqResearchChainOutcome.WaitingForStructuralReferences : pending;
                    var outcomes = preparation.Profiles.Select(p =>
                    {
                        (NasdaqResearchChainOutcome Outcome, DateTimeOffset? CompletedAtUtc) result =
                            p.ReasonCode == "PreparationCutoffNotReached"
                            ? (NasdaqResearchChainOutcome.WaitingForPreparationEvidence, null)
                            : !p.ContextAvailable ? (NasdaqResearchChainOutcome.H4ContextUnavailable, null)
                            : complete ? context : (stage, null);
                        return (p.Id, result.Outcome, result.CompletedAtUtc);
                    }).GroupBy(x => (x.Outcome, x.CompletedAtUtc))
                        .Select(g => new NasdaqResearchContextOutcome(g.Select(x => x.Id).ToArray(), g.Key.Outcome, g.Key.CompletedAtUtc)).ToArray();
                    return new(id, stopProfile, targetProfile, row?.Source.EvaluationId, row?.TargetEvaluationId,
                        row?.RewardRiskComparisonId, stage, complete ? realignedAt : null, outcomes);
                }
            }
        }
        return chains.AsReadOnly();
    }

    private static string State(M1M5ZoneEvidence fvg, M1M5ZoneEvidence? ifvg, DateTimeOffset time)
    {
        var directional = fvg.StateAt(time);
        var inverted = ifvg?.StateAt(time);
        if (directional == "Invalidated" || inverted == "Invalidated") return "Invalidated";
        return directional == "UnknownDueToMissingData" || inverted == "UnknownDueToMissingData"
            ? "EvidenceLimited" : "Observed";
    }

    private static NasdaqResearchChainOutcome Pending(M5SequenceResult sequence, M1RealignmentScan? scan)
    {
        if (sequence.FirstOutcome == M5FirstConfirmation.InsufficientEvidence || scan?.Outcome == "InsufficientEvidence")
            return NasdaqResearchChainOutcome.InsufficientEvidence;
        if (sequence.FirstOutcome is M5FirstConfirmation.AmbiguousSweepOrdering or M5FirstConfirmation.SimultaneousConfirmation &&
            sequence.CompletedAtUtc is null)
            return NasdaqResearchChainOutcome.Ambiguous;
        if (sequence.Episode.Selection.Event is null) return NasdaqResearchChainOutcome.WaitingForLiquidityTake;
        if (sequence.SweepReason != "Eligible") return NasdaqResearchChainOutcome.LiquidityTakeObserved;
        if (sequence.FirstEligibleUtc is null) return NasdaqResearchChainOutcome.WaitingForSCOrIFVG;
        if (sequence.CompletedAtUtc is null) return NasdaqResearchChainOutcome.WaitingForDirectionalFVG;
        return scan?.Attempts.Count > 0 ? NasdaqResearchChainOutcome.WaitingForM1Realignment
            : NasdaqResearchChainOutcome.WaitingForM1Countertrend;
    }

    private static bool BindingVerified(M5SequenceResult sequence, M1M5Binding binding, M1ZoneTimeline fvg,
        M1ZoneTimeline? ifvg, string scanId, string attemptId, DateTimeOffset at)
    {
        if (!sequence.FirstFvgs.Any(f => f.Eligible && f.ConfirmationId == binding.ConfirmationId &&
            f.Zone.Formation.Id == binding.FvgId) || !Matches(fvg, binding.FvgId, "DirectionalFVG"))
            return false;
        var confirmation = sequence.Confirmations.SingleOrDefault(c => c.Event.Id == binding.ConfirmationId && c.Eligible);
        if (confirmation is null) return false;
        return confirmation.Event.OriginZone is { } origin
            ? ifvg is not null && Matches(ifvg, origin.Formation.Id, "IFVG")
            : ifvg is null;

        bool Matches(M1ZoneTimeline timeline, string zoneId, string role) =>
            timeline.ScanId == scanId && timeline.Zone.ZoneId == zoneId && timeline.Zone.Role == role &&
            timeline.Attempts.Where(a => a.AttemptId == attemptId).Select(a => a.StateAtRealignment)
                .SequenceEqual([timeline.Zone.StateAt(at)]);
    }

    internal static (NasdaqResearchChainOutcome Outcome, DateTimeOffset? CompletedAtUtc) ContextAtRealignment(
        IReadOnlyList<NasdaqResearchBranch> branches, DateTimeOffset at)
    {
        var supporting = branches.Where(b => b.RealignmentBindingVerified && b.StateAtRealignment == "Observed").ToArray();
        if (supporting.Length > 0)
        {
            var outcome = supporting.Any(b => b.StateAtCutoff == "Observed")
                ? NasdaqResearchChainOutcome.ResearchChainComplete
                : supporting.All(b => b.StateAtCutoff == "Invalidated")
                    ? NasdaqResearchChainOutcome.ResearchChainCompleteWithInvalidatedContext
                    : NasdaqResearchChainOutcome.ResearchChainCompleteWithLimitedContext;
            return (outcome, at);
        }
        if (branches.Any(b => !b.RealignmentBindingVerified || !InvalidatedBefore(b) &&
            (b.Fvg.InvalidatedAtUtc == at || b.Ifvg?.InvalidatedAtUtc == at)))
            return (NasdaqResearchChainOutcome.Ambiguous, null);
        return (branches.Count > 0 && branches.All(InvalidatedBefore)
            ? NasdaqResearchChainOutcome.ContextInvalidatedAtRealignment
            : NasdaqResearchChainOutcome.InsufficientEvidence, null);

        bool InvalidatedBefore(NasdaqResearchBranch branch) =>
            branch.Fvg.InvalidatedAtUtc < at || branch.Ifvg?.InvalidatedAtUtc < at;
    }

    internal static NasdaqResearchChainOutcome SummarizeOutcome(IReadOnlyList<NasdaqResearchChain> chains)
    {
        var outcomes = chains.SelectMany(c => c.Geometry).SelectMany(g => g.ContextOutcomes).Select(o => o.Outcome).Distinct().ToArray();
        return outcomes.Length == 1 ? outcomes[0] : NasdaqResearchChainOutcome.MixedOutcomes;
    }

    internal static NasdaqResearchChainSummary Summarize(IReadOnlyList<NasdaqResearchChain> chains, NasdaqResearchPreparation preparation,
        M5ContextualSequenceReport? sequence, StructuralTakeProfitResearchReport? targets)
    {
        var rows = chains.SelectMany(c => c.Geometry).ToArray();
        var contexts = rows.SelectMany(g => g.ContextOutcomes).ToArray();
        var selected = (sequence?.Results ?? []).SelectMany(s => s.Confirmations
            .Where(c => s.FirstEligibleIds.Contains(c.Event.Id)).Select(c => c.Event)).ToArray();
        return new(1, chains.Select(c => c.EpisodeId).Distinct(StringComparer.Ordinal).Count(),
            chains.Where(c => c.LiquidityTakeUtc is not null).Select(c => c.EpisodeId).Distinct(StringComparer.Ordinal).Count(),
            selected.Select(c => c.AssociationKey).Distinct(StringComparer.Ordinal).Count(),
            selected.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count(),
            chains.Select(c => c.M1AnchorId).OfType<string>().Distinct(StringComparer.Ordinal).Count(),
            targets?.DistinctM1References ?? 0,
            chains.Select(c => c.RealignmentId).OfType<string>().Distinct(StringComparer.Ordinal).Count(),
            targets?.RewardRiskComparisons.Values.Sum(x => x.Pairs.Count) ?? 0,
            targets?.RewardRiskComparisons.Values.Sum(x => x.Pairs.Count(p => p.HypotheticalRewardRisk > 0)) ?? 0,
            chains.Select(c => (c.EpisodeId, c.M5ConfigurationId, c.M1Profile)).Distinct().Count(),
            rows.Count(g => g.TargetEvaluationId is not null), rows.Length * preparation.Profiles.Count,
            rows.Count(g => g.StructuralReferencesCompletedAtUtc is not null),
            contexts.Where(c => c.CompletedAtUtc is not null).Sum(c => c.H4ConfigurationIds.Count),
            contexts.Where(c => c.CompletedAtUtc is not null && c.Outcome == NasdaqResearchChainOutcome.ResearchChainCompleteWithInvalidatedContext)
                .Sum(c => c.H4ConfigurationIds.Count));
    }
}
