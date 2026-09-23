using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves one unique rebuilt-candidate membership against the context's observable closed H4 candles.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexMemberResolver
{
    private readonly StructuralTurnProtectionAnchorCalculator protectionAnchorCalculator = new();

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

        var members = new List<Candle>(selection.SemanticMemberOpenTimesUtc.Count);
        foreach (var openTimeUtc in selection.SemanticMemberOpenTimesUtc)
        {
            var member = frame!.AvailableCandles.FirstOrDefault(candle => candle.OpenTimeUtc == openTimeUtc)
                ?? throw new InvalidOperationException("A selected H4 rebuilt candidate vertex member is not observable.");
            members.Add(member);
        }

        var migration = members.SingleOrDefault(candle => candle.OpenTimeUtc == resolutionContext.MigrationCandle.OpenTimeUtc)
            ?? throw new InvalidOperationException("The migration candle is not present in the selected rebuilt candidate membership.");
        if (migration.ProviderId != resolutionContext.MigrationCandle.ProviderId || migration.Symbol != resolutionContext.MigrationCandle.Symbol
            || migration.Timeframe != resolutionContext.MigrationCandle.Timeframe || migration.CloseTimeUtc != resolutionContext.MigrationCandle.CloseTimeUtc)
        {
            throw new InvalidOperationException("The selected migration candle does not match the resolution context.");
        }

        var protectionSide = resolutionContext.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? StructuralTurnProtectionSide.Lower
            : StructuralTurnProtectionSide.Upper;
        var derivedProtectionAnchor = protectionAnchorCalculator.Evaluate(members, protectionSide).ProtectionAnchor;
        if (derivedProtectionAnchor != resolutionContext.KnownProtectionAnchor)
            throw new InvalidOperationException("Selected rebuilt candidate membership does not reproduce the known protection anchor.");

        return new NasdaqHumanRebuiltCandidateVertexMemberResolution(
            resolutionContext.Episode, migration, members.OrderBy(candle => candle.OpenTimeUtc), resolutionContext.KnownProtectionAnchor);
    }
}
