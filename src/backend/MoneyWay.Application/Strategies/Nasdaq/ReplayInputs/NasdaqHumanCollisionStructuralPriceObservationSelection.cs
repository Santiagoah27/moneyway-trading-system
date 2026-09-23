using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Immutable semantic selection outcome for visible collision StructuralPrice evidence.</summary>
public sealed class NasdaqHumanCollisionStructuralPriceObservationSelection
{
    internal NasdaqHumanCollisionStructuralPriceObservationSelection(
        NasdaqHumanCollisionStructuralPriceObservationSelectionKind kind,
        decimal? structuralPrice,
        IEnumerable<NasdaqHumanCollisionStructuralPriceObservation> supportingObservations,
        IEnumerable<decimal> distinctStructuralPrices)
    {
        Kind = kind;
        StructuralPrice = structuralPrice;
        SupportingObservations = new ReadOnlyCollection<NasdaqHumanCollisionStructuralPriceObservation>(supportingObservations.ToArray());
        DistinctStructuralPrices = new ReadOnlyCollection<decimal>(distinctStructuralPrices.ToArray());
    }

    public NasdaqHumanCollisionStructuralPriceObservationSelectionKind Kind { get; }
    public decimal? StructuralPrice { get; }
    public IReadOnlyList<NasdaqHumanCollisionStructuralPriceObservation> SupportingObservations { get; }
    public IReadOnlyList<decimal> DistinctStructuralPrices { get; }
}
