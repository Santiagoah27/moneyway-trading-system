using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves one unique rebuilt-candidate membership against the context's observable closed H4 candles.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexMemberResolver
{
    private readonly StructuralTurnProtectionAnchorCalculator protectionAnchorCalculator = new();

    public NasdaqHumanRebuiltCandidateVertexMemberResolution Evaluate(
        NasdaqPostInvalidationCandidateRebuildPendingState pending,
        NasdaqHumanRebuiltCandidateVertexObservationSelection selection,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(context);

        if (selection.Kind != NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique)
            throw new ArgumentException("Only a unique rebuilt candidate membership can be resolved.", nameof(selection));
        if (selection.SemanticMemberOpenTimesUtc.Count == 0 || selection.SupportingObservations.Count == 0)
            throw new ArgumentException("A unique selection must contain semantic membership and supporting evidence.", nameof(selection));
        if (context.ProviderId != pending.MigrationCandle.ProviderId || context.Symbol != pending.MigrationCandle.Symbol)
            throw new ArgumentException("The rebuild pending state must match the replay context market identity.", nameof(pending));
        if (!context.TryGetFrame(NasdaqHumanRebuiltCandidateVertexObservation.H4, out var frame))
            throw new InvalidOperationException("An observable H4 candle frame is required.");

        var episode = new NasdaqHumanRebuiltCandidateVertexEpisode(
            context.StrategyId, context.StrategyVersion, context.ProviderId, context.Symbol,
            pending.InvalidatingCandle.OpenTimeUtc, pending.MigrationCandle.OpenTimeUtc, pending.CandidateSide);
        if (selection.SupportingObservations.Any(observation => !episode.Matches(observation)))
            throw new ArgumentException("The selected human membership must match the rebuild pending episode.", nameof(selection));

        var members = new List<Candle>(selection.SemanticMemberOpenTimesUtc.Count);
        foreach (var openTimeUtc in selection.SemanticMemberOpenTimesUtc)
        {
            var member = frame!.AvailableCandles.FirstOrDefault(candle => candle.OpenTimeUtc == openTimeUtc)
                ?? throw new InvalidOperationException("A selected H4 rebuilt candidate vertex member is not observable.");
            members.Add(member);
        }

        var migration = members.SingleOrDefault(candle => candle.OpenTimeUtc == pending.MigrationCandle.OpenTimeUtc)
            ?? throw new InvalidOperationException("The migration candle is not present in the selected rebuilt candidate membership.");
        if (migration.ProviderId != pending.MigrationCandle.ProviderId || migration.Symbol != pending.MigrationCandle.Symbol
            || migration.Timeframe != pending.MigrationCandle.Timeframe || migration.CloseTimeUtc != pending.MigrationCandle.CloseTimeUtc)
        {
            throw new InvalidOperationException("The selected migration candle does not match the rebuild pending state.");
        }

        var protectionSide = pending.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? StructuralTurnProtectionSide.Lower
            : StructuralTurnProtectionSide.Upper;
        var derivedProtectionAnchor = protectionAnchorCalculator.Evaluate(members, protectionSide).ProtectionAnchor;
        if (derivedProtectionAnchor != pending.KnownProtectionAnchor)
            throw new InvalidOperationException("Selected rebuilt candidate membership does not reproduce the known protection anchor.");

        return new NasdaqHumanRebuiltCandidateVertexMemberResolution(
            episode, migration, members.OrderBy(candle => candle.OpenTimeUtc), pending.KnownProtectionAnchor);
    }
}
