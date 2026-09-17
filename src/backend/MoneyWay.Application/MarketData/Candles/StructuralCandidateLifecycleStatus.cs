namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Distinguishes a continuing candidate from validation, ordinary discard, and structural invalidation.</summary>
public enum StructuralCandidateLifecycleStatus
{
    Active = 0,
    Validated = 1,
    Discarded = 2,
    StructureInvalidated = 3,
}
