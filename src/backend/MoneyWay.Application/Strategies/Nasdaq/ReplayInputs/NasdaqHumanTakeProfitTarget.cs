namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Explicitly selected TP level. Its source identity does not independently establish target relevance.</summary>
public sealed class NasdaqHumanTakeProfitTarget
{
    public NasdaqHumanTakeProfitTarget(NasdaqLiquidityTakeReference reference, decimal takeProfitPrice)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        if (takeProfitPrice != reference.ReferencePrice)
            throw new ArgumentException("Take Profit must equal the exact selected reference price without adjustment.", nameof(takeProfitPrice));
        TakeProfitPrice = takeProfitPrice;
    }

    public NasdaqLiquidityTakeReference Reference { get; }
    public NasdaqStructuralLiquiditySide Side => Reference.Side;
    public decimal TargetReferencePrice => Reference.ReferencePrice;
    public decimal TakeProfitPrice { get; }

    internal bool SameFact(NasdaqHumanTakeProfitTarget other) => Reference.SameSlot(other.Reference)
        && TargetReferencePrice == other.TargetReferencePrice && TakeProfitPrice == other.TakeProfitPrice;
}
