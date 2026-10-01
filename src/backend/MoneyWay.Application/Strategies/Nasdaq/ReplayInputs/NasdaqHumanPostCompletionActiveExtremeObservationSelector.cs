using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects exact visible member sets for one event without resolving candles or assigning reviewer priority.</summary>
public sealed class NasdaqHumanPostCompletionActiveExtremeObservationSelector
{
    public NasdaqHumanPostCompletionActiveExtremeObservationSelection Select(
        StrategyReplayContext context, NasdaqPostCompletionActiveExtremeMembershipEvent membershipEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(membershipEvent);
        var supporting = context.InputObservations
            .OfType<NasdaqHumanPostCompletionActiveExtremeObservation>()
            .Where(item => item.MembershipEvent.Equals(membershipEvent))
            .OrderBy(item => item.ObservedAtUtc)
            .ThenBy(item => item.SourceReference, StringComparer.Ordinal)
            .ThenBy(item => item.SelectedMemberOpenTimesUtc, MemberSetComparer.Instance)
            .ToArray();
        if (supporting.Length == 0)
            return new NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing();
        var members = supporting[0].SelectedMemberOpenTimesUtc;
        return supporting.All(item => item.SelectedMemberOpenTimesUtc.SequenceEqual(members))
            ? new NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique(members, supporting)
            : new NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict(supporting);
    }

    private sealed class MemberSetComparer : IComparer<IReadOnlyList<DateTimeOffset>>
    {
        public static readonly MemberSetComparer Instance = new();
        public int Compare(IReadOnlyList<DateTimeOffset>? x, IReadOnlyList<DateTimeOffset>? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            for (var index = 0; index < Math.Min(x.Count, y.Count); index++)
            {
                var comparison = x[index].CompareTo(y[index]);
                if (comparison != 0) return comparison;
            }
            return x.Count.CompareTo(y.Count);
        }
    }
}
