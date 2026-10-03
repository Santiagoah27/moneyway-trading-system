using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact observable H4 lookup and audited migration-wick validity; no member inference or rebuilt geometry.</summary>
internal static class RebuiltCandidateMemberResolver
{
    internal static (IReadOnlyList<Candle> Members, IReadOnlyList<DateTimeOffset> Missing) Resolve(
        IReadOnlyList<DateTimeOffset> memberOpenTimesUtc, Candle migrationCandle,
        StructuralCandidateExtremeSide side, decimal knownProtectionAnchor, StrategyReplayContext context)
    {
        if (!context.TryGetFrame(NasdaqHumanOriginVertexObservation.H4, out var frame))
            return (Array.AsReadOnly(Array.Empty<Candle>()), Array.AsReadOnly(memberOpenTimesUtc.ToArray()));
        var members = new List<Candle>();
        var missing = new List<DateTimeOffset>();
        foreach (var open in memberOpenTimesUtc)
        {
            var member = frame!.AvailableCandles.FirstOrDefault(candle => candle.OpenTimeUtc == open);
            if (member is null) missing.Add(open);
            else members.Add(member);
        }
        if (missing.Count > 0)
            return (Array.AsReadOnly(Array.Empty<Candle>()), Array.AsReadOnly(missing.ToArray()));
        var migration = members.SingleOrDefault(candle => candle.OpenTimeUtc == migrationCandle.OpenTimeUtc)
            ?? throw new InvalidOperationException("The migration candle is not present in the selected rebuilt candidate membership.");
        if (migration.ProviderId != migrationCandle.ProviderId || migration.Symbol != migrationCandle.Symbol
            || migration.Timeframe != migrationCandle.Timeframe || migration.CloseTimeUtc != migrationCandle.CloseTimeUtc)
            throw new InvalidOperationException("The selected migration candle does not match the resolution context.");
        // Validate the already-known anchor, without deriving a new coordinate or complete geometry.
        // Equality requires no stricter selected wick and at least one wick equal to the known anchor.
        var lower = side == StructuralCandidateExtremeSide.Lower;
        if (members.Any(candle => lower ? candle.Low < knownProtectionAnchor : candle.High > knownProtectionAnchor)
            || !members.Any(candle => (lower ? candle.Low : candle.High) == knownProtectionAnchor))
            throw new InvalidOperationException("Selected rebuilt candidate membership does not reproduce the known protection anchor.");
        return (Array.AsReadOnly(members.OrderBy(candle => candle.OpenTimeUtc).ToArray()), Array.AsReadOnly(Array.Empty<DateTimeOffset>()));
    }
}
