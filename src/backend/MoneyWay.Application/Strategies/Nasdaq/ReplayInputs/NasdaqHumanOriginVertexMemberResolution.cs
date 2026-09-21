using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact closed H4 candles for one human-selected origin vertex.</summary>
public sealed class NasdaqHumanOriginVertexMemberResolution
{
    internal NasdaqHumanOriginVertexMemberResolution(Candle invalidatingCandle, IEnumerable<Candle> selectedMembers)
    {
        InvalidatingCandle = invalidatingCandle;
        SelectedMembers = new ReadOnlyCollection<Candle>(selectedMembers.ToArray());
    }

    public Candle InvalidatingCandle { get; }
    public IReadOnlyList<Candle> SelectedMembers { get; }
}
