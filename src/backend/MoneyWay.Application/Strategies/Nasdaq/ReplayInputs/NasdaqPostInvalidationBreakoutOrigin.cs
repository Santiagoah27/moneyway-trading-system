using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Retains the causal state from which one frozen-terminal breakout was observed.</summary>
public abstract record NasdaqPostInvalidationBreakoutOrigin
{
    private NasdaqPostInvalidationBreakoutOrigin() { }

    public sealed record Candidate : NasdaqPostInvalidationBreakoutOrigin
    {
        internal Candidate(NasdaqPostInvalidationCandidateState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));

        public NasdaqPostInvalidationCandidateState State { get; }
    }

    public sealed record Rebuild : NasdaqPostInvalidationBreakoutOrigin
    {
        internal Rebuild(Candle priorMigrationCandle, Candle? firstTurnCandle)
        {
            PriorMigrationCandle = priorMigrationCandle ?? throw new ArgumentNullException(nameof(priorMigrationCandle));
            FirstTurnCandle = firstTurnCandle;
        }

        /// <summary>The prior effective migration event retained for rebuilt-vertex provenance.</summary>
        public Candle PriorMigrationCandle { get; }
        public Candle? FirstTurnCandle { get; }
    }
}
