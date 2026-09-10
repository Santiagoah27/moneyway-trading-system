using System.Collections.ObjectModel;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>Provides compact, immutable source-resolution metadata without copying historical observations.</summary>
public sealed class ReplayMarketDataAvailability
{
    public static ReplayMarketDataAvailability CandleOnly { get; } = new(false, null);

    internal ReplayMarketDataAvailability(
        bool highResolutionInputConfigured,
        HistoricalMarketPriceObservationGroup? currentGroup,
        int visibleObservationCount = 0,
        IEnumerable<string>? observationKinds = null,
        IEnumerable<string>? sourceResolutions = null)
    {
        if (!highResolutionInputConfigured && currentGroup is not null)
            throw new ArgumentException("Current high-resolution observations require configured input.", nameof(currentGroup));
        if (visibleObservationCount < 0 || (!highResolutionInputConfigured && visibleObservationCount != 0))
            throw new ArgumentOutOfRangeException(nameof(visibleObservationCount));
        HighResolutionInputConfigured = highResolutionInputConfigured;
        VisibleObservationCount = visibleObservationCount;
        CurrentObservationCount = currentGroup?.Observations.Count ?? 0;
        CurrentGroupHasAuthoritativeOrder = currentGroup is null ? null : currentGroup.HasAuthoritativeOrder;
        ObservationKinds = new ReadOnlyCollection<string>((observationKinds ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        SourceResolutions = new ReadOnlyCollection<string>((sourceResolutions ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public bool HighResolutionInputConfigured { get; }
    public int VisibleObservationCount { get; }
    public int CurrentObservationCount { get; }
    public bool? CurrentGroupHasAuthoritativeOrder { get; }
    public IReadOnlyList<string> ObservationKinds { get; }
    public IReadOnlyList<string> SourceResolutions { get; }
}
