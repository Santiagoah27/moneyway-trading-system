import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import App from "../../App";
import SessionObservation from "./SessionObservation";
import { parseSessionReport } from "./sessionReport";

const provider = "oanda-global-markets-mt5-demo",
    instrument = "US100",
    day = "2026-06-23";
const dataset = "fixture-replay";
const windowId = "EXP-NQ-TRADING-WINDOW-1130-V1";
const h4 = "EXP-NQ-H4-STRICT-1-1-V1:1:1:H1";
const configuration =
    "StrictAfterSweep/WindowConfirmationWithPriorSweep/FormationAfterConfirmation/GeometryOnly/08:30:00.0000000-11:30:00.0000000";
const m1 = "BodyDirectionTurn",
    stopProfile = "EXP-NQ-M5-SL-STRICT-1-1-V1",
    targetProfile = "NearestAuditedSessionExtreme";
const flags = { canonicalAdmissionPermitted: false, operationalSignal: false };
const time = (hhmm: string) => day + "T" + hhmm + ":00Z";
const at = {
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
const digest = "a".repeat(64);
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

function artifact({
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

async function upload(value: unknown) {
    const file = new File(
        [typeof value === "string" ? value : JSON.stringify(value)],
        "replay.json",
        { type: "application/json" },
    );
    await userEvent.upload(screen.getByLabelText("Abrir informe local"), file);
}

afterEach(() => {
    cleanup();
    window.history.replaceState(null, "", "/");
});

describe("observación supervisada Nasdaq", () => {
    it("abre informes 1.1.0 con evidencia sustentadora y rechaza una finalización sin ella", () => {
        const report = artifact({ complete: true });
        report.research.version = "1.1.0";
        report.research.runId = [
            report.research.candidateId,
            report.research.version,
            report.research.datasetId,
            at.cutoff,
        ].join("/");
        for (const chain of report.research.chains)
            Object.assign(chain.branches[0], {
                realignmentBindingVerified: true,
            });
        expect(parseSessionReport(report).counts.complete).toBe(2);

        for (const chain of report.research.chains)
            chain.branches[0].stateAtRealignment = "Invalidated";
        expect(() => parseSessionReport(report)).toThrow(
            /finalización sin contexto M5 sustentador/,
        );

        report.research.summary.completeH4ToM1Associations = 0;
        for (const chain of report.research.chains)
            Object.assign(chain.geometry[0].contextOutcomes[0], {
                outcome: "ContextInvalidatedAtRealignment",
                completedAtUtc: null,
            });
        expect(parseSessionReport(report).counts.complete).toBe(0);
    });

    it("navega desde Strategy Lab al espacio de observación local", async () => {
        window.history.replaceState(null, "", "/#strategy-lab");
        render(<App />);
        const link = await screen.findByRole("link", {
            name: /Observación supervisada Nasdaq/,
        });
        expect(link.getAttribute("href")).toBe("#strategy-lab-observation");
        await userEvent.click(link);
        expect(
            await screen.findByRole("heading", {
                name: "Observación supervisada Nasdaq",
            }),
        ).toBeDefined();
    });

    it("conserva la evidencia M5/M1 y referencias con H4 indisponible", async () => {
        render(<SessionObservation />);
        await upload(artifact());
        expect(
            await screen.findByText("Replay histórico · Development expuesto"),
        ).toBeDefined();
        expect(screen.getByRole("status").textContent).toContain(
            "H4ContextUnavailable",
        );
        expect(screen.getByText("[08:30, 11:00)")).toBeDefined();
        expect(screen.getByText("[08:30, 11:30)")).toBeDefined();
        expect(screen.getByText(windowId)).toBeDefined();
        const stage = screen.getByRole("button", {
            name: /Realineación.*08:58:00/,
        });
        expect(stage).toBeDefined();
        expect(
            screen.getAllByRole("button", { name: /Toma de liquidez/ }),
        ).toHaveLength(1);
        expect(
            screen.getByRole("button", {
                name: /Primera confirmación.*08:35:00/,
            }),
        ).toBeDefined();
        expect(
            screen.getByRole("button", {
                name: /FVG direccional posterior.*08:55:00/,
            }),
        ).toBeDefined();
        const counts = screen.getByLabelText("Recuentos de investigación");
        expect(
            within(counts).getByText("Episodios").nextElementSibling
                ?.textContent,
        ).toBe("1");
        expect(
            within(counts).getByText("Asociaciones de configuración")
                .nextElementSibling?.textContent,
        ).toBe("2");
        expect(
            screen.getByText("R:R hipotético").nextElementSibling?.textContent,
        ).toBe("2");
        expect(screen.getByText("Admisión canónica bloqueada")).toBeDefined();
        expect(
            screen.getByText("Política ejecutable de entrada")
                .nextElementSibling?.textContent,
        ).toBe("Sin resolver");
        expect(
            screen.queryByRole("button", {
                name: /enviar orden|ejecutar|comprar|vender/i,
            }),
        ).toBeNull();
        await userEvent.click(stage);
        await userEvent.click(
            screen.getByText("Inspeccionar velas originales"),
        );
        expect(
            screen.getByRole("img", { name: /Velas OHLC M1/ }),
        ).toBeDefined();
        await userEvent.click(
            screen.getByText("Configuraciones de investigación"),
        );
        const sequence = screen.getByLabelText(
            "Secuencia M5/M1",
        ) as HTMLSelectElement;
        await userEvent.selectOptions(sequence, sequence.options[1]);
        expect(
            screen.getAllByRole("button", { name: /Toma de liquidez/ }),
        ).toHaveLength(1);
    });

    it("muestra la finalización histórica antes de su invalidación y conserva confirmaciones simultáneas", async () => {
        render(<SessionObservation />);
        await upload(artifact({ complete: true, simultaneous: true }));
        const completed = await screen.findByRole("button", {
            name: /Finalización histórica.*08:58:00/,
        });
        const invalidated = screen.getByRole("button", {
            name: /Cronología del contexto.*10:10:00/,
        });
        expect(
            completed.compareDocumentPosition(invalidated) &
                Node.DOCUMENT_POSITION_FOLLOWING,
        ).toBeTruthy();
        expect(
            screen.getByText("Contexto M5 invalidado después."),
        ).toBeDefined();
        const triggerStage = screen.getByRole("button", {
            name: /Primera confirmación.*SimultaneousConfirmation/,
        });
        expect(triggerStage.textContent).toContain(
            "orden intravela indeterminado",
        );
        await userEvent.click(triggerStage);
        expect(screen.getByText("StructuralChange")).toBeDefined();
        expect(screen.getByText("IFVG", { exact: true })).toBeDefined();
    });

    it("muestra SL no protector, TP ausente y R:R no disponible", async () => {
        render(<SessionObservation />);
        await upload(artifact({ protective: false, targetAvailable: false }));
        expect(await screen.findByText("TP no disponible")).toBeDefined();
        expect(screen.getAllByText("SL no protector").length).toBeGreaterThan(
            0,
        );
        expect(
            screen.getByText("R:R hipotético").nextElementSibling?.textContent,
        ).toBe("No disponible");
    });

    it("rechaza JSON truncado y limpia la sesión cargada", async () => {
        render(<SessionObservation />);
        await upload(artifact());
        await screen.findByText("Replay histórico · Development expuesto");
        await upload("{");
        expect((await screen.findByRole("alert")).textContent).toBe(
            "JSON inválido o truncado.",
        );
        expect(
            screen.queryByText("Replay histórico · Development expuesto"),
        ).toBeNull();
    });

    it("distingue adquisición prospectiva registrada de un replay histórico sin presentarla como sesión en vivo", async () => {
        const prospective = JSON.parse(
            JSON.stringify(artifact()).replaceAll(day, "2026-11-23"),
        ) as ReturnType<typeof artifact>;
        prospective.evidenceReadiness.session.exposure = "Prospective";
        const sources = prospective.sourceProvenance.map((s) => ({
            ...s,
            firstOpenUtc: "2026-11-23T05:00:00Z",
            lastNominalCloseUtc: "2026-11-23T16:30:00Z",
            localAcquisition: {
                acquiredAtUtc: "2026-11-23T17:30:00Z",
                evidence: { sha256: digest },
            },
        }));
        render(<SessionObservation />);
        await upload({ ...prospective, sourceProvenance: sources });
        expect(
            await screen.findByText(
                "Observación prospectiva registrada · replay al corte",
            ),
        ).toBeDefined();
        expect(screen.queryByText(/en vivo|sesión actual/i)).toBeNull();
    });

    it.each([
        "2026-06-22",
        "2026-06-24",
        "2026-06-25",
        "2026-06-26",
        "2026-06-30",
        "2026-09-08",
    ])(
        "rechaza el día protegido %s aunque declare exposición de desarrollo",
        (date) => {
            const input = artifact();
            input.research.operatingDate = date;
            expect(() => parseSessionReport(input)).toThrow(/protegida/);
        },
    );

    it("rechaza esquema, procedencia, identidades antiguas y configuraciones incoherentes", () => {
        expect(() =>
            parseSessionReport({ ...artifact(), schemaVersion: 2 }),
        ).toThrow(/versión del esquema/);
        const sourceMissing = artifact();
        sourceMissing.sourceProvenance.pop();
        expect(() => parseSessionReport(sourceMissing)).toThrow(
            /cuatro escalas/,
        );
        const configurationMismatch = artifact();
        configurationMismatch.research.chains[0].geometry[0].configurationId =
            "otro";
        expect(() => parseSessionReport(configurationMismatch)).toThrow(
            /inconsistente/,
        );
        const runtimeChange = artifact();
        runtimeChange.research.window.publishedRuntime.endUtc = at.cutoff;
        expect(() => parseSessionReport(runtimeChange)).toThrow(/límites/);
        const future = artifact();
        future.research.chains[0].branches[0].fvg.invalidatedAtUtc =
            day + "T16:30:00.0000001Z";
        expect(() => parseSessionReport(future)).toThrow(/posterior al corte/);
    });
});
