using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-selected corrective swing and closed 1M confirmation, without a swing detector.</summary>
public sealed class NasdaqHumanM1RealignmentEvent
{
    public NasdaqHumanM1RealignmentEvent(Candle confirmationCandle, IEnumerable<Candle> correctiveSwingCandles,
        decimal correctiveSwingLevel, NasdaqHumanH4PermittedDirection direction)
    {
        ConfirmationCandle = confirmationCandle ?? throw new ArgumentNullException(nameof(confirmationCandle));
        ArgumentNullException.ThrowIfNull(correctiveSwingCandles);
        if (direction is not (NasdaqHumanH4PermittedDirection.Buy or NasdaqHumanH4PermittedDirection.Sell))
            throw new ArgumentException("Realignment requires a resolved setup direction.", nameof(direction));
        if (confirmationCandle.Timeframe != NasdaqHumanM1CorrectiveRetracementEvent.M1)
            throw new ArgumentException("Confirmation must be an exact 1M candle.", nameof(confirmationCandle));
        var sources = correctiveSwingCandles.ToArray();
        if (sources.Length == 0 || sources.Any(c => c is null
            || c.Timeframe != NasdaqHumanM1CorrectiveRetracementEvent.M1
            || c.ProviderId != confirmationCandle.ProviderId || c.Symbol != confirmationCandle.Symbol
            || c.CloseTimeUtc > confirmationCandle.CloseTimeUtc)
            || sources.Select(c => c.OpenTimeUtc).Distinct().Count() != sources.Length)
            throw new ArgumentException("Selected corrective swing requires exact closed 1M sources in the confirming series.", nameof(correctiveSwingCandles));
        CorrectiveSwingCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.OpenTimeUtc).ToArray());
        CorrectiveSwingLevel = correctiveSwingLevel;
        Direction = direction;
    }

    public Candle ConfirmationCandle { get; }
    public IReadOnlyList<Candle> CorrectiveSwingCandles { get; }
    public decimal CorrectiveSwingLevel { get; }
    public NasdaqHumanH4PermittedDirection Direction { get; }
    public DateTimeOffset RealignmentEffectiveAtUtc => ConfirmationCandle.CloseTimeUtc;
    internal IEnumerable<Candle> SourcesToResolve => CorrectiveSwingCandles.Append(ConfirmationCandle);

    internal bool SameFact(NasdaqHumanM1RealignmentEvent other) => Direction == other.Direction
        && CorrectiveSwingLevel == other.CorrectiveSwingLevel
        && NasdaqStructuralLiquidityReference.SameCandle(ConfirmationCandle, other.ConfirmationCandle)
        && CorrectiveSwingCandles.Count == other.CorrectiveSwingCandles.Count
        && CorrectiveSwingCandles.Zip(other.CorrectiveSwingCandles)
            .All(pair => NasdaqStructuralLiquidityReference.SameCandle(pair.First, pair.Second));
}
