using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

internal static class M5LabFixtures
{
    internal static readonly MarketDataProviderId Provider = new("moneyway-synthetic-lab");
    internal static readonly MarketSymbol Symbol = new("US100");
    internal static DateTimeOffset At(int index) => new DateTimeOffset(2026, 11, 19, 13, 0, 0, TimeSpan.Zero).AddMinutes(index * 5);
    internal static Candle Bar(int index, decimal open, decimal high, decimal low, decimal close) => new(Provider, Symbol, CompareM5SwingHypothesesUseCase.M5, At(index), At(index + 1), open, high, low, close, null);
    internal static M5SwingExperimentInput Input(IEnumerable<Candle> candles, bool descending = false, DateTimeOffset? cutoff = null)
    {
        var rows = candles.Select(c => !descending ? c : Bar((int)((c.OpenTimeUtc - At(0)).TotalMinutes / 5), 200 - c.Open, 200 - c.Low, 200 - c.High, 200 - c.Close)).ToArray();
        return new("synthetic-explicit-episode", new(Provider, Symbol, CompareM5SwingHypothesesUseCase.M5, rows), descending ? M5ImpulseDirection.Descending : M5ImpulseDirection.Ascending,
            cutoff ?? rows.Last().CloseTimeUtc, new("synthetic-explicit-anchor", Provider, Symbol, CompareM5SwingHypothesesUseCase.M5, At(0), At(1), "synthetic:initial impulse candle"), M5ResearchDataOrigin.Synthetic, "Controlled synthetic OHLC, not authentic market activity or mentor labels");
    }
    internal static M5SwingExperimentInput Disagreement(bool descending = false) => Input([
        Bar(0,100,110,95,108), Bar(1,108,109,96,107), Bar(2,100,108,90,106), Bar(3,103,109,94,108), Bar(4,108,109,93,105), Bar(5,105,112,80,110.01m)], descending);
}
