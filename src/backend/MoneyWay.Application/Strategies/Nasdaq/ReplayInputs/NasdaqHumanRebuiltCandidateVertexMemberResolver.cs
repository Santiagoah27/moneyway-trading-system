using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves one unique rebuilt-candidate membership against the context's observable closed H4 candles.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexMemberResolver
{
    public NasdaqHumanRebuiltCandidateVertexMemberResolution Evaluate(
        NasdaqHumanRebuiltCandidateVertexResolutionContext resolutionContext,
        NasdaqHumanRebuiltCandidateVertexObservationSelection selection,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(resolutionContext);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(context);

        if (selection.Kind != NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique)
            throw new ArgumentException("Only a unique rebuilt candidate membership can be resolved.", nameof(selection));
        if (selection.SemanticMemberOpenTimesUtc.Count == 0 || selection.SupportingObservations.Count == 0)
            throw new ArgumentException("A unique selection must contain semantic membership and supporting evidence.", nameof(selection));
        if (context.StrategyId != resolutionContext.Episode.StrategyId
            || context.StrategyVersion != resolutionContext.Episode.StrategyVersion
            || context.ProviderId != resolutionContext.Episode.ProviderId
            || context.Symbol != resolutionContext.Episode.Symbol)
        {
            throw new ArgumentException("The resolution context must match the replay context identity.", nameof(resolutionContext));
        }
        if (!context.TryGetFrame(NasdaqHumanRebuiltCandidateVertexObservation.H4, out var frame))
            throw new InvalidOperationException("An observable H4 candle frame is required.");

        if (selection.SupportingObservations.Any(observation => !resolutionContext.Episode.Matches(observation)))
            throw new ArgumentException("The selected human membership must match the rebuilt-candidate episode.", nameof(selection));

        var (members, missing) = RebuiltCandidateMemberResolver.Resolve(selection.SemanticMemberOpenTimesUtc,
            resolutionContext.MigrationCandle, resolutionContext.CandidateSide, resolutionContext.KnownProtectionAnchor, context);
        if (missing.Count > 0)
            throw new InvalidOperationException("A selected H4 rebuilt candidate vertex member is not observable.");
        var migration = members.Single(candle => candle.OpenTimeUtc == resolutionContext.MigrationCandle.OpenTimeUtc);
        return new NasdaqHumanRebuiltCandidateVertexMemberResolution(
            resolutionContext.Episode, migration, members, resolutionContext.KnownProtectionAnchor);
    }
}
