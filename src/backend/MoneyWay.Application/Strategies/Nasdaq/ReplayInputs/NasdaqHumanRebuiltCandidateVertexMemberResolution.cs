using System.Collections.ObjectModel;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact closed H4 candles for one uniquely selected rebuilt-candidate vertex membership.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexMemberResolution
{
    internal NasdaqHumanRebuiltCandidateVertexMemberResolution(
        NasdaqHumanRebuiltCandidateVertexEpisode episode,
        Candle migrationCandle,
        IEnumerable<Candle> selectedMembers,
        decimal knownProtectionAnchor)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(migrationCandle);
        ArgumentNullException.ThrowIfNull(selectedMembers);

        Episode = episode;
        MigrationCandle = migrationCandle;
        SelectedMembers = new ReadOnlyCollection<Candle>(selectedMembers.ToArray());
        KnownProtectionAnchor = knownProtectionAnchor;
    }

    public NasdaqHumanRebuiltCandidateVertexEpisode Episode { get; }
    public Candle MigrationCandle { get; }
    public IReadOnlyList<Candle> SelectedMembers { get; }
    public decimal KnownProtectionAnchor { get; }
}
