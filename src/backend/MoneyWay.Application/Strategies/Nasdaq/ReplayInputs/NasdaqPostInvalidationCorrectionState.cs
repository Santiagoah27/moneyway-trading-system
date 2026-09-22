using System.Collections.ObjectModel;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// An active opposite correction after a verified invalidation, before candidate or pair validation.
/// </summary>
public sealed class NasdaqPostInvalidationCorrectionState
{
    internal NasdaqPostInvalidationCorrectionState(
        NasdaqHumanOriginVertexEpisode episode,
        Candle invalidatingCandle,
        StructuralTurnBodyCoordinateSide impulseTerminalSide,
        StructuralCandidateExtremeSide candidateSide,
        StructuralTurnGeometryResult originGeometry,
        StructuralTurnGeometryResult frozenImpulseTerminal,
        Candle correctionStartCandle,
        StructuralTurnGeometryResult correctionGeometry)
        : this(episode, invalidatingCandle, impulseTerminalSide, candidateSide, originGeometry,
            frozenImpulseTerminal, correctionStartCandle, [correctionStartCandle], correctionGeometry,
            correctionStartCandle)
    {
    }

    internal NasdaqPostInvalidationCorrectionState(
        NasdaqHumanOriginVertexEpisode episode,
        Candle invalidatingCandle,
        StructuralTurnBodyCoordinateSide impulseTerminalSide,
        StructuralCandidateExtremeSide candidateSide,
        StructuralTurnGeometryResult originGeometry,
        StructuralTurnGeometryResult frozenImpulseTerminal,
        Candle correctionStartCandle,
        IReadOnlyList<Candle> correctionTurnCandles,
        StructuralTurnGeometryResult correctionGeometry,
        Candle lastProcessedCandle)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(invalidatingCandle);
        ArgumentNullException.ThrowIfNull(originGeometry);
        ArgumentNullException.ThrowIfNull(frozenImpulseTerminal);
        ArgumentNullException.ThrowIfNull(correctionStartCandle);
        ArgumentNullException.ThrowIfNull(correctionTurnCandles);
        ArgumentNullException.ThrowIfNull(correctionGeometry);
        ArgumentNullException.ThrowIfNull(lastProcessedCandle);

        if (correctionTurnCandles.Count == 0
            || !ReferenceEquals(correctionTurnCandles[0], correctionStartCandle)
            || !ReferenceEquals(correctionTurnCandles[^1], lastProcessedCandle))
        {
            throw new ArgumentException("The correction members must begin and end at the supplied causal candles.",
                nameof(correctionTurnCandles));
        }

        if (!Enum.IsDefined(impulseTerminalSide) || !Enum.IsDefined(candidateSide))
        {
            throw new ArgumentOutOfRangeException(nameof(candidateSide), "The correction side is not supported.");
        }

        var expectedCandidateSide = impulseTerminalSide == StructuralTurnBodyCoordinateSide.Lower
            ? StructuralCandidateExtremeSide.Upper
            : StructuralCandidateExtremeSide.Lower;
        var expectedGeometrySide = expectedCandidateSide == StructuralCandidateExtremeSide.Upper
            ? StructuralTurnBodyCoordinateSide.Upper
            : StructuralTurnBodyCoordinateSide.Lower;
        if (candidateSide != expectedCandidateSide
            || correctionGeometry.Side != expectedGeometrySide
            || frozenImpulseTerminal.Side != impulseTerminalSide
            || originGeometry.Side != expectedGeometrySide)
        {
            throw new ArgumentException("The correction, origin, and frozen impulse sides must match the opposite reconstruction.");
        }

        Episode = episode;
        InvalidatingCandle = invalidatingCandle;
        ImpulseTerminalSide = impulseTerminalSide;
        CandidateSide = candidateSide;
        OriginGeometry = originGeometry;
        FrozenImpulseTerminal = frozenImpulseTerminal;
        CorrectionStartCandle = correctionStartCandle;
        CorrectionTurnCandles = new ReadOnlyCollection<Candle>(correctionTurnCandles.ToArray());
        CorrectionGeometry = correctionGeometry;
        LastProcessedCandle = lastProcessedCandle;
    }

    public NasdaqHumanOriginVertexEpisode Episode { get; }

    public Candle InvalidatingCandle { get; }

    public StructuralTurnBodyCoordinateSide ImpulseTerminalSide { get; }

    /// <summary>The side of the possible LH or HL; this is not yet a validated candidate.</summary>
    public StructuralCandidateExtremeSide CandidateSide { get; }

    public StructuralTurnGeometryResult OriginGeometry { get; }

    /// <summary>The frozen body/wick impulse terminal; its structural price is the later second-break reference.</summary>
    public StructuralTurnGeometryResult FrozenImpulseTerminal { get; }

    public Candle CorrectionStartCandle { get; }

    public IReadOnlyList<Candle> CorrectionTurnCandles { get; }

    /// <summary>Geometry of the correction turn so far, initially its sole first candle.</summary>
    public StructuralTurnGeometryResult CorrectionGeometry { get; }

    public Candle LastProcessedCandle { get; }
}
