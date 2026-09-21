using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Immutable opposite impulse established by one verified structural invalidation.
/// It contains neither a correction turn nor a validated opposite structural pair.
/// </summary>
public sealed class NasdaqPostInvalidationOppositeImpulseState
{
    internal NasdaqPostInvalidationOppositeImpulseState(
        Candle invalidatingCandle,
        StructuralTurnBodyCoordinateSide terminalSide,
        StructuralTurnGeometryResult originGeometry,
        StructuralTurnGeometryResult provisionalTerminal)
        : this(invalidatingCandle, terminalSide, originGeometry, provisionalTerminal, invalidatingCandle, false)
    {
    }

    internal NasdaqPostInvalidationOppositeImpulseState(
        Candle invalidatingCandle,
        StructuralTurnBodyCoordinateSide terminalSide,
        StructuralTurnGeometryResult originGeometry,
        StructuralTurnGeometryResult provisionalTerminal,
        Candle lastProcessedCandle,
        bool isTerminalFrozen)
    {
        ArgumentNullException.ThrowIfNull(invalidatingCandle);
        ArgumentNullException.ThrowIfNull(originGeometry);
        ArgumentNullException.ThrowIfNull(provisionalTerminal);
        ArgumentNullException.ThrowIfNull(lastProcessedCandle);

        if (!Enum.IsDefined(terminalSide))
        {
            throw new ArgumentOutOfRangeException(nameof(terminalSide), terminalSide, "The provisional terminal side is not supported.");
        }

        if (provisionalTerminal.Side != terminalSide || originGeometry.Side == terminalSide)
        {
            throw new ArgumentException("Origin and provisional terminal geometry must have opposite structural sides.");
        }

        InvalidatingCandle = invalidatingCandle;
        TerminalSide = terminalSide;
        OriginGeometry = originGeometry;
        ProvisionalTerminal = provisionalTerminal;
        LastProcessedCandle = lastProcessedCandle;
        IsTerminalFrozen = isTerminalFrozen;
    }

    /// <summary>The exact closed candle that invalidated the former structural side.</summary>
    public Candle InvalidatingCandle { get; }

    /// <summary>The Low/High side of the emerging provisional terminal.</summary>
    public StructuralTurnBodyCoordinateSide TerminalSide { get; }

    /// <summary>Previously resolved human-reviewed departure-vertex geometry.</summary>
    public StructuralTurnGeometryResult OriginGeometry { get; }

    /// <summary>Provisional terminal geometry accumulated through the last processed candle.</summary>
    public StructuralTurnGeometryResult ProvisionalTerminal { get; }

    /// <summary>The last causal candle included in the emerging impulse.</summary>
    public Candle LastProcessedCandle { get; }

    /// <summary>The final impulse terminal is frozen once this candle starts correction.</summary>
    public bool IsTerminalFrozen { get; }
}
