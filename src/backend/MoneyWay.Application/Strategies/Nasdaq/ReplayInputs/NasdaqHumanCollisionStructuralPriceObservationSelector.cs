using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Classifies visible human collision-price evidence without assigning authority or building geometry.</summary>
public sealed class NasdaqHumanCollisionStructuralPriceObservationSelector
{
    public NasdaqHumanCollisionStructuralPriceObservationSelection Select(
        StrategyReplayContext context,
        NasdaqHumanCollisionStructuralPriceEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(episode);

        var supporting = context.InputObservations
            .OfType<NasdaqHumanCollisionStructuralPriceObservation>()
            .Where(episode.Matches)
            .OrderBy(item => item.ObservedAtUtc)
            .ThenBy(item => item.SourceReference, StringComparer.Ordinal)
            .ThenBy(item => item.StructuralPrice)
            .ToArray();
        var prices = supporting
            .Select(item => item.StructuralPrice)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        return prices.Length switch
        {
            0 => new(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Missing, null, [], []),
            1 => new(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique, prices[0], supporting, prices),
            _ => new(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Conflict, null, supporting, prices),
        };
    }
}
