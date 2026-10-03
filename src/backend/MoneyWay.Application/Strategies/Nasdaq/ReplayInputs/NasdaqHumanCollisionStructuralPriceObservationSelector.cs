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

        var (supporting, prices) = StructuralPriceEvidenceSelector.Select(context.InputObservations
            .OfType<NasdaqHumanCollisionStructuralPriceObservation>()
            .Where(episode.Matches), item => item.StructuralPrice, item => item.SourceReference);

        return prices.Count switch
        {
            0 => new(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Missing, null, [], []),
            1 => new(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique, prices[0], supporting, prices),
            _ => new(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Conflict, null, supporting, prices),
        };
    }
}
