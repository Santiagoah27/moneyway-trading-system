using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class M5FvgCandidateTests
{
    private static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe Frame = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int index) => Start.AddMinutes(5 * index);

    private static Candle Bar(int index, decimal open, decimal high, decimal low, decimal close, bool mirror = false) =>
        mirror ? new(Provider, Symbol, Frame, At(index), At(index + 1), 200 - open, 200 - low, 200 - high, 200 - close, null)
               : new(Provider, Symbol, Frame, At(index), At(index + 1), open, high, low, close, null);

    private static Candle[] Prefix(bool mirror = false, decimal middleClose = 105, decimal lastLow = 110) =>
        [Bar(0, 95, 100, 90, 97, mirror), Bar(1, 97, 115, 95, middleClose, mirror),
         Bar(2, 112, 117, lastLow, 115, mirror)];

    private static M5FvgReport Run(Candle[] candles, M5FvgProfile profile = M5FvgProfile.GeometryOnly,
        DateTimeOffset? cutoff = null, IReadOnlyList<M5FvgClosure>? closures = null) =>
        new EvaluateM5FvgCandidateUseCase().Execute(new(new(Provider, Symbol, Frame, candles), Start,
            cutoff ?? candles[^1].CloseTimeUtc, profile, "synthetic", closures));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StrictThreeBarGeometryBecomesVisibleAtC3Close(bool mirror)
    {
        var prefix = Prefix(mirror);
        var before = Run(prefix, cutoff: At(3).AddTicks(-1));
        Assert.Empty(before.Zones);
        Assert.Equal(0, before.EligibleWindows);
        var result = Run(prefix, cutoff: At(3));
        var zone = Assert.Single(result.Zones);
        Assert.Equal(mirror ? M5FvgDirection.Bearish : M5FvgDirection.Bullish, zone.Formation.OriginalDirection);
        Assert.Equal(mirror ? 90 : 100, zone.Formation.LowerBoundary);
        Assert.Equal(mirror ? 100 : 110, zone.Formation.UpperBoundary);
        Assert.Equal(10, zone.Formation.GapSize);
        Assert.Equal(At(3), zone.Formation.FormationUtc);
        Assert.Equal(M5FvgEvent.Formed, Assert.Single(zone.Observations).Event);
        Assert.False(result.CanonicalAdmissionPermitted);
        Assert.False(result.OperationalSignal);
    }

    [Fact]
    public void EqualityAndMiddleCloseAreProfileSpecific()
    {
        Assert.Empty(Run(Prefix(lastLow: 100)).Zones);
        Assert.Empty(Run(Prefix(true, lastLow: 100)).Zones);
        var geometry = Assert.Single(Run(Prefix(middleClose: 100)).Zones);
        Assert.False(geometry.Formation.MiddleCloseConfirmed);
        Assert.Empty(Run(Prefix(middleClose: 100), M5FvgProfile.MiddleCloseConfirmation).Zones);
        var same = Assert.Single(Run(Prefix()).Zones);
        var narrow = Assert.Single(Run(Prefix(), M5FvgProfile.MiddleCloseConfirmation).Zones);
        Assert.NotEqual(same.Formation.Id, narrow.Formation.Id);
        Assert.Equal(same.Formation.GapSize, narrow.Formation.GapSize);
        Assert.Single(Run(Prefix(lastLow: 100.00001m)).Zones);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReachAndInversionAreIndependentAndInvalidationCannotReinvert(bool mirror)
    {
        Candle[] data =
        [
            .. Prefix(mirror),
            Bar(3, 115, 118, 111, 116, mirror),
            Bar(4, 116, 117, 110, 112, mirror),
            Bar(5, 112, 114, 105, 111, mirror),
            Bar(6, 111, 112, 96, 100, mirror),
            Bar(7, 100, 103, 96, 98, mirror),
            Bar(8, 98, 102, 94, 97, mirror),
            Bar(9, 97, 112, 96, 110, mirror),
            Bar(10, 110, 115, 105, 112, mirror),
            Bar(11, 112, 115, 95, 98, mirror)
        ];
        var zone = Run(data).Zones[0];
        Assert.Equal(M5FvgReach.NotReached, zone.Observations[1].Interaction!.Reach);
        Assert.Equal(M5FvgReach.BoundaryTouched, zone.Observations[2].Interaction!.Reach);
        Assert.Equal(M5FvgReach.PartiallyReached, zone.Observations[3].Interaction!.Reach);
        Assert.Equal(.5m, zone.Observations[3].Interaction!.ReachRatio);
        var full = zone.Observations[4];
        Assert.Equal(M5FvgReach.FullyReached, full.Interaction!.Reach);
        Assert.True(full.Interaction.FarBoundaryContact);
        Assert.Equal(mirror ? M5ZonePosition.AtUpper : M5ZonePosition.AtLower, full.Interaction.ClosePosition);
        Assert.Null(full.Event);
        Assert.Equal(At(8), zone.InversionUtc);
        Assert.Equal(mirror ? M5FvgDirection.Bullish : M5FvgDirection.Bearish, zone.InvertedDirection);
        Assert.Equal(At(11), zone.InvalidationUtc);
        Assert.Equal(At(11), zone.Observations[^1].ObservedAtUtc);
        Assert.Single(zone.Observations, o => o.Event == M5FvgEvent.Inverted);
        Assert.Null(Run(data, cutoff: At(8).AddTicks(-1)).Zones[0].InversionUtc);
        Assert.Null(Run(data, cutoff: At(11).AddTicks(-1)).Zones[0].InvalidationUtc);
    }

    [Fact]
    public void OpeningInsideOrBeyondSeparatesGapGeometryFromStrictClose()
    {
        var inside = Run([.. Prefix(), Bar(3, 105, 113, 98, 102)]).Zones[0].Observations[^1];
        Assert.Equal(M5FvgReach.GapAmbiguous, inside.Interaction!.Reach);
        Assert.Equal(M5ZonePosition.Inside, inside.Interaction.OpenPosition);
        Assert.True(inside.Interaction.OpeningGapUncertainty);
        Assert.Null(inside.Interaction.ReachRatio);
        Assert.Equal(100m, inside.Interaction.IntersectionLower);
        Assert.Equal(110m, inside.Interaction.IntersectionUpper);
        var beyond = Run([.. Prefix(), Bar(3, 95, 97, 92, 96)]).Zones[0].Observations[^1];
        Assert.Equal(M5FvgEvent.Inverted, beyond.Event);
        Assert.Equal(M5FvgReach.GapAmbiguous, beyond.Interaction!.Reach);
        Assert.Null(beyond.Interaction.IntersectionLower);
        Assert.False(beyond.Interaction.NearBoundaryContact);
        Assert.True(beyond.Interaction.OpeningGapUncertainty);
    }

    [Fact]
    public void OverlappingZonesRemainSeparateWhenTwoInvertOnOneClose()
    {
        var initial = Prefix();
        initial[1] = Bar(1, 97, 108, 95, 105);
        var result = Run([.. initial, Bar(3, 120, 125, 118, 121), Bar(4, 121, 123, 90, 95)]);
        var inverted = result.Zones.Where(z => z.InversionUtc == At(5)).ToArray();
        Assert.Equal(2, inverted.Length);
        Assert.NotEqual(inverted[0].Formation.Id, inverted[1].Formation.Id);
        Assert.True(inverted[1].Formation.LowerBoundary < inverted[0].Formation.UpperBoundary);
        Assert.False(result.IntrabarOrderingKnown);
        Assert.All(inverted, z => Assert.True(z.Formation.FormationUtc < At(5)));
        var sweepClose = At(4);
        Assert.True(inverted[0].Formation.FormationUtc < sweepClose && sweepClose < inverted[0].InversionUtc);
    }

    [Fact]
    public void UnknownGapFreezesAffectedZonesAndDocumentedClosureNeverBridgesFormation()
    {
        Candle[] input = [.. Prefix(), Bar(4, 115, 118, 95, 98), Bar(5, 99, 102, 93, 100)];
        var unknown = Run(input);
        var limit = unknown.Zones[0].Observations[^1];
        Assert.Equal(M5FvgEvent.InsufficientEvidence, limit.Event);
        Assert.Equal(At(4), limit.ObservedAtUtc);
        Assert.Null(unknown.Zones[0].InversionUtc);
        Assert.Equal(1, unknown.EligibleWindows);
        Assert.Null(Assert.Single(unknown.SourceGaps).ClosureEvidence);
        Assert.Equal(limit, Run(input, cutoff: At(4)).Zones[0].Observations[^1]);
        var documented = Run(input, closures: [new(At(3), At(4), "calendar-fixture")]);
        Assert.Equal(1, documented.EligibleWindows);
        Assert.Equal("calendar-fixture", Assert.Single(documented.SourceGaps).ClosureEvidence);
        Assert.Equal(At(5), documented.Zones[0].InversionUtc);
    }

    [Fact]
    public void FixedCutoffIsInvariantToAppendedAndMutatedLaterCandles()
    {
        Candle[] history = [.. Prefix(), Bar(3, 115, 116, 94, 97)];
        var cutoff = At(4);
        var baseline = Run(history, cutoff: cutoff);
        var expected = JsonSerializer.Serialize(baseline);
        Assert.Equal(expected, JsonSerializer.Serialize(Run(history, cutoff: cutoff)));
        Assert.Equal(expected, JsonSerializer.Serialize(Run([.. history, Bar(4, 98, 116, 92, 112)], cutoff: cutoff)));
        Assert.Equal(expected, JsonSerializer.Serialize(Run([.. history, Bar(4, 85, 88, 80, 84)], cutoff: cutoff)));
    }
}
