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
        LastProcessedCandle = terminalCandle;
    }

    private NasdaqPostInvalidationCandidateState(
        NasdaqPostInvalidationCandidateState current,
        Candle lastProcessedCandle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(lastProcessedCandle);

        var previous = current.LastProcessedCandle;
        if (lastProcessedCandle.ProviderId != previous.ProviderId
            || lastProcessedCandle.Symbol != previous.Symbol
            || lastProcessedCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || lastProcessedCandle.Timeframe != previous.Timeframe
            || lastProcessedCandle.OpenTimeUtc <= previous.OpenTimeUtc
            || lastProcessedCandle.OpenTimeUtc < previous.CloseTimeUtc
            || lastProcessedCandle.CloseTimeUtc <= previous.CloseTimeUtc)
        {
            throw new ArgumentException("The candidate cursor must advance to a later closed candle in the same H4 series.", nameof(lastProcessedCandle));
        }

        Episode = current.Episode;
        InvalidatingCandle = current.InvalidatingCandle;
        ImpulseTerminalSide = current.ImpulseTerminalSide;
        CandidateSide = current.CandidateSide;
        OriginGeometry = current.OriginGeometry;
        FrozenImpulseTerminal = current.FrozenImpulseTerminal;
        CorrectionStartCandle = current.CorrectionStartCandle;
        CorrectionTurnCandles = new ReadOnlyCollection<Candle>(current.CorrectionTurnCandles.ToArray());
        CandidateGeometry = current.CandidateGeometry;
        TerminalCandle = current.TerminalCandle;
        LastProcessedCandle = lastProcessedCandle;
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

    /// <summary>The latest closed H4 candle consumed while this candidate remains active.</summary>
    public Candle LastProcessedCandle { get; }

    /// <summary>Returns an equivalent candidate with a later consumed H4 cursor and unchanged structural fields.</summary>
    public NasdaqPostInvalidationCandidateState WithLastProcessedCandle(Candle lastProcessedCandle) =>
        new(this, lastProcessedCandle);
}
