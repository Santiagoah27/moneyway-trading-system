using System.Collections.ObjectModel;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A provisional LH/HL formed after invalidation, before the opposite pair is validated.</summary>
public sealed class NasdaqPostInvalidationCandidateState
{
    internal NasdaqPostInvalidationCandidateState(
        NasdaqPostInvalidationCorrectionState correction,
        StructuralTurnGeometryResult candidateGeometry,
        Candle terminalCandle)
    {
        ArgumentNullException.ThrowIfNull(correction);
        ArgumentNullException.ThrowIfNull(candidateGeometry);
        ArgumentNullException.ThrowIfNull(terminalCandle);

        if (candidateGeometry.Side != correction.CorrectionGeometry.Side)
        {
            throw new ArgumentException("Candidate geometry must match the correction side.", nameof(candidateGeometry));
        }

        Episode = correction.Episode;
        InvalidatingCandle = correction.InvalidatingCandle;
        ImpulseTerminalSide = correction.ImpulseTerminalSide;
        CandidateSide = correction.CandidateSide;
        OriginGeometry = correction.OriginGeometry;
        FrozenImpulseTerminal = correction.FrozenImpulseTerminal;
        CorrectionStartCandle = correction.CorrectionStartCandle;
        CorrectionTurnCandles = new ReadOnlyCollection<Candle>(correction.CorrectionTurnCandles.ToArray());
        CandidateGeometry = candidateGeometry;
        TerminalCandle = terminalCandle;
    }

    public NasdaqHumanOriginVertexEpisode Episode { get; }

    public Candle InvalidatingCandle { get; }

    public StructuralTurnBodyCoordinateSide ImpulseTerminalSide { get; }

    public StructuralCandidateExtremeSide CandidateSide { get; }

    public StructuralTurnGeometryResult OriginGeometry { get; }

    public StructuralTurnGeometryResult FrozenImpulseTerminal { get; }

    public Candle CorrectionStartCandle { get; }

    /// <summary>Body members of the completed correction; excludes the terminal impulse candle.</summary>
    public IReadOnlyList<Candle> CorrectionTurnCandles { get; }

    /// <summary>Body geometry from correction members; protection may also include the terminal wick.</summary>
    public StructuralTurnGeometryResult CandidateGeometry { get; }

    /// <summary>The correction terminal and first candle of the next opposite impulse.</summary>
    public Candle TerminalCandle { get; }

    public Candle LastProcessedCandle => TerminalCandle;
}
