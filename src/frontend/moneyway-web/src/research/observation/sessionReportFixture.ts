export const provider = "oanda-global-markets-mt5-demo",
    instrument = "US100",
    day = "2026-06-23";
const dataset = "fixture-replay";
export const windowId = "EXP-NQ-TRADING-WINDOW-1130-V1";
const h4 = "EXP-NQ-H4-STRICT-1-1-V1:1:1:H1";
const configuration =
    "StrictAfterSweep/WindowConfirmationWithPriorSweep/FormationAfterConfirmation/GeometryOnly/08:30:00.0000000-11:30:00.0000000";
const m1 = "BodyDirectionTurn",
    stopProfile = "EXP-NQ-M5-SL-STRICT-1-1-V1",
    targetProfile = "NearestAuditedSessionExtreme";
const flags = { canonicalAdmissionPermitted: false, operationalSignal: false };
const time = (hhmm: string) => day + "T" + hhmm + ":00Z";
export const at = {
    prep: time("13:00"),
    open: time("13:30"),
    published: time("16:00"),
    cutoff: time("16:30"),
    take: time("13:10"),
    trigger: time("13:35"),
    fvg: time("13:55"),
    counter: time("13:57"),
    realign: time("13:58"),
    invalid: time("15:10"),
};
export const digest = "a".repeat(64);
const bar = (start: string, end: string, amount: number, unit = "Minute") => ({
    providerId: { value: provider },
    symbol: { value: instrument },
    timeframe: { amount, unit },
    openTimeUtc: time(start),
    closeTimeUtc: time(end),
    open: 105,
    high: 107,
    low: 102,
    close: 106,
    volume: 10,
});
const realignmentCandle = bar("13:57", "13:58", 1);
const fixtureBase = (candidateId: string) => ({
    candidateId,
    version: "1.0.0",
    sessionId: dataset,
    asOfUtc: at.cutoff,
    provider,
    instrument,
    ...flags,
});
const trigger = (id: string, kind: string) => ({
    id,
    kind,
    confirmedAtUtc: at.trigger,
    direction: "Bullish",
    candidateId: "candidate-trigger",
    configuration: "StrictClose",
    evidenceQuality: "Observable",
    sourceReference: "ref/" + id,
    evidenceCandle: bar("13:30", "13:35", 5),
});

export function artifact({
    complete = false,
    protective = true,
    targetAvailable = true,
    simultaneous = false,
} = {}) {
    const closeReference = { price: 106, candle: realignmentCandle };
    const triggerEvidence = [
        trigger("sc", "StructuralChange"),
        ...(simultaneous ? [trigger("ifvg", "IFVG")] : []),
    ];
    const status = complete
        ? "ResearchChainCompleteWithInvalidatedContext"
        : "H4ContextUnavailable";
    return {
        schemaVersion: 1,
        registrationSha256: digest,
        sourceProvenance: ["H4", "H1", "M5", "M1"].map((timeframe) => ({
            timeframe,
            originalPath: timeframe + ".tsv",
            evidenceManifestSha256: digest,
            originalSha256: digest,
            canonicalSha256: "b".repeat(64),
            timestampEvidence: "RegisteredJuneDocumentaryMapping",
            localAcquisition: null,
        })),
        evidenceReadiness: {
            ...flags,
            session: {
                operatingDate: day,
                cutoffUtc: at.cutoff,
                exposure: "Development",
            },
            researchReplayPermitted: true,
            synchronizedInputReady: true,
            timestampEvidenceVerified: true,
            blockers: complete ? [] : ["H4ContextUnavailable"],
            acquisitionActions: ["Revisar cobertura H4 original"],
            frames: ["H4", "H1", "M5", "M1"].map((timeframe) => ({
                timeframe,
                coverageSufficient: true,
                missingRanges: [],
            })),
        },
        research: {
            ...fixtureBase("EXP-NQ-END-TO-END-MTF-V1"),
            datasetId: dataset,
            runId: ["EXP-NQ-END-TO-END-MTF-V1", dataset, at.cutoff].join("/"),
            operatingDate: day,
            sourceProvenance: "Fixture sintética para pruebas",
            replayStrategyVersion: "nasdaq-0.1.0-draft",
            historicalAvailability:
                "DocumentaryNominalClosure_NotAuthenticated",
            outcome: status,
            replayCutoff: {
                requestedUtc: at.cutoff,
                canonicalFrameUtc: at.cutoff,
                sources: ["4h", "1h", "5m", "1m"].map((timeframe) => ({
                    timeframe,
                    lastNominalCloseUtc: at.prep,
                    closedCount: 4,
                })),
            },
            window: {
                ...flags,
                asOfUtc: at.cutoff,
                preparationUtc: at.prep,
                candidateConfigurationId: windowId,
                lifecycleStatus: "CandidateConfiguration_NotActivated",
                timeZone: "America/Bogota",
                inclusion: "StartInclusive_EndExclusive",
                candidate: { startUtc: at.open, endUtc: at.cutoff },
                publishedRuntime: { startUtc: at.open, endUtc: at.published },
            },
            preparation: {
                scheduledUtc: at.prep,
                profiles: [
                    {
                        id: h4,
                        contextAvailable: complete,
                        independentlyValidated: false,
                        reasonCode: complete
                            ? "ObservableExperimentalH4Context"
                            : "InsufficientConfirmedPivots",
                        evidence: {
                            ...flags,
                            configurationId: h4,
                            candidateId: "EXP-NQ-H4-CONTEXT-V1",
                            candidateVersion: "1.0.0",
                            asOfUtc: at.prep,
                            candleHistory: [],
                            delayedHistory: [],
                            marketStructure: {
                                ...flags,
                                candidateId: "EXP-NQ-H4-MARKET-STRUCTURE-V1",
                                candidateVersion: "1.0.0",
                                asOfUtc: at.prep,
                                providerId: provider,
                                instrument,
                                classification: complete
                                    ? "Bullish"
                                    : "InsufficientEvidence",
                                confirmedPivots: [],
                                highRelation: "InsufficientEvidence",
                                lowRelation: "InsufficientEvidence",
                                sourceGaps: [],
                            },
                        },
                    },
                ],
            },
            sessionLiquidity: ["Asia", "London"].map((session) => ({
                session,
                operatingDate: day,
                provider,
                instrument,
                sourceTimeframe: "1h",
                strategyVersion: "nasdaq-0.3.0-session-candidate",
                startUtc: time("07:00"),
                endUtc: time("08:00"),
                availableAtUtc: time("08:00"),
                coverageStatus: "Complete",
                high: 115,
                low: 100,
                sourceCandles: [bar("07:00", "08:00", 1, "Hour")],
            })),
            summary: {
                distinctEpisodes: 1,
                episodesWithObservedLiquidityTake: 1,
                distinctSelectedConfirmationEvents: triggerEvidence.length,
                distinctM1Anchors: 1,
                distinctRealignmentEvents: 1,
                totalResearchConfigurationAssociations: 2,
                completeH4ToM1Associations: complete ? 2 : 0,
                distinctPairsWithPositiveRewardRisk:
                    protective && targetAvailable ? 1 : 0,
            },
            sequence: {
                ...fixtureBase("EXP-NQ-M5-CONTEXTUAL-SEQUENCE-V1"),
                episodes: [
                    {
                        id: "episode",
                        operatingDate: day,
                        selection: {
                            policyId: "EXP-NQ-CONTEXT-EVENT-E1-V1",
                            reason: "Selected",
                            liquidityReference: {
                                id: "reference",
                                category: "LL",
                                operatingDate: day,
                                provider,
                                instrument,
                                price: 102,
                            },
                            event: {
                                id: "take",
                                referenceId: "reference",
                                availableAtUtc: at.take,
                                candle: bar("13:05", "13:10", 5),
                            },
                        },
                    },
                ],
                confirmationEvidence: triggerEvidence,
                zones: [
                    {
                        formation: {
                            id: "zone",
                            originalDirection: "Bullish",
                            profile: "GeometryOnly",
                            formationUtc: at.fvg,
                            lowerBoundary: 105,
                            upperBoundary: 107,
                            c1: bar("13:40", "13:45", 5),
                            c2: bar("13:45", "13:50", 5),
                            c3: bar("13:50", "13:55", 5),
                        },
                    },
                ],
            },
            realignment: {
                ...fixtureBase("EXP-NQ-M1-REALIGNMENT-V1"),
                scans: [m1, "PullbackRangeReclaim"].map((profile) => ({
                    id: "scan/" + profile,
                    profile,
                    sourceGaps: [],
                    anchor: {
                        id: "anchor",
                        atUtc: at.fvg,
                        direction: "Bullish",
                        operatingDate: day,
                        windowOpenUtc: at.open,
                        windowCloseUtc: at.cutoff,
                    },
                    firstRealignmentUtc: at.realign,
                    firstRealignmentIds: ["attempt"],
                    attempts: [
                        {
                            id: "attempt",
                            observedAtUtc: at.counter,
                            confirmedAtUtc: at.realign,
                            realignment: realignmentCandle,
                            first: bar("13:56", "13:57", 1),
                            lastIncluded: bar("13:56", "13:57", 1),
                            low: 102,
                            high: 107,
                            status: "RealignmentObserved",
                            windowEligible: true,
                        },
                    ],
                })),
            },
            structuralStop: {
                ...fixtureBase("EXP-NQ-M5-STRUCTURAL-SL-V1"),
                evaluations: {
                    sl: {
                        asOfUtc: at.realign,
                        closeReference,
                        direction: "Bullish",
                        profile: { id: stopProfile },
                        reasonCode: protective
                            ? "LatestConfirmedStructuralReference"
                            : "ReferenceNotProtective",
                        onProtectiveSide: protective,
                        distancePoints: protective ? 4 : null,
                        sourceGaps: [],
                        selected: {
                            relation: "HigherLow",
                            pivot: {
                                id: "pivot",
                                profileId: stopProfile,
                                price: protective ? 102 : 108,
                                confirmedAtUtc: time("13:50"),
                                extremeCloseUtc: time("13:45"),
                            },
                        },
                    },
                },
            },
            structuralTakeProfit: {
                ...fixtureBase("EXP-NQ-STRUCTURAL-TP-V1"),
                evaluations: {
                    tp: {
                        asOfUtc: at.realign,
                        closeReference,
                        direction: "Bullish",
                        profile: { id: targetProfile },
                        reasonCode: targetAvailable
                            ? "TargetIdentified"
                            : "NoFavorableReference",
                        targetPrice: targetAvailable ? 114 : null,
                        rewardDistancePoints: targetAvailable ? 8 : null,
                        pivotSourceGaps: [],
                        selectedIds: targetAvailable ? ["objective"] : [],
                        candidates: [
                            {
                                id: "objective",
                                provider,
                                instrument,
                                price: 114,
                                eligibleAtCutoff: true,
                                favorableDirection: true,
                                category: "SessionHigh",
                                timeframe: "1h",
                                sourceTimestampUtc: at.prep,
                                availableAtUtc: at.realign,
                                priorInteraction: null,
                            },
                        ],
                    },
                },
                rewardRiskComparisons: {
                    pair: {
                        stopEvaluationId: "sl",
                        targetEvaluationId: "tp",
                        stopProfile: { id: stopProfile },
                        pairs: [
                            {
                                targetId: "objective",
                                hypotheticalRewardRisk: protective ? 2 : null,
                                reasonCode: protective
                                    ? "PositiveStructuralRewardAndRisk"
                                    : "ReferenceNotProtective",
                            },
                        ],
                    },
                },
            },
            chains: [m1, "PullbackRangeReclaim"].map((profile) => ({
                episodeId: "episode",
                m5ConfigurationId: configuration,
                m1Profile: profile,
                scanId: "scan/" + profile,
                m1AnchorId: "anchor",
                realignmentId: "attempt",
                liquidityTakeUtc: at.take,
                firstConfirmationUtc: at.trigger,
                firstConfirmationOutcome: simultaneous
                    ? "SimultaneousConfirmation"
                    : "StructuralChangeFirst",
                firstConfirmationIds: triggerEvidence.map((t) => t.id),
                directionalFvgUtc: at.fvg,
                realignmentUtc: at.realign,
                m5Reason: "Eligible",
                m1Reason: "RealignmentObserved",
                branches: [
                    {
                        binding: { fvgId: "zone", confirmationId: "sc" },
                        fvg: {
                            zoneId: "zone",
                            role: "DirectionalFVG",
                            invalidatedAtUtc: at.invalid,
                            evidenceLimitedAtUtc: null,
                        },
                        ifvg: null,
                        stateAtRealignment: "Observed",
                        stateAtCutoff: "Invalidated",
                    },
                ],
                geometry: [
                    {
                        configurationId: [
                            configuration,
                            profile,
                            stopProfile,
                            targetProfile,
                        ].join("/"),
                        stopProfileId: stopProfile,
                        targetProfileId: targetProfile,
                        stopEvaluationId: "sl",
                        targetEvaluationId: "tp",
                        rewardRiskComparisonId: targetAvailable ? "pair" : null,
                        downstreamOutcome:
                            protective && targetAvailable
                                ? "StructuralReferencesObserved"
                                : "WaitingForStructuralReferences",
                        structuralReferencesCompletedAtUtc:
                            protective && targetAvailable ? at.realign : null,
                        contextOutcomes: [
                            {
                                h4ConfigurationIds: [h4],
                                outcome: status,
                                completedAtUtc: complete ? at.realign : null,
                            },
                        ],
                    },
                ],
            })),
        },
    };
}
