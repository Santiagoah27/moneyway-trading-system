using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Separates definitive 007 vertex ownership from the unresolved 008 price boundary.</summary>
public abstract class NasdaqCollisionCandidateResolution
{
    private NasdaqCollisionCandidateResolution() { }

    public sealed class Directional : NasdaqCollisionCandidateResolution
    {
        internal Directional(StructuralCandidateValidationResult validation) => Validation = validation;
        public StructuralCandidateValidationResult Validation { get; }
        /// <summary>The same collision candle owns both geometry components and confirms validation.</summary>
        public Candle Member => Validation.BreakObservation.Candle!;
    }

    public sealed class HumanStructuralPriceRequired : NasdaqCollisionCandidateResolution
    {
        internal HumanStructuralPriceRequired() { }
    }
}
