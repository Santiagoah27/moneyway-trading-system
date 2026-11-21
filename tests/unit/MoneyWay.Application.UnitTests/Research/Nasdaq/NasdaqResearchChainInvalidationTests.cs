using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class NasdaqResearchChainInvalidationTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 23, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = DateOnly.FromDateTime(T.DateTime);
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly M5StructuralPivotProfile Profile = new(1, 1);
    private static readonly M5SequenceConfiguration Configuration = new(M5SweepPolicy.StrictAfterSweep,
        M5SequenceWindowPolicy.WindowConfirmationWithPriorSweep, M5SequenceFvgPolicy.FormationAfterConfirmation,
        M5FvgProfile.GeometryOnly, new(8, 30), new(11, 30));
    private static readonly M1RealignmentProfile M1Profile = M1RealignmentProfile.BodyDirectionTurn;

    [Theory]
    [InlineData("observed", "ResearchChainComplete", true, 0)]
    [InlineData("before", "ContextInvalidatedAtRealignment", false, 0)]
    [InlineData("before,before", "ContextInvalidatedAtRealignment", false, 0)]
    [InlineData("equal", "Ambiguous", false, 0)]
    [InlineData("ifvg-equal", "Ambiguous", false, 0)]
    [InlineData("after", "ResearchChainCompleteWithInvalidatedContext", true, 1)]
    [InlineData("observed,before", "ResearchChainComplete", true, 0)]
    [InlineData("observed,equal", "ResearchChainComplete", true, 0)]
    [InlineData("after,observed", "ResearchChainComplete", true, 0)]
    [InlineData("after,after", "ResearchChainCompleteWithInvalidatedContext", true, 1)]
    [InlineData("limited", "InsufficientEvidence", false, 0)]
    [InlineData("limited,before", "InsufficientEvidence", false, 0)]
    [InlineData("limited,observed", "ResearchChainComplete", true, 0)]
    [InlineData("limited-later", "ResearchChainCompleteWithLimitedContext", true, 0)]
    [InlineData("after,limited-later", "ResearchChainCompleteWithLimitedContext", true, 0)]
    [InlineData("foreign,before", "Ambiguous", false, 0)]
    [InlineData("", "InsufficientEvidence", false, 0)]
    public void ContextOutcomeTimestampAndCountsUseTheSameCausalSupport(
        string states, string expectedOutcome, bool completed, int invalidatedAtCutoff)
    {
        var result = Run(states);
        var geometry = Assert.Single(result.Chain.Geometry);
        var context = Assert.Single(geometry.ContextOutcomes);
        Assert.Equal(NasdaqResearchChainOutcome.StructuralReferencesObserved, geometry.DownstreamOutcome);
        Assert.Equal(T, geometry.StructuralReferencesCompletedAtUtc);
        Assert.Equal(1, result.Summary.GeometryCompletedAssociations);
        Assert.Equal((expectedOutcome, completed ? (DateTimeOffset?)T : null, completed ? 1 : 0, invalidatedAtCutoff),
            (context.Outcome.ToString(), context.CompletedAtUtc, result.Summary.CompleteH4ToM1Associations,
                result.Summary.CompleteAssociationsWithInvalidationAtCutoff));
    }

    [Theory]
    [InlineData(false, true, "H4ContextUnavailable", true)]
    [InlineData(true, false, "WaitingForStructuralReferences", false)]
    public void MissingPreparationOrPartialGeometryKeepsIndependentDiagnostics(
        bool h4Available, bool paired, string expected, bool geometryComplete)
    {
        var result = Run("observed", h4Available: h4Available, paired: paired);
        var geometry = Assert.Single(result.Chain.Geometry);
        var context = Assert.Single(geometry.ContextOutcomes);
        Assert.Equal(expected, context.Outcome.ToString());
        Assert.Null(context.CompletedAtUtc);
        Assert.Equal(geometryComplete ? (DateTimeOffset?)T : null, geometry.StructuralReferencesCompletedAtUtc);
        Assert.Equal(0, result.Summary.CompleteH4ToM1Associations);
        Assert.Equal(0, result.Summary.CompleteAssociationsWithInvalidationAtCutoff);
    }

    [Fact]
    public void FutureAppendsAndMutationsKeepTheExactCompletionAtTAndEarlierProjection()
    {
        var at = Run("observed", T);
        var future = Run("after", T);
        var mutated = Run("after-mutated", T);
        Assert.Equal(JsonSerializer.Serialize(at), JsonSerializer.Serialize(future));
        Assert.Equal(JsonSerializer.Serialize(at), JsonSerializer.Serialize(mutated));
        var later = Run("after");
        Assert.Equal(T, Assert.Single(Assert.Single(later.Chain.Geometry).ContextOutcomes).CompletedAtUtc);
        Assert.Equal(T.AddMinutes(5), Assert.Single(later.Chain.Branches).Fvg.InvalidatedAtUtc);
    }

    [Fact]
    public void BindingOrderAndDuplicatesDoNotChangeTheProjection()
    {
        var first = Run("observed,before");
        var twice = Run("observed,before");
        var reordered = Run("observed,before", reorder: true);
        Assert.Equal(2, reordered.Chain.Branches.Count);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(twice));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(reordered));
    }

    private static (NasdaqResearchChain Chain, NasdaqResearchChainSummary Summary) Run(string states,
        DateTimeOffset? cutoff = null, bool h4Available = true, bool paired = true, bool reorder = false)
    {
        var asOf = cutoff ?? T.AddMinutes(20);
        var takeAt = T.AddMinutes(-25);
        var scAt = T.AddMinutes(-20);
        var fvgAt = T.AddMinutes(-15);
        var reference = new ContextualLiquidityReference("fixture-level", "LL", 100, false, Day,
            Provider.Value, Symbol.Value, "1h", [], null, T.AddHours(-1), "Complete", "synthetic fixture");
        var take = new ContextualLiquidityObservation("fixture-take", reference.Id, Bar(takeAt, 5), takeAt,
            "Fixture", "Fixture", new(100, false, true, false, true, false, 1), false, 0, 0, false, null, null);
        var eventSelection = new ContextualScEventSelection("E1", "1.0.0", reference, "Selected", "synthetic fixture",
            take, take, null, null, [], "Complete");
        var episode = new M5SequenceEpisode("fixture-episode", Day, eventSelection);
        var kinds = states.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var triggers = new List<M5SequenceConfirmation>();
        var zones = new List<M5SequenceZone>();
        for (var index = 0; index < Math.Max(1, kinds.Length); index++)
        {
            var kind = kinds.ElementAtOrDefault(index);
            var invalidated = kind switch
            {
                "before" => T.AddMinutes(-5),
                "equal" => T,
                "after" => T.AddMinutes(5),
                "after-mutated" => T.AddMinutes(10),
                _ => (DateTimeOffset?)null
            };
            var limited = kind == "limited" ? T.AddMinutes(-5) :
                kind == "limited-later" ? T.AddMinutes(5) : (DateTimeOffset?)null;
            var origin = kind == "ifvg-equal" ? Zone("fixture-ifvg", scAt.AddMinutes(-10),
                M5FvgDirection.Bearish, scAt, T <= asOf ? T : null, null) : null;
            if (origin is not null) zones.Add(origin);
            triggers.Add(new($"trigger-{index}", origin is null ? M5ConfirmationKind.StructuralChange : M5ConfirmationKind.IFVG,
                M5FvgDirection.Bullish, scAt, "fixture", "fixture", $"trigger-{index}", "fixture",
                "Verified", true, Bar(scAt, 5), origin));
            // Formations are separate, and selection below keeps one exact trigger per FVG.
            zones.Add(Zone($"fvg-{index}", fvgAt, M5FvgDirection.Bullish,
                invalidated <= asOf ? invalidated : null, null, limited <= asOf ? limited : null));
        }
        var selected = EvaluateM5ContextualSequenceUseCase.EvaluateEpisode(episode, Configuration, asOf, true, triggers, zones, []);
        var diagonal = selected.FirstFvgs.Where(f => f.ConfirmationId["trigger-".Length..] == f.Zone.Formation.Id["fvg-".Length..]).ToArray();
        var sequence = new M5ContextualSequenceReport("fixture", asOf, "synthetic fixture",
            [selected with { FirstFvgs = diagonal }], [], []);
        var m1Bars = Enumerable.Range(-14, 15).Select(minute => Bar(T.AddMinutes(minute), 1,
            minute == -1 ? 104 : 103, minute == -1 ? 103 : minute == 0 ? 106 : 103));
        var report = new EvaluateM1RealignmentCandidateUseCase().Execute(sequence,
            new(Provider, Symbol, new(1, TimeframeUnit.Minute), m1Bars), [M1Profile]);
        var scan = Assert.Single(report.Scans);
        Assert.Equal(T, scan.FirstRealignmentUtc);
        var original = Assert.Single(report.Associations);
        var timelines = report.ZoneTimelines.ToDictionary(p => p.Key, p => p.Value);
        if (kinds.Contains("foreign"))
        {
            var key = original.Bindings.Single(b => b.FvgId == "fvg-0").FvgZoneTimelineId;
            timelines[key] = timelines[key] with { ScanId = "foreign-scan" };
        }
        var bindings = kinds.Length == 0 ? [] : reorder ? [.. original.Bindings.Reverse(), original.Bindings[0]] : original.Bindings;
        var m1 = report with { Associations = [original with { Bindings = bindings }], ZoneTimelines = timelines };
        var tpProfile = new StructuralTargetProfile(StructuralTargetPolicy.NearestConfirmedM5Swing, Profile);
        var sl = new M5StructuralStopAssociation(episode.Id, Configuration, M1Profile, scan.Anchor, scan.Id,
            Assert.Single(scan.FirstRealignmentIds), T, M1WindowPosition.InsideWindow, Profile, "fixture-sl",
            M5StructuralStopOutcome.StructuralReferenceIdentified, "fixture", []);
        var target = new StructuralTakeProfitResearchReport("fixture", asOf, Provider.Value, Symbol.Value, "fixture",
            [tpProfile], new Dictionary<string, StructuralTargetEvaluation>(),
            new Dictionary<string, StructuralRewardRiskComparison>
            {
                ["fixture-pair"] = new("fixture-tp", "fixture-sl", Profile,
                    [new("fixture-target", 112, 6, "fixture-stop", 102, 4, 1.5m, "fixture")])
            },
            [new(sl, tpProfile, paired ? "fixture-tp" : null, paired ? "fixture-pair" : null,
                paired ? StructuralTargetOutcome.TargetIdentified : StructuralTargetOutcome.NoHistoricalReference, "fixture")]);
        var preparation = new NasdaqResearchPreparation(T.AddHours(-1), null,
            [new("fixture-h4", new("fixture", 1, 1), 1, h4Available, "fixture", null)]);
        var chains = NasdaqResearchChainProjection.Build(sequence, m1, target, preparation);
        return (Assert.Single(chains), NasdaqResearchChainProjection.Summarize(chains, preparation, sequence, target));
    }

    private static M5SequenceZone Zone(string id, DateTimeOffset at, M5FvgDirection direction,
        DateTimeOffset? inversion, DateTimeOffset? invalidated, DateTimeOffset? limited) =>
        new(new(id, Configuration.Profile, direction, Bar(at.AddMinutes(-10), 5),
            Bar(at.AddMinutes(-5), 5), Bar(at, 5), 100, 104, true, "synthetic fixture"), inversion, invalidated, limited);

    private static Candle Bar(DateTimeOffset at, int minutes, decimal open = 103, decimal close = 103) =>
        new(Provider, Symbol, new(minutes, TimeframeUnit.Minute), at.AddMinutes(-minutes), at, open, 109, 98, close, null);
}
