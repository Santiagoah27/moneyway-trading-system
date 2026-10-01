using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A new structural identity derived from one verified completion; retains its branch provenance.</summary>
public sealed class NasdaqPostCompletionEpisode : IEquatable<NasdaqPostCompletionEpisode>
{
    private NasdaqPostCompletionEpisode(NasdaqH4ReconstructionSnapshot.Completed completion)
    {
        Completion = completion;
    }

    public static NasdaqPostCompletionEpisode FromCompleted(NasdaqH4ReconstructionSnapshot.Completed completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var candle = completion.MarketCursor;
        if (completion.StrategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId
            || candle.ProviderId != completion.ProviderId || candle.Symbol != completion.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || !completion.ValidatedCandidate.IsValidated)
            throw new ArgumentException("A verified matching Nasdaq H4 completion is required.", nameof(completion));
        return new(completion);
    }

    public NasdaqH4ReconstructionSnapshot.Completed Completion { get; }
    public NasdaqHumanOriginVertexEpisode PreviousCompletedEpisode => Completion.Episode;
    public Candle ConfirmingCandle => Completion.MarketCursor;
    public DateTimeOffset ConfirmingCandleOpenTimeUtc => ConfirmingCandle.OpenTimeUtc;
    public StructuralTurnBodyCoordinateSide ActiveExtremeSide => Completion.ValidatedCandidate.CandidateSide switch
    {
        StructuralCandidateExtremeSide.Lower => StructuralTurnBodyCoordinateSide.Upper,
        StructuralCandidateExtremeSide.Upper => StructuralTurnBodyCoordinateSide.Lower,
        _ => throw new InvalidOperationException("The completion candidate side is not supported."),
    };

    public bool Equals(NasdaqPostCompletionEpisode? other) => other is not null
        && PreviousCompletedEpisode == other.PreviousCompletedEpisode
        && ConfirmingCandleOpenTimeUtc == other.ConfirmingCandleOpenTimeUtc;
    public override bool Equals(object? obj) => Equals(obj as NasdaqPostCompletionEpisode);
    public override int GetHashCode() => HashCode.Combine(PreviousCompletedEpisode, ConfirmingCandleOpenTimeUtc);
}
