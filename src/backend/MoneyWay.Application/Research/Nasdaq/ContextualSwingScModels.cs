using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public sealed record ContextualScEventSelection(string PolicyId, string Version, ContextualLiquidityReference LiquidityReference,
    string Status, string Reason, ContextualLiquidityObservation? Event, ContextualLiquidityObservation? ObservedCandidate,
    ContextualLiquidityObservation? EarlierPenetration, ContextualLiquidityObservation? IntermediateReturn,
    IReadOnlyList<DateTimeOffset> MissingPriorOpensUtc, string TrackingCoverage);
public sealed record ContextualStructuralSelection(decimal Price, string Side, IReadOnlyList<Candle> Sources,
    DateTimeOffset FormationAtUtc, DateTimeOffset AvailableAtUtc, DateTimeOffset SelectedAtUtc);
public sealed record ContextualSwingScComparison(string Id, ContextualScEventSelection EventSelection, string HypothesisId,
    string HypothesisVersion, string? SwingVariantId, string? SwingVariantVersion, string ExperimentalBreakDirection,
    string Status, string Reason, ContextualStructuralSelection? StructuralReference, M5SwingHypothesisResult? BoundedDetector,
    StructuralBodyCloseBreakResult? PreEventBreak, StructuralBodyCloseBreakResult? EventCloseComparison,
    StructuralBodyCloseBreakResult? SubsequentSc, StructuralBodyCloseBreakResult? ObservedSubsequentBreak,
    IReadOnlyList<DateTimeOffset> MissingStructuralOpensUtc, IReadOnlyList<DateTimeOffset> MissingScOpensUtc)
{
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public string Limitation => "Experimental directional mapping, frozen reference, unresolved relevance/replacement; no operational approval or canonical fact.";
}
public sealed record ContextualScOutcomeGroup(IReadOnlyList<string> ComparisonIds, string Meaning)
{
    public string Scope => "EqualReportedOutcomes_NotEquivalentAlgorithmsOrIndependentEpisodes";
}
public sealed record ContextualSwingScReport(ContextualLiquidityTrackingResult Tracking,
    IReadOnlyList<ContextualScEventSelection> EventSelections, IReadOnlyList<ContextualSwingScComparison> Comparisons,
    IReadOnlyList<ContextualScOutcomeGroup> OutcomeGroups)
{
    public const string ResearchId = "EXP-NQ-CONTEXT-SWING-SC-V1";
    public const string ResearchVersion = "1.0.0";
    public string AlgorithmId => ResearchId;
    public string Version => ResearchVersion;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public string Scope => "IsolatedContextualStructuralResearch_NotMentorConfirmed_NotIndependentValidation";
    public IReadOnlyList<string> Assumptions => Array.AsReadOnly(new[] {
        "Low-side liquidity investigates upward break; high-side investigates downward break. No operational BUY/SELL.",
        "E1 first observed; E2 first penetration candle opening from 08:30; E3 distinct prior penetration / strict other-side close / later penetration opening from 08:30. Verify first against required coverage.",
        "H1 previous exact M5; H2 continuous preparation range; H3 four unchanged inclusive fixed-anchor variants initialized at the exact 08:00 candle. References known no later than event open.",
        "Freeze each reference. A prior strict break is stale. Event-candle strict break is unresolved same-candle evidence; do not credit later continuation. Subsequent SC opens no earlier than event close.",
        "Full required closed-slot coverage, no gap filling, intrabar path, tolerance, ranking, reference replacement, multi-cycle rescue, outcome optimization or promotion.",
        "Original continuation labels are not contextual SC ground truth. Documentary timestamps are not authenticated availability." });
}
