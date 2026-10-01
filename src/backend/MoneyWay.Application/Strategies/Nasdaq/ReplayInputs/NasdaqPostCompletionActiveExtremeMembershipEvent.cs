using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Identity of an already-established correction-start boundary; does not search for a turn.</summary>
public sealed class NasdaqPostCompletionActiveExtremeMembershipEvent : IEquatable<NasdaqPostCompletionActiveExtremeMembershipEvent>
{
    public NasdaqPostCompletionActiveExtremeMembershipEvent(NasdaqPostCompletionEpisode episode, Candle correctionStartCandle)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(correctionStartCandle);
        var confirming = episode.ConfirmingCandle;
        if (correctionStartCandle.ProviderId != confirming.ProviderId
            || correctionStartCandle.Symbol != confirming.Symbol || correctionStartCandle.Timeframe != confirming.Timeframe)
            throw new ArgumentException("The correction start must belong to the completion's H4 series.", nameof(correctionStartCandle));
        if (correctionStartCandle.OpenTimeUtc < confirming.OpenTimeUtc
            || correctionStartCandle.OpenTimeUtc > confirming.OpenTimeUtc
                && (correctionStartCandle.OpenTimeUtc < confirming.CloseTimeUtc || correctionStartCandle.CloseTimeUtc <= confirming.CloseTimeUtc))
            throw new ArgumentException("The correction start must be the confirming candle or a later non-overlapping candle.", nameof(correctionStartCandle));
        if (correctionStartCandle.OpenTimeUtc == confirming.OpenTimeUtc
            && (correctionStartCandle.CloseTimeUtc != confirming.CloseTimeUtc
                || correctionStartCandle.Open != confirming.Open || correctionStartCandle.High != confirming.High
                || correctionStartCandle.Low != confirming.Low || correctionStartCandle.Close != confirming.Close
                || correctionStartCandle.Volume != confirming.Volume))
            throw new ArgumentException("The same-candle correction start must preserve the confirming OHLC identity.", nameof(correctionStartCandle));
        var body = new CandleBodyDirectionCalculator().Evaluate(correctionStartCandle);
        var side = episode.ActiveExtremeSide == StructuralTurnBodyCoordinateSide.Upper
            ? CorrectionOriginExtremeSide.Ceiling : CorrectionOriginExtremeSide.Floor;
        if (!new CorrectionBodyDirectionCalculator().Evaluate(side, body))
            throw new ArgumentException("The established correction start must have the exact corrective body direction.", nameof(correctionStartCandle));
        Episode = episode;
        CorrectionStartCandle = correctionStartCandle;
    }

    public NasdaqPostCompletionEpisode Episode { get; }
    public Candle CorrectionStartCandle { get; }
    public DateTimeOffset CorrectionStartCandleOpenTimeUtc => CorrectionStartCandle.OpenTimeUtc;
    public bool Equals(NasdaqPostCompletionActiveExtremeMembershipEvent? other) => other is not null
        && Episode.Equals(other.Episode) && CorrectionStartCandleOpenTimeUtc == other.CorrectionStartCandleOpenTimeUtc;
    public override bool Equals(object? obj) => Equals(obj as NasdaqPostCompletionActiveExtremeMembershipEvent);
    public override int GetHashCode() => HashCode.Combine(Episode, CorrectionStartCandleOpenTimeUtc);
}
