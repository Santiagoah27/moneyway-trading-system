using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum LondonAsiaCompletedClassification { NoAsiaExtremePenetrated, AsiaHighOnlyPenetrated, AsiaLowOnlyPenetrated, BothAsiaExtremesPenetrated }
public sealed record LondonAsiaSideObservation(decimal ReferencePrice, bool ExactExtremeContact, bool StrictPenetration,
    bool CloseBeyond, bool PenetrationWithoutCloseBeyond, bool OpensBeyondReference, decimal PenetrationMagnitude);
public sealed record LondonAsiaCandleObservation(Candle Candle, DateTimeOffset ObservableAtUtc,
    LondonAsiaSideObservation AsiaHigh, LondonAsiaSideObservation AsiaLow)
{
    public string Ordering => AsiaHigh.StrictPenetration && AsiaLow.StrictPenetration ? "BothInSameCandle_OrderUnknown" : "NoIntrabarTrajectoryInferred";
}
public sealed record LondonAsiaInteractionResult(DateOnly OperatingDate, string Provider, string Instrument,
    string SourceTimeframe, DateTimeOffset CutoffUtc, M5ResearchDataOrigin DataOrigin, string SourceProvenance,
    string Status, string Reason, NasdaqSessionReference Asia, NasdaqSessionReference London,
    IReadOnlyList<LondonAsiaCandleObservation> Observations, LondonAsiaCompletedClassification? CompletedClassification)
{
    public string AlgorithmId => "EXP-NQ-LONDON-ASIA-INTERACTION-V1";
    public string AlgorithmVersion => "1.0.0";
    public string Classification => "DescriptiveOhlcResearch_NotMentorConfirmedOperationalInterpretation";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public string AvailabilityMeaning => "Evaluated source prefix, not authenticated publication or exact event time. First means first observed supplied candle, not an event in missing data.";
    public LondonAsiaCandleObservation? FirstUpperPenetration => Observations.FirstOrDefault(o => o.AsiaHigh.StrictPenetration);
    public LondonAsiaCandleObservation? FirstLowerPenetration => Observations.FirstOrDefault(o => o.AsiaLow.StrictPenetration);
    public LondonAsiaCandleObservation? FirstUpperCloseBeyond => Observations.FirstOrDefault(o => o.AsiaHigh.CloseBeyond);
    public LondonAsiaCandleObservation? FirstLowerCloseBeyond => Observations.FirstOrDefault(o => o.AsiaLow.CloseBeyond);
    public LondonAsiaCandleObservation? FirstUpperExactContact => Observations.FirstOrDefault(o => o.AsiaHigh.ExactExtremeContact);
    public LondonAsiaCandleObservation? FirstLowerExactContact => Observations.FirstOrDefault(o => o.AsiaLow.ExactExtremeContact);
    public decimal? ObservedUpperMagnitude => Observations.Count > 0 ? Observations.Max(o => o.AsiaHigh.PenetrationMagnitude) : null;
    public decimal? ObservedLowerMagnitude => Observations.Count > 0 ? Observations.Max(o => o.AsiaLow.PenetrationMagnitude) : null;
    public decimal? CompletedUpperMagnitude => CompletedClassification is null ? null : ObservedUpperMagnitude;
    public decimal? CompletedLowerMagnitude => CompletedClassification is null ? null : ObservedLowerMagnitude;
    public string FirstPenetrationOrdering => (FirstUpperPenetration, FirstLowerPenetration) switch
    {
        (null, null) => "NeitherObserved",
        (not null, null) => "OnlyUpperObserved",
        (null, not null) => "OnlyLowerObserved",
        ({ } upper, { } lower) when upper.Candle.OpenTimeUtc == lower.Candle.OpenTimeUtc => "BothInSameCandle_OrderUnknown",
        ({ } upper, { } lower) when upper.Candle.OpenTimeUtc < lower.Candle.OpenTimeUtc => "UpperBeforeLowerAtCandleResolution",
        _ => "LowerBeforeUpperAtCandleResolution"
    };
}

/// <summary>Pure, stateless research over completed Asia and observable London H1. No canonical input/fact/evaluator interfaces.</summary>
public sealed class AnalyzeLondonAsiaInteractionUseCase
{
    public LondonAsiaInteractionResult Execute(CandleSeries source, DateOnly operatingDate, DateTimeOffset cutoffUtc,
        M5ResearchDataOrigin dataOrigin, string sourceProvenance)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Timeframe != new Timeframe(1, TimeframeUnit.Hour)) throw new ArgumentException("Exact H1 required; no lower-timeframe ordering refinement.");
        if (!Enum.IsDefined(dataOrigin) || string.IsNullOrWhiteSpace(sourceProvenance) || sourceProvenance != sourceProvenance.Trim()
            || sourceProvenance.Length > 2048 || sourceProvenance.Any(char.IsControl)) throw new ArgumentException("Explicit bounded research provenance required.");
        var sessions = new NasdaqSessionLiquidityCalculator().CalculateExplicitSessions(source, operatingDate, cutoffUtc);
        var asia = sessions[0]; var london = sessions[1];
        LondonAsiaInteractionResult Result(string status, string reason, IReadOnlyList<LondonAsiaCandleObservation> observations,
            LondonAsiaCompletedClassification? classification = null) => new(operatingDate, source.ProviderId.Value, source.Symbol.Value,
                source.Timeframe.ToString(), cutoffUtc, dataOrigin, sourceProvenance, status, reason, asia, london, observations, classification);
        if (!asia.IsComplete) return Result("AsiaUnavailable", $"Completed Asia references unavailable: {asia.CoverageStatus}; no interaction inferred.", []);
        static LondonAsiaSideObservation Side(Candle c, decimal reference, bool upper)
        {
            var extreme = upper ? c.High : c.Low;
            var penetration = upper ? extreme > reference : extreme < reference;
            var close = upper ? c.Close > reference : c.Close < reference;
            return new(reference, extreme == reference, penetration, close, penetration && !close,
                upper ? c.Open > reference : c.Open < reference, penetration ? upper ? extreme - reference : reference - extreme : 0m);
        }
        var observations = Array.AsReadOnly(london.SourceCandles.Select(c => new LondonAsiaCandleObservation(c, cutoffUtc,
            Side(c, asia.High!.Value, true), Side(c, asia.Low!.Value, false))).ToArray());
        if (!london.IsComplete)
        {
            var pastMissing = london.MissingOpensUtc.Any(t => t.AddHours(1) <= cutoffUtc);
            return Result(cutoffUtc < london.EndUtc ? pastMissing ? "PartialWithMissingData" : "Partial" : "LondonIncomplete",
                $"Only observed closed H1 relationships are reported; no completed classification. London coverage: {london.CoverageStatus}.", observations);
        }
        var upper = observations.Any(o => o.AsiaHigh.StrictPenetration); var lower = observations.Any(o => o.AsiaLow.StrictPenetration);
        var category = upper ? lower ? LondonAsiaCompletedClassification.BothAsiaExtremesPenetrated : LondonAsiaCompletedClassification.AsiaHighOnlyPenetrated
            : lower ? LondonAsiaCompletedClassification.AsiaLowOnlyPenetrated : LondonAsiaCompletedClassification.NoAsiaExtremePenetrated;
        return Result("Complete", "Completed source coverage supports descriptive OHLC relationships only; no relevance, direction, rejection mechanism, crossing trajectory or strategy authorization inferred.", observations, category);
    }
}
