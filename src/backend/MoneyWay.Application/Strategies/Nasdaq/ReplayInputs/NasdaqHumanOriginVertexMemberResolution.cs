using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact closed H4 candles for one human-selected origin vertex.</summary>
public sealed class NasdaqHumanOriginVertexMemberResolution
{
    internal NasdaqHumanOriginVertexMemberResolution(
        NasdaqHumanOriginVertexEpisode episode,
        Candle invalidatingCandle,
        IEnumerable<Candle> selectedMembers)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(invalidatingCandle);
        ArgumentNullException.ThrowIfNull(selectedMembers);

        if (episode.ProviderId != invalidatingCandle.ProviderId
            || episode.Symbol != invalidatingCandle.Symbol
            || episode.Timeframe != invalidatingCandle.Timeframe
            || episode.InvalidatingCandleOpenTimeUtc != invalidatingCandle.OpenTimeUtc)
        {
            throw new ArgumentException("The resolved invalidating candle must match the origin episode.", nameof(invalidatingCandle));
        }

        Episode = episode;
        InvalidatingCandle = invalidatingCandle;
        SelectedMembers = new ReadOnlyCollection<Candle>(selectedMembers.ToArray());
    }

    public NasdaqHumanOriginVertexEpisode Episode { get; }
    public Candle InvalidatingCandle { get; }
    public IReadOnlyList<Candle> SelectedMembers { get; }
}
