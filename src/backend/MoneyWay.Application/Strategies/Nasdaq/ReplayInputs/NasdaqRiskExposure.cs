namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Documented planned exposure, not calculated size or a risk-limit decision.</summary>
public sealed record NasdaqRiskExposure
{
    public NasdaqRiskExposure(decimal accountBasisAmount, string currency, string accountBasis,
        decimal reportedMaximumLoss, decimal plannedEntryPrice, string costTreatment,
        string calculationSourceReference, decimal? quantity = null, string? quantityUnit = null)
    {
        if (accountBasisAmount <= 0) throw new ArgumentOutOfRangeException(nameof(accountBasisAmount));
        if (reportedMaximumLoss < 0) throw new ArgumentOutOfRangeException(nameof(reportedMaximumLoss));
        NasdaqStructuralLiquidityReference.ValidateProvenance(currency);
        NasdaqStructuralLiquidityReference.ValidateProvenance(accountBasis);
        NasdaqStructuralLiquidityReference.ValidateProvenance(costTreatment);
        NasdaqStructuralLiquidityReference.ValidateProvenance(calculationSourceReference);
        if (quantity is <= 0 || quantity.HasValue != (quantityUnit is not null))
            throw new ArgumentException("Documented quantity must be positive and have an explicit unit.", nameof(quantity));
        if (quantityUnit is not null) NasdaqStructuralLiquidityReference.ValidateProvenance(quantityUnit);
        AccountBasisAmount = accountBasisAmount;
        Currency = currency;
        AccountBasis = accountBasis;
        ReportedMaximumLoss = reportedMaximumLoss;
        PlannedEntryPrice = plannedEntryPrice;
        CostTreatment = costTreatment;
        CalculationSourceReference = calculationSourceReference;
        Quantity = quantity;
        QuantityUnit = quantityUnit;
    }

    public decimal AccountBasisAmount { get; }
    /// <summary>Both monetary amounts use this exact documented currency.</summary>
    public string Currency { get; }
    /// <summary>Source-named basis; no balance/equity convention is selected by MoneyWay.</summary>
    public string AccountBasis { get; }
    public decimal ReportedMaximumLoss { get; }
    public decimal PlannedEntryPrice { get; }
    public string CostTreatment { get; }
    public string CalculationSourceReference { get; }
    public decimal? Quantity { get; }
    public string? QuantityUnit { get; }

    internal bool SameFact(NasdaqRiskExposure other) => AccountBasisAmount == other.AccountBasisAmount
        && Currency == other.Currency && AccountBasis == other.AccountBasis
        && ReportedMaximumLoss == other.ReportedMaximumLoss && PlannedEntryPrice == other.PlannedEntryPrice
        && CostTreatment == other.CostTreatment && Quantity == other.Quantity && QuantityUnit == other.QuantityUnit;
}
