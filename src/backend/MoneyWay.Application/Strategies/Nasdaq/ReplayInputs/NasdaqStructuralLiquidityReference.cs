using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>An established human-selected liquidity point. Factories preserve model-specific coordinate authority.</summary>
/// <remarks>Construction derives/checks values but grants no replay visibility. The selector must resolve all sources
/// against its closed snapshot before using the reference. SourceReference is provenance, not semantic authority.</remarks>
public sealed class NasdaqStructuralLiquidityReference : IEquatable<NasdaqStructuralLiquidityReference>, IComparable<NasdaqStructuralLiquidityReference>
{
    private readonly string semanticKey;

    private NasdaqStructuralLiquidityReference(NasdaqHumanH4StructuralRole role, NasdaqStructuralLiquidityModel model,
        IEnumerable<Candle> members, IEnumerable<Candle> sources, decimal structuralPrice, string sourceReference,
        StructuralBodyCloseBreakResult? validation = null, NasdaqDirectionalMigrationBreakoutCompletionResult? directional = null,
        NasdaqHumanStructuralPriceBreakoutCompletionResult? humanCollision = null,
        NasdaqHumanPostCompletionActiveExtremeGeometryResult? extreme = null)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        ValidateProvenance(sourceReference);
        var memberSnapshot = Snapshot(members, allowEmpty: humanCollision is not null);
        var sourceSnapshot = Snapshot(sources, allowRepeatedSources: true);
        var first = sourceSnapshot[0];
        if (first.Timeframe != new Timeframe(1, TimeframeUnit.Hour) && first.Timeframe != NasdaqHumanH4ContextObservation.H4)
            throw new ArgumentException("Only 1H/4H structural references are admitted.", nameof(sources));
        if (memberSnapshot.Concat(sourceSnapshot).Any(candle => candle.ProviderId != first.ProviderId
            || candle.Symbol != first.Symbol || candle.Timeframe != first.Timeframe))
            throw new ArgumentException("Every source must belong to the same exact series.", nameof(sources));
        Role = role;
        Model = model;
        Timeframe = first.Timeframe;
        ProviderId = first.ProviderId;
        Symbol = first.Symbol;
        StructuralPrice = structuralPrice;
        SourceReference = sourceReference;
        Members = new ReadOnlyCollection<Candle>(memberSnapshot);
        SourceCandles = new ReadOnlyCollection<Candle>(sourceSnapshot);
        Validation = validation;
        DirectionalCompletion = directional;
        HumanCollisionCompletion = humanCollision;
        ActiveExtremeGeometry = extreme;
        semanticKey = JsonSerializer.Serialize(new
        {
            Role,
            Model,
            Price = Number(StructuralPrice),
            Members = Members.Select(CandleKey),
            Sources = SourceCandles.Select(CandleKey),
            Validation = validation is null ? null : new
            {
                Level = Number(validation.ReferenceLevel),
                validation.Direction,
                validation.AsOfUtc,
            },
            Episode = EpisodeIdentity,
            Collision = CollisionKey(directional?.Breakout ?? humanCollision?.Breakout),
            Completion = extreme?.MemberResolution.MembershipEvent.Episode.ConfirmingCandleOpenTimeUtc,
            Turn = extreme?.MemberResolution.MembershipEvent.CorrectionStartCandleOpenTimeUtc,
            Predecessor = extreme is null ? null : new
            {
                Branch = extreme.MemberResolution.MembershipEvent.Episode.Completion.GetType().Name,
                ProtectedPrice = Number(extreme.MemberResolution.MembershipEvent.Episode.Completion.ValidatedCandidate.CandidateGeometry.StructuralPrice),
                ProtectedAnchor = Number(extreme.MemberResolution.MembershipEvent.Episode.Completion.ValidatedCandidate.CandidateGeometry.ProtectionAnchor),
                BrokenReference = Number(extreme.MemberResolution.MembershipEvent.Episode.Completion.ValidatedCandidate.BreakObservation.ReferenceLevel),
            },
        });
    }

    public NasdaqHumanH4StructuralRole Role { get; }
    public NasdaqStructuralLiquidityModel Model { get; }
    public NasdaqStructuralLiquiditySide Side => Role is NasdaqHumanH4StructuralRole.HigherHigh or NasdaqHumanH4StructuralRole.LowerHigh
        ? NasdaqStructuralLiquiditySide.High : NasdaqStructuralLiquiditySide.Low;
    public NasdaqStructuralLiquidityPriceOwnership PriceOwnership => Model is NasdaqStructuralLiquidityModel.HumanCollision008
        or NasdaqStructuralLiquidityModel.HumanEstablishedExtreme
        ? NasdaqStructuralLiquidityPriceOwnership.HumanDocumented : NasdaqStructuralLiquidityPriceOwnership.FormulaBacked;
    public Timeframe Timeframe { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public decimal StructuralPrice { get; }
    public string SourceReference { get; }
    /// <summary>Exact body members; empty only for 008, whose price has no invented body owner.</summary>
    public IReadOnlyList<Candle> Members { get; }
    public IReadOnlyList<Candle> SourceCandles { get; }
    public StructuralBodyCloseBreakResult? Validation { get; }
    public NasdaqDirectionalMigrationBreakoutCompletionResult? DirectionalCompletion { get; }
    public NasdaqHumanStructuralPriceBreakoutCompletionResult? HumanCollisionCompletion { get; }
    public NasdaqHumanPostCompletionActiveExtremeGeometryResult? ActiveExtremeGeometry { get; }
    internal NasdaqHumanOriginVertexEpisode? EpisodeIdentity => DirectionalCompletion?.Breakout.Episode
        ?? HumanCollisionCompletion?.Breakout.Episode
        ?? ActiveExtremeGeometry?.MemberResolution.MembershipEvent.Episode.PreviousCompletedEpisode;
    internal string ProvenanceKey => JsonSerializer.Serialize(new
    {
        SourceReference,
        CollisionSupport = SupportKey(HumanCollisionCompletion?.HumanPriceSelection.SupportingObservations),
        ClusterSupport = SupportKey(ActiveExtremeGeometry?.MemberResolution.Selection.SupportingObservations),
        PredecessorSupport = ActiveExtremeGeometry?.MemberResolution.MembershipEvent.Episode.Completion
            is NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice predecessor
                ? SupportKey(predecessor.Result.HumanPriceSelection.SupportingObservations) : null,
    });

    private static object? SupportKey(IEnumerable<IStrategyReplayInputObservation>? supporting) => supporting?
        .OrderBy(item => item.ObservedAtUtc).ThenBy(item => item.SourceReference, StringComparer.Ordinal)
        .Select(item => new { item.ObservedAtUtc, item.SourceReference }).ToArray();

    public static NasdaqStructuralLiquidityReference OrdinaryTurn(NasdaqHumanH4StructuralRole role, IEnumerable<Candle> members,
        string sourceReference, StructuralBodyCloseBreakResult? validation = null, decimal? suppliedPrice = null)
    {
        var side = CandidateSide(role);
        var snapshot = Snapshot(members);
        CheckValidation(validation, side);
        if (validation is not null && snapshot.Any(member => member.CloseTimeUtc > validation.Candle!.OpenTimeUtc))
            throw new ArgumentException("An ordinary correction turn cannot include its confirming candle or later members.", nameof(members));
        var price = new StructuralTurnBodyCoordinateCalculator().Evaluate(snapshot, side).StructuralPrice;
        CheckPrice(suppliedPrice, price);
        return new(role, NasdaqStructuralLiquidityModel.OrdinaryTurn, snapshot,
            validation is null ? snapshot : snapshot.Append(validation.Candle!), price, sourceReference, validation);
    }

    public static NasdaqStructuralLiquidityReference ExpansionOrigin(NasdaqHumanH4StructuralRole role,
        StructuralBodyCloseBreakResult validation, string sourceReference, decimal? suppliedPrice = null)
    {
        ArgumentNullException.ThrowIfNull(validation);
        var side = CandidateSide(role);
        CheckValidation(validation, side);
        var candle = validation.Candle!;
        var body = new CandleBodyDirectionCalculator().Evaluate(candle);
        if (body != (side == StructuralTurnBodyCoordinateSide.Lower ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish))
            throw new ArgumentException("The single expansive origin must have the qualifying directional body.", nameof(validation));
        CheckPrice(suppliedPrice, candle.Open);
        return new(role, NasdaqStructuralLiquidityModel.ExpansionOrigin, [candle], [candle], candle.Open, sourceReference, validation);
    }

    public static NasdaqStructuralLiquidityReference DirectionalCollision(NasdaqDirectionalMigrationBreakoutCompletionResult completion,
        string sourceReference, decimal? suppliedPrice = null)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var canonical = new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(completion.Breakout);
        CheckPrice(suppliedPrice, canonical.CandidateGeometry.StructuralPrice);
        return new(CandidateRole(completion.Breakout.CandidateSide), NasdaqStructuralLiquidityModel.DirectionalCollision007,
            [completion.Breakout.ValidatingCandle], BreakoutSources(completion.Breakout), canonical.CandidateGeometry.StructuralPrice,
            sourceReference, completion.ValidatedCandidate.BreakObservation, directional: completion);
    }

    public static NasdaqStructuralLiquidityReference HumanCollision(NasdaqHumanStructuralPriceBreakoutCompletionResult completion,
        string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(completion);
        // This canonical completion checks the verified 008 event, not the human price against OHLC.
        var canonical = new NasdaqHumanStructuralPriceBreakoutCompletionCalculator().Evaluate(completion.Breakout, completion.HumanPriceSelection);
        return new(CandidateRole(completion.Breakout.CandidateSide), NasdaqStructuralLiquidityModel.HumanCollision008,
            [], BreakoutSources(completion.Breakout), canonical.CandidateGeometry.StructuralPrice,
            sourceReference, completion.ValidatedCandidate.BreakObservation, humanCollision: completion);
    }

    public static NasdaqStructuralLiquidityReference PostCompletionExtreme(NasdaqHumanPostCompletionActiveExtremeGeometryResult geometry,
        string sourceReference, decimal? suppliedPrice = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var canonical = new NasdaqHumanPostCompletionActiveExtremeGeometryCalculator().Evaluate(geometry.MemberResolution);
        CheckPrice(suppliedPrice, canonical.Geometry.StructuralPrice);
        var evt = geometry.MemberResolution.MembershipEvent;
        var role = evt.Episode.ActiveExtremeSide == StructuralTurnBodyCoordinateSide.Upper
            ? NasdaqHumanH4StructuralRole.HigherHigh : NasdaqHumanH4StructuralRole.LowerLow;
        var sources = geometry.MemberResolution.SelectedMembers.Append(evt.CorrectionStartCandle)
            .Concat(CompletionSources(evt.Episode.Completion));
        return new(role, NasdaqStructuralLiquidityModel.PostCompletionExtreme, geometry.MemberResolution.SelectedMembers,
            sources, canonical.Geometry.StructuralPrice, sourceReference, extreme: geometry);
    }

    public static NasdaqStructuralLiquidityReference HumanEstablishedExtreme(NasdaqHumanH4StructuralRole role,
        IEnumerable<Candle> members, decimal structuralPrice, string sourceReference, StructuralBodyCloseBreakResult? validation = null)
    {
        if (role is not (NasdaqHumanH4StructuralRole.HigherHigh or NasdaqHumanH4StructuralRole.LowerLow))
            throw new ArgumentException("Only established HH/LL outside the formula-backed final-cluster model use this human contract.", nameof(role));
        var snapshot = Snapshot(members);
        CheckValidation(validation, role == NasdaqHumanH4StructuralRole.HigherHigh
            ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper);
        return new(role, NasdaqStructuralLiquidityModel.HumanEstablishedExtreme, snapshot,
            validation is null ? snapshot : snapshot.Append(validation.Candle!), structuralPrice, sourceReference, validation);
    }

    internal bool MatchesSession(NasdaqDemoSessionIdentity session) => ProviderId == session.ProviderId && Symbol == session.Symbol
        && (EpisodeIdentity is not { } episode || episode.StrategyId == session.StrategyId && episode.StrategyVersion == session.StrategyVersion
            && episode.ProviderId == session.ProviderId && episode.Symbol == session.Symbol);

    internal bool IsObservable(StrategyReplayContext context, DateTimeOffset effectiveAtUtc)
    {
        if (!context.TryGetFrame(Timeframe, out var frame)) return false;
        var closed = frame!.AvailableCandles.ToDictionary(candle => candle.OpenTimeUtc);
        if (SourceCandles.Any(source => source.CloseTimeUtc > effectiveAtUtc || !closed.TryGetValue(source.OpenTimeUtc, out var actual)
            || !SameCandle(source, actual))) return false;
        if (HumanCollisionCompletion is { } collision && collision.HumanPriceSelection.SupportingObservations.Any(item => item.ObservedAtUtc > effectiveAtUtc))
            return false;
        if (HumanCollisionCompletion is { } human && !HumanPriceStillCompatible(context, human)) return false;
        if (ActiveExtremeGeometry is { } extreme)
        {
            if (extreme.MemberResolution.Selection.SupportingObservations.Any(item => item.ObservedAtUtc > effectiveAtUtc)) return false;
            var visible = new NasdaqHumanPostCompletionActiveExtremeObservationSelector().Select(context, extreme.MemberResolution.MembershipEvent);
            if (visible is NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict
                || visible is NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique unique
                    && !unique.SemanticMemberOpenTimesUtc.SequenceEqual(Members.Select(member => member.OpenTimeUtc))) return false;
            var memberIds = Members.Select(candle => candle.OpenTimeUtc).ToHashSet();
            if (frame.AvailableCandles.Any(candle => candle.OpenTimeUtc >= Members[0].OpenTimeUtc
                && candle.OpenTimeUtc <= Members[^1].OpenTimeUtc && !memberIds.Contains(candle.OpenTimeUtc))) return false;
            if (!CompletionEvidenceObservable(extreme.MemberResolution.MembershipEvent.Episode.Completion, effectiveAtUtc)) return false;
            if (extreme.MemberResolution.MembershipEvent.Episode.Completion is NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice predecessor
                && !HumanPriceStillCompatible(context, predecessor.Result)) return false;
        }
        // Recheck the formula against the actually resolved closed snapshot, never an original/future series.
        var resolvedMembers = Members.Select(member => closed[member.OpenTimeUtc]).ToArray();
        return Model switch
        {
            NasdaqStructuralLiquidityModel.OrdinaryTurn => new StructuralTurnBodyCoordinateCalculator()
                .Evaluate(resolvedMembers, CandidateSide(Role)).StructuralPrice == StructuralPrice,
            NasdaqStructuralLiquidityModel.ExpansionOrigin or NasdaqStructuralLiquidityModel.DirectionalCollision007 => resolvedMembers[0].Open == StructuralPrice,
            NasdaqStructuralLiquidityModel.PostCompletionExtreme => new StructuralTurnBodyCoordinateCalculator()
                .Evaluate(resolvedMembers, Side == NasdaqStructuralLiquiditySide.High ? StructuralTurnBodyCoordinateSide.Upper
                    : StructuralTurnBodyCoordinateSide.Lower).StructuralPrice == StructuralPrice,
            _ => true, // Human coordinates have no invented OHLC relation.
        };
    }

    private static bool HumanPriceStillCompatible(StrategyReplayContext context, NasdaqHumanStructuralPriceBreakoutCompletionResult completion)
    {
        var breakout = completion.Breakout;
        var visible = new NasdaqHumanCollisionStructuralPriceObservationSelector().Select(context,
            new(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide));
        return visible.Kind != NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Conflict
            && (visible.Kind != NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique
                || visible.StructuralPrice == completion.CandidateGeometry.StructuralPrice);
    }

    private static bool CompletionEvidenceObservable(NasdaqH4ReconstructionSnapshot.Completed completion, DateTimeOffset effective) => completion switch
    {
        NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice human => human.Result.HumanPriceSelection.SupportingObservations.All(item => item.ObservedAtUtc <= effective),
        _ => true,
    };

    private static IEnumerable<Candle> CompletionSources(NasdaqH4ReconstructionSnapshot.Completed completion) => completion switch
    {
        NasdaqH4ReconstructionSnapshot.Completed.Directional directional => BreakoutSources(directional.Result.Breakout),
        NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice human => BreakoutSources(human.Result.Breakout),
        NasdaqH4ReconstructionSnapshot.Completed.Ordinary ordinary => BreakoutSources(ordinary.Result.Breakout).Concat(ordinary.Result.MemberResolution.SelectedMembers),
        NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate direct => CandidateSources(direct.Result.Candidate).Append(direct.Result.ValidatingCandle),
        _ => throw new ArgumentException("Unknown completion branch.", nameof(completion)),
    };

    private static IEnumerable<Candle> BreakoutSources(NasdaqPostInvalidationCandidateRebuildBreakoutState breakout)
    {
        IEnumerable<Candle> origin = breakout.Origin switch
        {
            NasdaqPostInvalidationBreakoutOrigin.Candidate candidate => CandidateSources(candidate.State),
            NasdaqPostInvalidationBreakoutOrigin.Rebuild rebuild => rebuild.FirstTurnCandle is { } turn
                ? [rebuild.PriorMigrationCandle, turn] : [rebuild.PriorMigrationCandle],
            _ => throw new ArgumentException("Unknown breakout origin.", nameof(breakout)),
        };
        return origin.Append(breakout.InvalidatingCandle).Append(breakout.ValidatingCandle);
    }

    private static IEnumerable<Candle> CandidateSources(NasdaqPostInvalidationCandidateState candidate) => candidate.CorrectionTurnCandles
        .Append(candidate.InvalidatingCandle).Append(candidate.CorrectionStartCandle).Append(candidate.TerminalCandle).Append(candidate.LastProcessedCandle);

    private static object? CollisionKey(NasdaqPostInvalidationCandidateRebuildBreakoutState? breakout) => breakout is null ? null : new
    {
        breakout.Episode,
        breakout.CandidateSide,
        breakout.CollisionKind,
        FrozenPrice = Number(breakout.FrozenImpulseTerminal.StructuralPrice),
        FrozenAnchor = Number(breakout.FrozenImpulseTerminal.ProtectionAnchor),
        PriorAnchor = Number(breakout.PreviousProtectionAnchor),
        OriginPrice = Number(breakout.OriginGeometry.StructuralPrice),
        OriginAnchor = Number(breakout.OriginGeometry.ProtectionAnchor),
        Origin = breakout.Origin is NasdaqPostInvalidationBreakoutOrigin.Candidate ? "Candidate" : "Rebuild",
    };

    private static object CandleKey(Candle candle) => new
    {
        candle.ProviderId,
        candle.Symbol,
        candle.Timeframe,
        candle.OpenTimeUtc,
        candle.CloseTimeUtc,
        Open = Number(candle.Open),
        High = Number(candle.High),
        Low = Number(candle.Low),
        Close = Number(candle.Close),
        Volume = candle.Volume is { } volume ? Number(volume) : null,
    };

    internal static bool SameCandle(Candle a, Candle b) => a.ProviderId == b.ProviderId && a.Symbol == b.Symbol && a.Timeframe == b.Timeframe
        && a.OpenTimeUtc == b.OpenTimeUtc && a.CloseTimeUtc == b.CloseTimeUtc && a.Open == b.Open && a.High == b.High
        && a.Low == b.Low && a.Close == b.Close && a.Volume == b.Volume;

    private static Candle[] Snapshot(IEnumerable<Candle> candles, bool allowEmpty = false, bool allowRepeatedSources = false)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var snapshot = candles.ToArray();
        if ((!allowEmpty && snapshot.Length == 0) || snapshot.Any(candle => candle is null))
            throw new ArgumentException("Non-null exact source candles are required.", nameof(candles));
        var groups = snapshot.GroupBy(candle => candle.OpenTimeUtc).ToArray();
        if (!allowRepeatedSources && groups.Length != snapshot.Length)
            throw new ArgumentException("Selected members must be distinct.", nameof(candles));
        if (groups.Any(group => group.Any(candle => !SameCandle(group.First(), candle))))
            throw new ArgumentException("A source timestamp cannot carry contradictory candle data.", nameof(candles));
        return groups.Select(group => group.First()).OrderBy(candle => candle.OpenTimeUtc).ToArray();
    }

    private static StructuralTurnBodyCoordinateSide CandidateSide(NasdaqHumanH4StructuralRole role) => role switch
    {
        NasdaqHumanH4StructuralRole.HigherLow => StructuralTurnBodyCoordinateSide.Lower,
        NasdaqHumanH4StructuralRole.LowerHigh => StructuralTurnBodyCoordinateSide.Upper,
        _ => throw new ArgumentException("This model requires an established HL or LH.", nameof(role)),
    };

    private static NasdaqHumanH4StructuralRole CandidateRole(StructuralCandidateExtremeSide side) => side switch
    {
        StructuralCandidateExtremeSide.Lower => NasdaqHumanH4StructuralRole.HigherLow,
        StructuralCandidateExtremeSide.Upper => NasdaqHumanH4StructuralRole.LowerHigh,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static void CheckValidation(StructuralBodyCloseBreakResult? validation, StructuralTurnBodyCoordinateSide side)
    {
        if (validation is not null && (!validation.IsConfirmed || validation.Candle is null
            || validation.Direction != (side == StructuralTurnBodyCoordinateSide.Lower ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower)))
            throw new ArgumentException("The supplied validation must be an actual side-appropriate confirmed body break.", nameof(validation));
    }

    private static void CheckPrice(decimal? supplied, decimal calculated)
    {
        if (supplied is { } price && price != calculated)
            throw new ArgumentException("Supplied structural price must exactly equal its authoritative formula.", nameof(supplied));
    }

    internal static void ValidateProvenance(string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));
    }

    private static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    public int CompareTo(NasdaqStructuralLiquidityReference? other) => other is null ? 1 : StringComparer.Ordinal.Compare(semanticKey, other.semanticKey);
    public bool Equals(NasdaqStructuralLiquidityReference? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => Equals(obj as NasdaqStructuralLiquidityReference);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(semanticKey);
}
