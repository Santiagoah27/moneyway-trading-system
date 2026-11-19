using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum M5ImpulseDirection { Ascending, Descending }
public enum M5ResearchDataOrigin { Synthetic, RetrospectiveDocumentary }
public enum M5Hypothesis { CandleColor, PriceStructure, CombinedStrict, CombinedFlexible }
public enum M5ExperimentalStatus
{
    ExperimentalReferenceConfirmed, AwaitingPullbackStart, AwaitingContinuation,
    AbstainedAmbiguousStartAndContinuation, AbstainedNoPullbackBeforeContinuation,
    AbstainedMissingInitialization, AbstainedInitializationUnavailable, AbstainedLateInitialization, RejectedInput
}
public sealed record M5ImpulseInitialization(string ReferenceId, MarketDataProviderId ProviderId, MarketSymbol Symbol,
    Timeframe Timeframe, DateTimeOffset AnchorOpenTimeUtc, DateTimeOffset AvailableAtUtc, string SourceReference);
public sealed record M5SwingExperimentInput(string SeriesId, CandleSeries Series, M5ImpulseDirection Direction,
    DateTimeOffset CutoffUtc, M5ImpulseInitialization? Initialization, M5ResearchDataOrigin DataOrigin, string Provenance);
public sealed record M5SwingExperimentContext(string SeriesId, string ProviderId, string Symbol, string Timeframe,
    M5ImpulseDirection Direction, DateTimeOffset EvaluationCutoffUtc, M5ImpulseInitialization? Initialization,
    decimal? InitialImpulsePrice, DateTimeOffset? InitialImpulseClosedAtUtc, M5ResearchDataOrigin DataOrigin, string Provenance)
{
    public string PriceUnit => "NativeInstrumentPrice";
    public string HistoricalAvailability => "Unauthenticated_NotEstablishedByThisExperiment";
    public string Coverage => "Bounded supplied sequence; previous means preceding supplied closed candle; completeness not authenticated; no gap filling";
}
public sealed record M5SwingHypothesisDefinition(M5Hypothesis Hypothesis, string AlgorithmId, string AlgorithmVersion, string StartCondition);
public sealed record M5SwingHypothesisResult(M5SwingHypothesisDefinition Algorithm, M5SwingExperimentContext Input,
    M5ExperimentalStatus Status, string Reason, Candle? PullbackStart, decimal? ProposedReferencePrice,
    IReadOnlyList<Candle> ReferenceCandles, IReadOnlyList<Candle> AggregationCandles, Candle? ContinuationCandle)
{
    public string Classification => "ExperimentalMoneyWayHypothesis_NotMentorConfirmed";
    public string SharedPolicyId => "EXP-M5-BOUNDED-CONTINUATION-V1";
    public bool IsExperimentalReferenceConfirmed => Status == M5ExperimentalStatus.ExperimentalReferenceConfirmed;
    public bool Abstained => Status.ToString().StartsWith("Abstained", StringComparison.Ordinal) || Status == M5ExperimentalStatus.RejectedInput;
    public DateTimeOffset? ConfirmationAtUtc => IsExperimentalReferenceConfirmed ? ContinuationCandle?.CloseTimeUtc : null;
    public TimeSpan? ConfirmationLatencyFromAnchor => ConfirmationAtUtc is { } end && Input.InitialImpulseClosedAtUtc is { } closed ? end - closed : null;
    public TimeSpan? PullbackToConfirmationLatency => ConfirmationAtUtc is { } end && PullbackStart is { } start ? end - start.CloseTimeUtc : null;
    public bool MentorRuleConfirmed => false;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}
public sealed record M5HypothesisPairComparison(string LeftAlgorithm, string RightAlgorithm, bool FullAgreement,
    bool? ConfirmedReferenceAgreement, TimeSpan? ConfirmationLatencyDifference, TimeSpan? PullbackLatencyDifference, string Explanation);
public sealed record M5SwingComparisonReport(M5SwingExperimentContext Input, IReadOnlyList<string> SharedAssumptions,
    IReadOnlyList<M5SwingHypothesisResult> Results, IReadOnlyList<M5HypothesisPairComparison> Comparisons, IReadOnlyList<Candle> EvidenceCandles)
{
    public string SchemaVersion => "1";
    public string Scope => "IsolatedExperimentalResearch_NotCanonicalStrategyEvaluation";
    public bool AllAlgorithmsAgree => Comparisons.All(c => c.FullAgreement);
    public IReadOnlyList<string> AbstainedAlgorithms => Results.Where(r => r.Abstained).Select(r => r.Algorithm.AlgorithmId).ToArray();
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public string PromotionStatus => "ResearchOnly_NoWinnerSelected_NoStrategyActivation";
}
