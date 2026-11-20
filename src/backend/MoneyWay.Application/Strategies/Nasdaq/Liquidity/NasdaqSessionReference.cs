using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.Liquidity;

/// <summary>Source-bound session calculation, not relevance, a take, a verdict or data authentication.</summary>
public sealed record NasdaqSessionReference(string StrategyVersion, DateOnly OperatingDate, string Session,
    string Provider, string Instrument, string SourceTimeframe, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    DateTimeOffset EvaluatedAtUtc, string CoverageStatus, int ExpectedCount, IReadOnlyList<Candle> SourceCandles,
    IReadOnlyList<DateTimeOffset> MissingOpensUtc, decimal? High, decimal? Low,
    IReadOnlyList<Candle> HighCandles, IReadOnlyList<Candle> LowCandles)
{
    public bool IsComplete => CoverageStatus == "Complete";
    public DateTimeOffset? AvailableAtUtc => IsComplete ? EvaluatedAtUtc : null;
    public string AvailabilityMeaning => "Observable in supplied replay/source prefix; not authenticated provider publication";
    public string SourceAuthority => SourceTimeframe == "1h" ? "AuditedExactH1" : "ExplicitNonAuthoritativeComparison";
}
