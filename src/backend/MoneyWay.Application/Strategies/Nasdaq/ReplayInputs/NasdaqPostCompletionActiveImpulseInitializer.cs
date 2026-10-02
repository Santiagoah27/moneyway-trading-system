using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Initializes only the trend-body handoff, consuming no additional market candle.</summary>
public sealed class NasdaqPostCompletionActiveImpulseInitializer
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();

    public NasdaqPostCompletionActiveImpulseState Initialize(NasdaqH4ReconstructionSnapshot.Completed completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var episode = NasdaqPostCompletionEpisode.FromCompleted(completion);
        var expectedBody = episode.ActiveExtremeSide switch
        {
            StructuralTurnBodyCoordinateSide.Upper => CandleBodyDirection.Bullish,
            StructuralTurnBodyCoordinateSide.Lower => CandleBodyDirection.Bearish,
            _ => throw new ArgumentOutOfRangeException(nameof(completion)),
        };
        if (bodyDirectionCalculator.Evaluate(episode.ConfirmingCandle) != expectedBody)
            throw new ArgumentException("This initializer requires a trend-directional confirming body; opposite and neutral bodies are outside its scope.", nameof(completion));

        return new(episode);
    }
}
