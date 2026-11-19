using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

/// <summary>One isolated bounded episode, four hypotheses, no canonical interfaces or operational side effects.</summary>
public sealed class CompareM5SwingHypothesesUseCase
{
    public static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    public static IReadOnlyList<M5SwingHypothesisDefinition> Algorithms { get; } = Array.AsReadOnly(new[] {
        new M5SwingHypothesisDefinition(M5Hypothesis.CandleColor, "EXP-M5-SWING-A-V1", "1.0.0", "Ascending Close < Open; descending Close > Open"),
        new(M5Hypothesis.PriceStructure, "EXP-M5-SWING-B-V1", "1.0.0", "Ascending Low < Previous.Low; descending High > Previous.High"),
        new(M5Hypothesis.CombinedStrict, "EXP-M5-SWING-C-V1", "1.0.0", "A AND B on the same start candle"),
        new(M5Hypothesis.CombinedFlexible, "EXP-M5-SWING-D-V1", "1.0.0", "A OR B on the same start candle") });
    public static IReadOnlyList<string> Assumptions { get; } = Array.AsReadOnly(new[] {
        "MoneyWay experimental hypotheses; mirrored directions are not independently mentor-confirmed.",
        "Closed M5 only; explicit source-bound anchor; fixed actual anchor High ascending or Low descending.",
        "First qualifying start; previous means previous supplied closed candle, beginning with the anchor.",
        "Minimum Low ascending / maximum High descending from start through pre-continuation candles; exact ties retain every identity.",
        "First strict-close continuation terminates this one episode; exclude its candle from aggregation.",
        "Start plus continuation before any established pullback abstains; no pullback before continuation also abstains.",
        "Initialization must be available before processing the episode; do not retrospectively authorize already closed episode candles.",
        "No gap filling, automatic initialization, resets, double-touch, multiple cycles, optimization, ranking or operational promotion." });
    public M5SwingComparisonReport Execute(M5SwingExperimentInput input)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Series);
        Text(input.SeriesId); Text(input.Provenance);
        if (input.CutoffUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(input.Direction) || !Enum.IsDefined(input.DataOrigin)) throw new ArgumentException("Explicit UTC cutoff and supported research declarations required.");
        var context = new M5SwingExperimentContext(input.SeriesId, input.Series.ProviderId.Value, input.Series.Symbol.Value, input.Series.Timeframe.ToString(),
            input.Direction, input.CutoffUtc, input.Initialization, null, null, input.DataOrigin, input.Provenance);
        if (input.Series.Timeframe != M5) return Unavailable(context, M5ExperimentalStatus.RejectedInput, "Only M5 is supported.");
        var init = input.Initialization;
        if (init is null) return Unavailable(context, M5ExperimentalStatus.AbstainedMissingInitialization, "No explicit initialization supplied; no anchor is selected.");
        Text(init.ReferenceId); Text(init.SourceReference);
        if (init.ProviderId != input.Series.ProviderId || init.Symbol != input.Series.Symbol || init.Timeframe != M5
            || init.AnchorOpenTimeUtc.Offset != TimeSpan.Zero || init.AvailableAtUtc.Offset != TimeSpan.Zero)
            return Unavailable(context, M5ExperimentalStatus.RejectedInput, "Initialization identity/timeframe/UTC does not match the input.");
        if (init.AvailableAtUtc > input.CutoffUtc) return Unavailable(context, M5ExperimentalStatus.AbstainedInitializationUnavailable, "Initialization is not available at the cutoff.");
        // Project visibility first. Neither future candle content nor future candle counts enter the output.
        var visible = input.Series.Candles.Where(c => c.CloseTimeUtc <= input.CutoffUtc).ToArray();
        var anchor = visible.SingleOrDefault(c => c.OpenTimeUtc == init.AnchorOpenTimeUtc);
        if (anchor is null) return Unavailable(context, M5ExperimentalStatus.AbstainedInitializationUnavailable, "The anchor is not observable at the cutoff.");
        if (init.AvailableAtUtc < anchor.CloseTimeUtc) return Unavailable(context, M5ExperimentalStatus.RejectedInput, "Initialization availability precedes anchor closure.");
        var episode = visible.Where(c => c.OpenTimeUtc > anchor.OpenTimeUtc).ToArray();
        if (episode.Any(c => c.CloseTimeUtc < init.AvailableAtUtc))
            return Unavailable(context, M5ExperimentalStatus.AbstainedLateInitialization, "Episode candles already closed before initialization became available; no retrospective authorization.");
        context = context with { InitialImpulsePrice = input.Direction == M5ImpulseDirection.Ascending ? anchor.High : anchor.Low, InitialImpulseClosedAtUtc = anchor.CloseTimeUtc };
        var results = Algorithms.Select(a => Evaluate(a, context, anchor, episode)).ToArray();
        var pairs = new List<M5HypothesisPairComparison>();
        for (var i = 0; i < results.Length; i++) for (var j = i + 1; j < results.Length; j++) pairs.Add(Compare(results[i], results[j]));
        return new(context, Assumptions, Array.AsReadOnly(results), pairs.AsReadOnly(), Array.AsReadOnly(visible.Where(c => c.OpenTimeUtc >= anchor.OpenTimeUtc).ToArray()));
    }
    private static void Text(string value)
    { if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > 2048 || value.Any(char.IsControl)) throw new ArgumentException("Explicit bounded research identity/provenance required."); }
    private static M5SwingComparisonReport Unavailable(M5SwingExperimentContext context, M5ExperimentalStatus status, string reason)
    {
        var rows = Algorithms.Select(a => new M5SwingHypothesisResult(a, context, status, reason, null, null, [], [], null)).ToArray();
        var pairs = new List<M5HypothesisPairComparison>();
        for (var i = 0; i < rows.Length; i++) for (var j = i + 1; j < rows.Length; j++) pairs.Add(Compare(rows[i], rows[j]));
        return new(context, Assumptions, Array.AsReadOnly(rows), pairs.AsReadOnly(), []);
    }
    private static M5SwingHypothesisResult Evaluate(M5SwingHypothesisDefinition algorithm, M5SwingExperimentContext context, Candle anchor, Candle[] episode)
    {
        var body = new CandleBodyDirectionCalculator(); var breaks = new StructuralBodyCloseBreakCalculator(); var extreme = new StructuralCandidateExtremeCalculator();
        var ascending = context.Direction == M5ImpulseDirection.Ascending;
        var previous = anchor; Candle? start = null; decimal? price = null;
        var members = new List<Candle>(); var ties = new List<Candle>();
        M5SwingHypothesisResult Result(M5ExperimentalStatus status, string reason, Candle? continuation = null) =>
            new(algorithm, context, status, reason, start, price, ties.AsReadOnly(), members.AsReadOnly(), continuation);
        foreach (var candle in episode)
        {
            var a = body.Evaluate(candle) == (ascending ? CandleBodyDirection.Bearish : CandleBodyDirection.Bullish);
            var b = ascending ? candle.Low < previous.Low : candle.High > previous.High;
            var begins = algorithm.Hypothesis switch { M5Hypothesis.CandleColor => a, M5Hypothesis.PriceStructure => b, M5Hypothesis.CombinedStrict => a && b, _ => a || b };
            var continuation = breaks.Evaluate(candle, context.InitialImpulsePrice!.Value, ascending ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower).IsConfirmed;
            if (continuation)
                return start is null ? Result(begins ? M5ExperimentalStatus.AbstainedAmbiguousStartAndContinuation : M5ExperimentalStatus.AbstainedNoPullbackBeforeContinuation,
                    begins ? "A/B start condition and continuation share one candle; intrabar order is unknown." : "First continuation arrived without an established pullback; the bounded episode ends.", candle)
                    : Result(M5ExperimentalStatus.ExperimentalReferenceConfirmed, "A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.", candle);
            if (start is null && begins) start = candle;
            if (start is not null)
            {
                members.Add(candle); var observed = ascending ? candle.Low : candle.High;
                var next = price is null ? observed : extreme.Evaluate(price.Value, observed, ascending ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper).ResultingExtreme;
                if (price is null || next != price) { price = next; ties.Clear(); }
                if (observed == price) ties.Add(candle);
            }
            previous = candle;
        }
        return Result(start is null ? M5ExperimentalStatus.AwaitingPullbackStart : M5ExperimentalStatus.AwaitingContinuation,
            start is null ? "No qualifying pullback start or strict continuation is visible." : "Only a provisional reference exists; no strict continuation is visible.");
    }
    private static M5HypothesisPairComparison Compare(M5SwingHypothesisResult left, M5SwingHypothesisResult right)
    {
        var sameReference = left.ProposedReferencePrice == right.ProposedReferencePrice && left.ReferenceCandles.Select(c => c.OpenTimeUtc).SequenceEqual(right.ReferenceCandles.Select(c => c.OpenTimeUtc));
        var sameStart = left.PullbackStart?.OpenTimeUtc == right.PullbackStart?.OpenTimeUtc;
        var same = left.Status == right.Status && sameReference && sameStart && left.ConfirmationAtUtc == right.ConfirmationAtUtc;
        bool? confirmedAgreement = left.IsExperimentalReferenceConfirmed && right.IsExperimentalReferenceConfirmed ? sameReference && left.ConfirmationAtUtc == right.ConfirmationAtUtc : null;
        return new(left.Algorithm.AlgorithmId, right.Algorithm.AlgorithmId, same, confirmedAgreement,
            left.ConfirmationLatencyFromAnchor is { } la && right.ConfirmationLatencyFromAnchor is { } ra ? la - ra : null,
            left.PullbackToConfirmationLatency is { } lp && right.PullbackToConfirmationLatency is { } rp ? lp - rp : null,
            $"{left.Algorithm.StartCondition} versus {right.Algorithm.StartCondition}. Start agreement={sameStart}; reference agreement={sameReference}; statuses={left.Status}/{right.Status}. Same confirmed references can have different start-to-confirmation latency. Agreement is not evidence of accuracy or profit.");
    }
}
