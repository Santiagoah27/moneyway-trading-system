using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

internal static class StrictConfirmedPivots
{
    internal static IEnumerable<(Candle Extreme, DateTimeOffset ConfirmedAtUtc, bool High, bool Low)> Find(
        IReadOnlyList<Candle> candles, int leftBars, int rightBars)
    {
        for (var index = leftBars; index < candles.Count - rightBars; index++)
        {
            var left = index - leftBars;
            var right = index + rightBars;
            var extreme = candles[index];
            var high = true;
            var low = true;
            for (var neighbor = left; neighbor <= right; neighbor++)
            {
                if (neighbor < right && candles[neighbor].CloseTimeUtc != candles[neighbor + 1].OpenTimeUtc)
                {
                    high = low = false;
                    break;
                }
                if (neighbor == index) continue;
                high &= extreme.High > candles[neighbor].High;
                low &= extreme.Low < candles[neighbor].Low;
            }
            if (high || low) yield return (extreme, candles[right].CloseTimeUtc, high, low);
        }
    }
}
