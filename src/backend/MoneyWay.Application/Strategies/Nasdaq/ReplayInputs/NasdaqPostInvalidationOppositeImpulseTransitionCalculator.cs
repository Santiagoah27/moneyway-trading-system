using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Advances a provisional opposite impulse by exactly one later closed H4 candle.</summary>
public sealed class NasdaqPostInvalidationOppositeImpulseTransitionCalculator
{
    private readonly StructuralTurnBodyCoordinateCalculator bodyCalculator = new();
    private readonly CorrectionBoundaryObservationCalculator boundaryObservationCalculator = new();
    private readonly CorrectionStartTransitionCalculator correctionStartCalculator = new();

    public NasdaqPostInvalidationOppositeImpulseTransitionResult Evaluate(
        NasdaqPostInvalidationOppositeImpulseState current,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);

        if (current.IsTerminalFrozen)
        {
            throw new ArgumentException("A frozen impulse terminal cannot advance after correction starts.", nameof(current));
        }

        var previous = current.LastProcessedCandle;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || candle.Timeframe != previous.Timeframe)
        {
            throw new ArgumentException("The next candle must belong to the same H4 series.", nameof(candle));
        }

        if (candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
        {
            throw new ArgumentException("The next closed candle must follow the last processed candle without overlap.", nameof(candle));
        }

        var side = current.TerminalSide;
        var extremeSide = side switch
        {
            StructuralTurnBodyCoordinateSide.Lower => CorrectionOriginExtremeSide.Floor,
            StructuralTurnBodyCoordinateSide.Upper => CorrectionOriginExtremeSide.Ceiling,
            _ => throw new ArgumentOutOfRangeException(nameof(current), "The provisional terminal side is not supported."),
        };

        var observation = boundaryObservationCalculator.Evaluate(
            current.ProvisionalTerminal.ProtectionAnchor, candle, extremeSide);
        var body = bodyCalculator.Evaluate([candle], side);
        var structuralPrice = side == StructuralTurnBodyCoordinateSide.Lower
            ? decimal.Min(current.ProvisionalTerminal.StructuralPrice, body.StructuralPrice)
            : decimal.Max(current.ProvisionalTerminal.StructuralPrice, body.StructuralPrice);
        var terminal = new StructuralTurnGeometryResult(
            new StructuralTurnBodyCoordinateResult(structuralPrice, side),
            new StructuralTurnProtectionAnchorResult(
                observation.ResultingExtreme,
                side == StructuralTurnBodyCoordinateSide.Lower
                    ? StructuralTurnProtectionSide.Lower
                    : StructuralTurnProtectionSide.Upper));

        var correctionStart = correctionStartCalculator.Evaluate(observation, candle);
        var startsCorrection = correctionStart.TransitionKind == CorrectionStartTransitionKind.StartCorrection;
        var resultingState = new NasdaqPostInvalidationOppositeImpulseState(
            current.Episode, current.InvalidatingCandle, side, current.OriginGeometry, terminal, candle, startsCorrection);
        return new NasdaqPostInvalidationOppositeImpulseTransitionResult(resultingState, correctionStart);
    }
}
