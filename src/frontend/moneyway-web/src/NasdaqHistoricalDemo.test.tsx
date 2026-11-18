import { act, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NasdaqHistoricalDemo } from "./NasdaqHistoricalDemo";
import type {
    NasdaqHistoricalReport,
    SupervisedHistoricalTradeSnapshot,
} from "./types/nasdaqHistoricalDemo";

const report: NasdaqHistoricalReport = {
    sessionId: "synthetic-no-trade-ui-only",
    strategyId: "moneyway-nasdaq",
    strategyVersion: "nasdaq-0.1.0-draft",
    providerId: "test-demo-provider",
    symbol: "US100",
    tradingDay: "2026-06-29",
    schemaVersion: 2,
    frameCount: 9660,
    finalVerdict: "NoTrade",
    replayStartUtc: "2026-06-21T22:01:00Z",
    replayEndUtc: "2026-06-30T21:00:00Z",
    reportSha256: "A".repeat(64),
    reportBytes: 178140291,
    validation: "SchemaChronologyAndCountChecked",
    evidenceValidation: "NotRevalidated",
    inputDiagnosticCodes: [],
    bindingDiagnosticCodes: [],
    selectedSessionLastFrame: {
        step: 8000,
        asOfUtc: "2026-06-29T16:20:00Z",
        verdict: "NoTrade",
        reason: "No qualifying preparation evidence.",
        blockingRuleId: "NQ-TIME-003",
        blockingResult: "Failed",
        hasCompleteRequiredCoverage: true,
        missingRequiredRuleIds: [],
        snapshotState: "Unavailable",
        snapshotDiagnostic: "CanonicalEligibilityUnavailable",
        factualEvaluationState: "Unavailable",
        factualRequiresHumanValidation: null,
        rules: [
            {
                ruleId: "NQ-TIME-003",
                definitionStatus: "Confirmed",
                result: "Failed",
                sequence: 5,
                isRequired: true,
                reason: "No qualifying preparation evidence.",
                evaluatedAtUtc: "2026-06-29T16:20:00Z",
                evidenceId: null,
            },
        ],
    },
};

function respond(body: unknown = report, status = 200) {
    vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue(
            new Response(JSON.stringify(body), {
                status,
                headers: { "Content-Type": "application/json" },
            }),
        ),
    );
    render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
}

afterEach(() => vi.unstubAllGlobals());

const tradeFixture = {
    path: "WickfillScenario2",
    evidenceScope: "RecordedRuleEvaluationsAndInputs",
    stages: [
        {
            stage: "StopLoss",
            status: "MissingOrBlocked",
            ruleId: "NQ-SL-001",
            ruleResult: "Waiting",
            reason: "Scenario 2 SL selector evaluated: native evidence missing.",
        },
        {
            stage: "SessionAttempts",
            status: "BlockedByUnresolvedSpecification",
            ruleId: "NQ-REENTRY-001",
            ruleResult: "HumanValidationRequired",
            reason: "Reentry specification remains unresolved.",
        },
    ],
    entry: null,
    stopPrice: null,
    takeProfitPrice: null,
    riskRatio: null,
    exit: null,
    exitScope: null,
    documentedExitReason: null,
    failureClassification: "inconclusive",
    failedRuleIds: [],
    sources: [
        {
            inputType: "PersistedInput",
            observedAtUtc: "2026-06-29T13:27:42Z",
            sourceReference: "human:S9",
            sourceReferenceSha256: "a".repeat(64),
            truncated: false,
        },
    ],
    omittedSources: 0,
    completeSupervisedTrade: false,
};

it("shows the incomplete trade lifecycle and human provenance without invented prices", async () => {
    respond({
        ...report,
        selectedSessionLastFrame: {
            ...report.selectedSessionLastFrame,
            tradeLifecycle: tradeFixture,
        },
    });
    await screen.findByRole("region", {
        name: "Ciclo de la operación histórica",
    });
    expect(
        screen.getByText(
            "Scenario 2 SL selector evaluated: native evidence missing.",
        ),
    ).toBeTruthy();
    expect(
        screen.getByText("Falta evidencia de entrada en el snapshot canónico"),
    ).toBeTruthy();
    expect(screen.getByText("human:S9")).toBeTruthy();
    expect(screen.queryByText("29604")).toBeNull();
    expect(screen.queryByText("29662.25")).toBeNull();
    expect(screen.getByText(/Clasificación: inconclusive/)).toBeTruthy();
});

it("shows a source-documented close without claiming profit or a valid strategy loss", async () => {
    respond({
        ...report,
        selectedSessionLastFrame: {
            ...report.selectedSessionLastFrame,
            snapshotState: "Available",
            factualEvaluationState: "Available",
            tradeLifecycle: {
                ...tradeFixture,
                entry: {
                    price: 101.25,
                    effectiveAtUtc: "2026-06-29T14:00:00Z",
                },
                stopPrice: 99,
                takeProfitPrice: 104,
                exit: { price: 103.5, effectiveAtUtc: "2026-06-29T14:30:00Z" },
                exitScope: "Partial",
                documentedExitReason: "Synthetic documented close",
            },
        },
    });
    await screen.findByText("101.25 · 2026-06-29T14:00:00Z");
    expect(
        screen.getByText("103.5 · 2026-06-29T14:30:00Z · Partial"),
    ).toBeTruthy();
    expect(
        screen.getByText(
            "Motivo literal de salida: Synthetic documented close",
        ),
    ).toBeTruthy();
    expect(
        screen.getByText(/sesión supervisada completa sigue pendiente/),
    ).toBeTruthy();
});

it("shows the separate canonical WF2 stop selection while entry risk and TP remain incomplete", async () => {
    respond({
        ...report,
        selectedSessionLastFrame: {
            ...report.selectedSessionLastFrame,
            tradeLifecycle: {
                ...tradeFixture,
                stages: [
                    {
                        stage: "StopLoss",
                        status: "RecordedEvaluation",
                        ruleId: "NQ-SL-001",
                        ruleResult: "Passed",
                        reason: "Scenario 2 SL selector established native evidence.",
                    },
                ],
                scenario2StopLoss: {
                    referenceId: "synthetic:peak",
                    protectivePrice: 115,
                    stopPrice: 116,
                    effectiveAtUtc: "2026-06-29T13:31:00Z",
                    observedAtUtc: "2026-06-29T13:33:00Z",
                    source: {
                        sourceReference: "synthetic:human-stop",
                        sourceReferenceSha256: "a".repeat(64),
                    },
                    protectiveSource: {
                        sourceReference: "synthetic:human-peak",
                        sourceReferenceSha256: "b".repeat(64),
                    },
                },
            },
        },
    });
    await screen.findByText(/SL canónico Scenario 2: 116/);
    expect(screen.getByText(/synthetic:peak · 115/)).toBeTruthy();
    expect(screen.getByText(/Fuente SL: synthetic:human-stop/)).toBeTruthy();
    expect(
        screen.getByText("Falta evidencia de entrada en el snapshot canónico"),
    ).toBeTruthy();
    expect(screen.getByText(/requisitos independientes/)).toBeTruthy();
});

it.each([true, false])(
    "renders the canonical Scenario 2 risk comparison (%s) without inventing execution",
    async (passed) => {
        respond({
            ...report,
            selectedSessionLastFrame: {
                ...report.selectedSessionLastFrame!,
                tradeLifecycle: {
                    ...tradeFixture,
                    path: "WickfillScenario2",
                    scenario2RiskExposure: {
                        accountBasisAmountText: "10000",
                        reportedMaximumLossText: passed
                            ? "100"
                            : "100.00000000000000000000000001",
                        plannedEntryPriceText: "110",
                        quantityText: null,
                        stopPriceText: "116",
                        riskRatioText: "0.01",
                        exposure: {
                            accountBasisAmount: 10000,
                            currency: "USD",
                            accountBasis: "synthetic:named-basis",
                            reportedMaximumLoss: 100,
                            plannedEntryPrice: 110,
                            costTreatment: "synthetic:costs",
                            calculationSourceReference: "synthetic:calculation",
                            quantity: null,
                            quantityUnit: null,
                        },
                        stopPrice: 116,
                        riskRatio: 0.01,
                        ratioExceedsDecimalRange: false,
                        limit: 0.01,
                        isWithinLimit: passed,
                        effectiveAtUtc: "2026-06-29T13:33:00Z",
                        observedAtUtc: "2026-06-29T13:34:00Z",
                        source: {
                            sourceReference: "synthetic:approved-risk",
                            sourceReferenceSha256: "a".repeat(64),
                        },
                    },
                },
            },
        });
        const panel = await screen.findByRole("region", {
            name: "Riesgo documentado Scenario 2",
        });
        expect(panel.textContent).toContain(passed ? "Passed" : "Failed");
        expect(panel.textContent).toContain("10000 USD");
        expect(panel.textContent).toContain(
            passed ? "100 USD" : "100.00000000000000000000000001 USD",
        );
        expect(panel.textContent).toContain("synthetic:approved-risk");
        expect(panel.textContent).toContain("2026-06-29T13:34:00Z");
        expect(panel.textContent).toContain("No suministrada");
        expect(
            screen.getByText(
                "Falta evidencia de entrada en el snapshot canónico",
            ),
        ).toBeTruthy();
    },
);

it("renders exact human Scenario 2 target identity and original availability without a completed trade", async () => {
    respond({
        ...report,
        selectedSessionLastFrame: {
            ...report.selectedSessionLastFrame!,
            tradeLifecycle: {
                ...tradeFixture,
                path: "WickfillScenario2",
                scenario2TakeProfit: {
                    providerId: "synthetic-native",
                    symbol: "US100",
                    direction: "Sell",
                    referenceKind: "Structural",
                    referenceDescription: "HumanEstablishedExtreme/LowerLow/1H",
                    referenceSha256: "c".repeat(64),
                    targetReferencePriceText: "77.123456789012345678901234567",
                    takeProfitPriceText: "77.123456789012345678901234567",
                    priceUnit: "NativeInstrumentPrice",
                    effectiveAtUtc: "2026-06-29T13:15:00Z",
                    observedAtUtc: "2026-06-29T13:34:00Z",
                    source: {
                        sourceReference: "synthetic:human-selected-TP",
                        sourceReferenceSha256: "a".repeat(64),
                    },
                    referenceSource: {
                        sourceReference: "synthetic:reviewed-LL",
                        sourceReferenceSha256: "b".repeat(64),
                    },
                },
            },
        },
    });
    const panel = await screen.findByRole("region", {
        name: "TP documentado Scenario 2",
    });
    expect(panel.textContent).toContain("NQ-TP-001");
    expect(panel.textContent).toContain("Passed");
    expect(panel.textContent).toContain("synthetic-native / US100");
    expect(panel.textContent).toContain("77.123456789012345678901234567");
    expect(panel.textContent).toContain("synthetic:human-selected-TP");
    expect(panel.textContent).toContain("2026-06-29T13:34:00Z");
    expect(
        screen.getByText("Falta evidencia de entrada en el snapshot canónico"),
    ).toBeTruthy();
    expect(
        screen.getByText(/sesión supervisada completa sigue pendiente/),
    ).toBeTruthy();
});

it.each([false, true])(
    "shows documentary human entry with honest precision, native gaps and original rules (%s)",
    async (approximate) => {
        respond({
            ...report,
            selectedSessionLastFrame: {
                ...report.selectedSessionLastFrame!,
                tradeLifecycle: {
                    ...tradeFixture,
                    historicalEntry: {
                        asOfUtc: "2026-06-29T13:36:00Z",
                        selectionStatus: "RetrospectiveOnly",
                        scope: "HistoricalEvidenceAuditNotStrategyInput",
                        entryAuthorization: "NotGrantedByDiagnostic",
                        nativeExecutionIndependentlyVerified: false,
                        failureClassification: "inconclusive",
                        observations: [
                            {
                                availability: "RetrospectiveOnly",
                                nativeExecutionStatus:
                                    "NativeExecutionUnverified",
                                evidence: {
                                    reviewId: "synthetic:entry",
                                    sourceReference: "synthetic:source-entry",
                                    humanValidationIdentity:
                                        "synthetic:human-reviewer",
                                    origin: "HumanReview",
                                    eventKind: "PositionOpeningField",
                                    sourcePlatform: "MT5",
                                    sourceProviderId: null,
                                    sourceSymbol: "USTEC",
                                    originalTime: "synthetic:16:32 server",
                                    timePrecision: approximate
                                        ? "Approximate"
                                        : "SourceReportedSecond",
                                    earliestEventUtc: "2026-06-29T13:32:00Z",
                                    latestEventUtc: approximate
                                        ? "2026-06-29T13:33:00Z"
                                        : "2026-06-29T13:32:00Z",
                                    timeMappingReference:
                                        "synthetic:explicit-source-mapping",
                                    historicalAvailableAtUtc: null,
                                    reviewCreatedOn: "2026-11-05",
                                    sourceEntryPriceText: null,
                                    sourcePriceUnit: null,
                                    executionReference: null,
                                },
                                canonicalAtEntry: approximate
                                    ? null
                                    : {
                                          step: 32,
                                          asOfUtc: "2026-06-29T13:32:00Z",
                                          verdict: "HumanValidationRequired",
                                          hasCompleteRequiredCoverage: true,
                                          rules: [
                                              {
                                                  ...report
                                                      .selectedSessionLastFrame!
                                                      .rules[0],
                                                  ruleId: "NQ-WF2-002",
                                                  result: "Waiting",
                                              },
                                          ],
                                      },
                                issues: [
                                    "HistoricalAvailabilityUnknown_NotAdmitted",
                                    "OriginalInstrumentMismatch_NoMapping",
                                    "Wf2NotPassedAtOriginalEntry",
                                ],
                            },
                        ],
                    },
                },
            },
        });
        const panel = await screen.findByRole("region", {
            name: "Evidencia histórica de entrada humana",
        });
        expect(panel.textContent).toContain("NotGrantedByDiagnostic");
        expect(panel.textContent).toContain(
            "Verificación nativa independiente: No",
        );
        expect(panel.textContent).toContain("USTEC");
        expect(panel.textContent).toContain("synthetic:source-entry");
        expect(panel.textContent).toContain(
            "revisión retrospectiva no admitida",
        );
        expect(panel.textContent).toContain(
            "Falta registro nativo de ejecución",
        );
        expect(panel.textContent).toContain("No documentado");
        if (approximate)
            expect(panel.textContent).toContain(
                "No hay un frame exacto acreditado",
            );
        else expect(panel.textContent).toContain("NQ-WF2-002: Waiting");
    },
);

it.each([
    [false, false],
    [true, false],
    [false, true],
    [true, true],
])(
    "renders a supervised WF2 snapshot with independent documentary completeness (%s, exit=%s)",
    async (parameters, withExit) => {
        const source = {
            sourceReference: "synthetic:native-source",
            sourceReferenceSha256: "a".repeat(64),
        };
        const snapshot: SupervisedHistoricalTradeSnapshot = {
            historicalExitObserved: withExit,
            documentaryExit: withExit
                ? {
                      asOfUtc: "2026-06-29T13:37:00Z",
                      humanValidationRequired: true,
                      nativeExitIndependentlyVerified: false,
                      entryExitRelationshipIndependentlyVerified: false,
                      completeSupervisedTrade: false,
                      economicOutcomeSupported: false,
                      observations: [
                          {
                              evidence: {
                                  reviewId: "synthetic:exit",
                                  sourceReference: "synthetic:close-review",
                                  humanValidationIdentity: "synthetic:human",
                                  origin: parameters
                                      ? "NativeExecutionRecord"
                                      : "HumanReview",
                                  eventKind: "ReportedClosure",
                                  sourcePlatform: "synthetic:platform",
                                  sourceSymbol: parameters ? "US100" : "USTEC",
                                  originalTime: "synthetic:around 08:37",
                                  originalClockDomain: "synthetic:UTC-5",
                                  timePrecision: parameters
                                      ? "ExactNativeRecord"
                                      : "Approximate",
                                  nominalEventUtc: null,
                                  effectiveAtUtc: parameters
                                      ? "synthetic:13:37Z"
                                      : null,
                                  historicalAvailableAtUtc: null,
                                  exitPriceText: parameters
                                      ? "100.00000000000000000000000001"
                                      : null,
                                  quantityText: null,
                                  executionReference: parameters
                                      ? "synthetic:closing-deal"
                                      : null,
                                  linkedEntryReviewId: "synthetic:entry",
                                  associationSourceReference:
                                      "synthetic:explicit-association",
                                  documentedCloseMechanism: null,
                                  reportedStopProtection:
                                      "synthetic:approximately 1:1",
                                  stopProtectionSourceReference:
                                      "synthetic:management-review",
                                  limitations:
                                      "synthetic:unverified-retrospective-evidence",
                              },
                              availability: "RetrospectiveOnly",
                              observationStatus:
                                  "ReportedClosure_NotIndependentlyVerified",
                              entryAssociation:
                                  "DocumentaryCandidate_OrderingRequiresValidation",
                              nativeExitStatus: "NativeExitUnverified",
                              nativeIdentityConsistent: false,
                              issues: [
                                  "IndependentNativeExitAndRelationshipVerificationNotPerformed",
                              ],
                          },
                      ],
                  }
                : null,
            session: {
                providerId: { value: "synthetic-native" },
                symbol: { value: "US100" },
                tradingDay: "2026-06-29",
            },
            frameStep: 36,
            asOfUtc: "2026-06-29T13:36:00Z",
            canonicalVerdict: "HumanValidationRequired",
            components: [
                {
                    stage: "Entry",
                    status: "RecordedArtifact",
                    ruleId: null,
                    ruleResult: null,
                    reason: "Documentary source, not authorization.",
                },
                {
                    stage: "StopLoss",
                    status: parameters
                        ? "RecordedArtifact"
                        : "MissingOrBlocked",
                    ruleId: "NQ-SL-001",
                    ruleResult: parameters
                        ? "Passed"
                        : "HumanValidationRequired",
                    reason: "Original native reference and human validation required.",
                },
                {
                    stage: "Risk",
                    status: parameters
                        ? "RecordedArtifact"
                        : "MissingOrBlocked",
                    ruleId: "NQ-RISK-001",
                    ruleResult: parameters ? "Passed" : "Waiting",
                    reason: "No size inferred.",
                },
                {
                    stage: "TakeProfit",
                    status: parameters
                        ? "RecordedArtifact"
                        : "MissingOrBlocked",
                    ruleId: "NQ-TP-001",
                    ruleResult: parameters ? "Passed" : "Waiting",
                    reason: "Original selected Low only.",
                },
                {
                    stage: "Execution",
                    status: parameters ? "RecordedArtifact" : "MissingInput",
                    ruleId: null,
                    ruleResult: null,
                    reason: "No independent broker verification.",
                },
                {
                    stage: "DocumentedExit",
                    status: "NotImplemented",
                    ruleId: null,
                    ruleResult: null,
                    reason: "No WF2 exit.",
                },
                {
                    stage: "EconomicOutcome",
                    status: "NotImplemented",
                    ruleId: null,
                    ruleResult: null,
                    reason: "No economic result.",
                },
            ],
            missingEvidence: [
                "IndependentNativeExecutionVerificationNotPerformed",
                "Wf2DocumentedExitNotIntegrated",
                "EconomicOutcomeNotImplemented",
            ],
            instrumentCorrespondence: parameters
                ? "native_identity_matches"
                : "unresolved",
            historicalEntryObserved: true,
            canonicalEligibilityRecordedAtEntry: parameters,
            stopLossSupported: parameters,
            riskExposureSupported: parameters,
            takeProfitSupported: parameters,
            nativeExecutionDocumented: parameters,
            canonicalBindingsConsistent: parameters,
            documentedParameterSetAvailable: parameters,
            documentaryEntry: {
                asOfUtc: "2026-06-29T13:36:00Z",
                selectionStatus: parameters ? "Unique" : "RetrospectiveOnly",
                scope: "HistoricalEvidenceAuditNotStrategyInput",
                entryAuthorization: "NotGrantedByDiagnostic",
                nativeExecutionIndependentlyVerified: false,
                failureClassification: "inconclusive",
                observations: [
                    {
                        availability: parameters
                            ? "AvailableAtSelectedFrame"
                            : "RetrospectiveOnly",
                        nativeExecutionStatus: parameters
                            ? "NativeRecordDocumented"
                            : "NativeExecutionUnverified",
                        evidence: {
                            reviewId: "synthetic:entry",
                            sourceReference: "synthetic:original-entry",
                            humanValidationIdentity: "synthetic:human",
                            origin: parameters
                                ? "NativeExecutionRecord"
                                : "HumanReview",
                            eventKind: "ReportedExecution",
                            sourcePlatform: "synthetic:platform",
                            sourceProviderId: parameters
                                ? "synthetic-native"
                                : null,
                            sourceSymbol: parameters ? "US100" : "USTEC",
                            originalTime: "synthetic:13:32Z",
                            timePrecision: parameters
                                ? "ExactNativeRecord"
                                : "SourceReportedSecond",
                            earliestEventUtc: "2026-06-29T13:32:00Z",
                            latestEventUtc: "2026-06-29T13:32:00Z",
                            timeMappingReference: "synthetic:explicit-mapping",
                            historicalAvailableAtUtc: parameters
                                ? "2026-06-29T13:32:00Z"
                                : null,
                            reviewCreatedOn: null,
                            sourceEntryPriceText: parameters
                                ? "110"
                                : "29604.00",
                            sourcePriceUnit: "NativeInstrumentPrice",
                            executionReference: parameters
                                ? "synthetic:deal-123"
                                : null,
                        },
                        canonicalAtEntry: parameters
                            ? {
                                  step: 32,
                                  asOfUtc: "2026-06-29T13:32:00Z",
                                  verdict: "Ready",
                                  hasCompleteRequiredCoverage: true,
                                  rules: [
                                      {
                                          ...report.selectedSessionLastFrame!
                                              .rules[0],
                                          ruleId: "NQ-WF2-002",
                                          result: "Passed",
                                          evaluatedAtUtc:
                                              "2026-06-29T13:32:00Z",
                                      },
                                  ],
                              }
                            : null,
                        issues: [],
                    },
                ],
            },
            stopLoss: parameters
                ? {
                      referenceId: "synthetic:peak",
                      protectivePrice: 115,
                      stopPrice: 116,
                      stopPriceText: "116.00000000000000000000000001",
                      effectiveAtUtc: "2026-06-29T13:31:00Z",
                      observedAtUtc: "2026-06-29T13:31:00Z",
                      source,
                      protectiveSource: source,
                  }
                : null,
            risk: parameters
                ? {
                      exposure: {
                          accountBasisAmount: 10000,
                          reportedMaximumLoss: 100,
                          currency: "USD",
                          accountBasis: "synthetic:basis",
                          plannedEntryPrice: 110,
                          costTreatment: "synthetic:costs",
                          calculationSourceReference: "synthetic:calculation",
                          quantity: null,
                          quantityUnit: null,
                      },
                      accountBasisAmountText: "10000",
                      reportedMaximumLossText: "100",
                      plannedEntryPriceText: "110",
                      quantityText: null,
                      stopPriceText: "116.00000000000000000000000001",
                      riskRatioText: "0.01",
                      stopPrice: 116,
                      riskRatio: 0.01,
                      ratioExceedsDecimalRange: false,
                      limit: 0.01,
                      isWithinLimit: true,
                      effectiveAtUtc: "2026-06-29T13:31:00Z",
                      observedAtUtc: "2026-06-29T13:31:00Z",
                      source,
                  }
                : null,
            takeProfit: parameters
                ? {
                      providerId: "synthetic-native",
                      symbol: "US100",
                      direction: "Sell",
                      referenceKind: "SessionLevel",
                      referenceDescription: "AsiaLow",
                      referenceSha256: "b".repeat(64),
                      targetReferencePriceText: "90",
                      takeProfitPriceText: "90",
                      priceUnit: "NativeInstrumentPrice",
                      effectiveAtUtc: "2026-06-29T13:31:00Z",
                      observedAtUtc: "2026-06-29T13:31:00Z",
                      source,
                      referenceSource: source,
                  }
                : null,
            scope: "SupervisedHistoricalEvidenceSnapshot",
            scenario: "WickfillScenario2",
            direction: "Sell",
            entryAuthorization: "NotGrantedBySnapshot",
            nativeExecutionIndependentlyVerified: false,
            exitSupported: false,
            exit: null,
            economicOutcomeSupported: false,
            economicOutcome: null,
            completeSupervisedTrade: false,
            failureClassification: "inconclusive",
        };
        respond({
            ...report,
            selectedSessionLastFrame: {
                ...report.selectedSessionLastFrame!,
                tradeLifecycle: {
                    ...tradeFixture,
                    supervisedSnapshot: snapshot,
                },
            },
        });
        const panel = await screen.findByRole("region", {
            name: "Snapshot histórico supervisado WF2",
        });
        expect(panel.textContent).toContain("Incompleto");
        expect(panel.textContent).toContain("NotGrantedBySnapshot");
        expect(panel.textContent).toContain(
            "Verificación nativa independiente: No",
        );
        expect(panel.textContent).toContain("NotImplemented");
        expect(panel.textContent).toContain("synthetic:original-entry");
        if (withExit) {
            const exitPanel = screen.getByRole("region", {
                name: "Evidencia de salida histórica",
            });
            expect(exitPanel.textContent).toContain(
                "Salida nativa verificada: No",
            );
            expect(exitPanel.textContent).toContain("Operación completa: No");
            expect(exitPanel.textContent).toContain("synthetic:around 08:37");
            expect(exitPanel.textContent).toContain(
                "synthetic:approximately 1:1",
            );
            expect(exitPanel.textContent).toContain(
                "DocumentaryCandidate_OrderingRequiresValidation",
            );
            expect(exitPanel.textContent).toContain("synthetic:close-review");
            expect(exitPanel.textContent).not.toContain("+1R");
        }
        if (parameters) {
            expect(panel.textContent).toContain(
                "116.00000000000000000000000001",
            );
            expect(panel.textContent).toContain("100 / 10000 USD");
            expect(panel.textContent).toContain("synthetic:deal-123");
        } else {
            expect(panel.textContent).toContain("USTEC: 29604.00");
            expect(panel.textContent).toContain("unresolved");
            expect(panel.textContent).toContain("Falta SL nativo");
            expect(panel.textContent).toContain("Falta deal nativo");
        }
    },
);

// Synthetic transport fixtures: explicit timestamps, never an ordinal-to-time formula.
function navigable(step = 8000): NasdaqHistoricalReport {
    const steps = [7998, 7999, 8000];
    const timestamps = [
        "2026-06-29T13:29:00Z",
        "2026-06-29T13:30:00Z",
        "2026-06-29T16:20:00Z",
    ];
    const index = steps.indexOf(step);
    const frame = report.selectedSessionLastFrame!;
    return {
        ...report,
        selectedSessionLastFrame: null,
        selectedFrame: {
            ...frame,
            step,
            asOfUtc: timestamps[index],
            verdict: index === 0 ? "Wait" : "NoTrade",
            reason: `source frame ${step}`,
            rules: [
                {
                    ...frame.rules[0],
                    reason: `source rule ${step}`,
                    result: index === 0 ? "Waiting" : "Failed",
                },
            ],
        },
        navigation: {
            firstStep: 7998,
            lastStep: 8000,
            selectedStep: step,
            previousStep: steps[index - 1] ?? null,
            nextStep: steps[index + 1] ?? null,
            sessionFrameCount: 3,
        },
    };
}

function jsonResponse(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
    });
}

function navigationFetch() {
    const mock = vi.fn((url: string) =>
        Promise.resolve(
            jsonResponse(
                navigable(
                    Number(new URL(url).searchParams.get("frameStep") ?? 8000),
                ),
            ),
        ),
    );
    vi.stubGlobal("fetch", mock);
    return mock;
}

describe("Historical frame navigation", () => {
    it("starts at last, uses actual neighbors, respects boundaries and returns to last", async () => {
        const mock = navigationFetch();
        const user = userEvent.setup();
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        expect(
            screen
                .getByRole("button", { name: "Siguiente" })
                .getAttribute("disabled"),
        ).not.toBeNull();
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source frame 7999");
        expect(mock.mock.calls.at(-1)![0]).toContain("frameStep=7999");
        expect(mock.mock.calls.at(-1)![0]).toContain(
            `reportSha256=${report.reportSha256}`,
        );
        await user.click(screen.getByRole("button", { name: "Siguiente" }));
        await screen.findByText("source frame 8000");
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source frame 7999");
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source frame 7998");
        expect(
            screen
                .getByRole("button", { name: "Anterior" })
                .getAttribute("disabled"),
        ).not.toBeNull();
        expect(screen.getByText("Wait")).toBeTruthy();
        expect(screen.getByText("NO_TRADE (NoTrade)")).toBeTruthy(); // Complete-report result remains separate.
        expect(screen.getByText(/Hora America\/Bogota:/).textContent).toMatch(
            /\b0?8:29:00/,
        );
        expect(screen.getByText(/Frame 7998/).textContent).toContain(
            "2026-06-29T13:29:00Z",
        );
        expect(screen.queryByText("source rule 8000")).toBeNull();
        await user.click(screen.getByRole("button", { name: "Último frame" }));
        await screen.findByText("source frame 8000");
        expect(mock.mock.calls.at(-1)![0]).toBe(
            "http://localhost:5080/api/nasdaq/historical-demo",
        );
    });

    it("supports direct selection and rejects invalid input without requesting a frame", async () => {
        const mock = navigationFetch();
        const user = userEvent.setup();
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        const input = screen.getByLabelText("Step del frame");
        await user.clear(input);
        await user.type(input, "7999");
        await user.click(screen.getByRole("button", { name: "Ir al frame" }));
        await screen.findByText("source frame 7999");
        await user.clear(screen.getByLabelText("Step del frame"));
        await user.type(screen.getByLabelText("Step del frame"), "0");
        await user.click(screen.getByRole("button", { name: "Ir al frame" }));
        expect((await screen.findByRole("alert")).textContent).toContain(
            "Step entero",
        );
        expect(mock).toHaveBeenCalledTimes(2);
        expect(screen.getByText("source frame 7999")).toBeTruthy();
    });

    it("hides previous evidence while loading and handles an unavailable frame", async () => {
        let complete!: (response: Response) => void;
        vi.stubGlobal(
            "fetch",
            vi
                .fn()
                .mockResolvedValueOnce(jsonResponse(navigable()))
                .mockImplementationOnce(
                    () =>
                        new Promise<Response>((resolve) => {
                            complete = resolve;
                        }),
                ),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        fireEvent.click(screen.getByRole("button", { name: "Anterior" }));
        expect(screen.getByText("Cargando reporte histórico.")).toBeTruthy();
        expect(screen.queryByText("source frame 8000")).toBeNull();
        complete(jsonResponse({ code: "FrameNotInSelectedSession" }, 404));
        expect((await screen.findByRole("alert")).textContent).toContain(
            "Frame no disponible para esta sesión",
        );
        expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
    });

    it("resets a selection when the configured report changes", async () => {
        const replacement = {
            ...navigable(),
            sessionId: "replacement-session",
            reportSha256: "B".repeat(64),
        };
        vi.stubGlobal(
            "fetch",
            vi
                .fn()
                .mockResolvedValueOnce(jsonResponse(navigable()))
                .mockResolvedValueOnce(
                    jsonResponse({ code: "ReportChanged" }, 409),
                )
                .mockResolvedValueOnce(jsonResponse(replacement)),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        fireEvent.click(screen.getByRole("button", { name: "Anterior" }));
        expect(await screen.findByText("replacement-session")).toBeTruthy();
        expect(screen.getByText(/El reporte cambió/)).toBeTruthy();
        expect(fetch).toHaveBeenLastCalledWith(
            "http://localhost:5080/api/nasdaq/historical-demo",
            expect.anything(),
        );
    });

    it("ignores a late response after API/session replacement even if fetch ignores abort", async () => {
        let stale!: (response: Response) => void;
        const mock = vi
            .fn()
            .mockImplementationOnce(
                () =>
                    new Promise<Response>((resolve) => {
                        stale = resolve;
                    }),
            )
            .mockResolvedValueOnce(
                jsonResponse({ ...navigable(7998), sessionId: "new-session" }),
            );
        vi.stubGlobal("fetch", mock);
        const view = render(
            <NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />,
        );
        view.rerender(
            <NasdaqHistoricalDemo apiBaseUrl="http://localhost:5090" />,
        );
        await screen.findByText("new-session");
        await act(async () => stale(jsonResponse(navigable())));
        expect(screen.getByText("source frame 7998")).toBeTruthy();
        expect(screen.queryByText("source frame 8000")).toBeNull();
        expect(mock.mock.calls[1][0]).toBe(
            "http://localhost:5090/api/nasdaq/historical-demo",
        );
    });

    it("ignores an older frame response after return-to-last supersedes it", async () => {
        let stale!: (response: Response) => void;
        vi.stubGlobal(
            "fetch",
            vi
                .fn()
                .mockResolvedValueOnce(jsonResponse(navigable()))
                .mockImplementationOnce(
                    () =>
                        new Promise<Response>((resolve) => {
                            stale = resolve;
                        }),
                )
                .mockResolvedValueOnce(jsonResponse(navigable())),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        fireEvent.click(screen.getByRole("button", { name: "Anterior" }));
        expect(screen.getByText("Cargando reporte histórico.")).toBeTruthy();
        fireEvent.click(screen.getByRole("button", { name: "Último frame" }));
        await screen.findByText("source frame 8000");
        await act(async () => stale(jsonResponse(navigable(7999))));
        expect(screen.getByText("source frame 8000")).toBeTruthy();
        expect(screen.queryByText("source frame 7999")).toBeNull();
    });
});

describe("Nasdaq historical demo", () => {
    it("shows loading without execution controls", () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(() => new Promise<Response>(() => undefined)),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        expect(screen.getByRole("status").textContent).toContain("Cargando");
        expect(
            screen
                .getByRole("button", { name: "Último frame" })
                .getAttribute("disabled"),
        ).not.toBeNull();
        expect(
            screen.queryByRole("button", { name: /run|ejecutar/i }),
        ).toBeNull();
    });

    it("shows canonical NO_TRADE, provenance, blockers and unavailable factual operation", async () => {
        respond();
        expect(await screen.findByText(report.sessionId!)).toBeTruthy();
        expect(screen.getAllByText("NO_TRADE (NoTrade)")).toHaveLength(2);
        expect(screen.getByText("2026-06-29")).toBeTruthy();
        expect(screen.getByText(/test-demo-provider \/ US100/)).toBeTruthy();
        expect(
            screen.getByText("Evaluación factual: Unavailable"),
        ).toBeTruthy();
        expect(
            screen.getByText(/No hay una operación histórica factual completa/),
        ).toBeTruthy();
        expect(screen.getByText(/SHA-256:/).textContent).toContain(
            report.reportSha256,
        );
        expect(
            screen.getByText(/Contenido de evidencias:/).textContent,
        ).toContain("NotRevalidated");
        expect(
            screen.queryByText(/PnL|R-multiple|Entry:|Take Profit:/),
        ).toBeNull();
        expect(
            screen.queryByRole("button", { name: /run|ejecutar/i }),
        ).toBeNull();
        expect(fetch).toHaveBeenCalledWith(
            "http://localhost:5080/api/nasdaq/historical-demo",
            expect.objectContaining({ cache: "no-store" }),
        );
    });

    it.each(["HumanValidationRequired", "DataUnavailable", "Wait"])(
        "preserves %s without converting it into NoTrade",
        async (status) => {
            const frame = report.selectedSessionLastFrame!;
            respond({
                ...report,
                finalVerdict: status,
                selectedSessionLastFrame: {
                    ...frame,
                    verdict: status,
                    rules: [
                        {
                            ...frame.rules[0],
                            result: status === "Wait" ? "Waiting" : status,
                        },
                    ],
                },
            });
            await screen.findByText(report.sessionId!);
            expect(screen.getAllByText(status).length).toBeGreaterThanOrEqual(
                2,
            );
            expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
        },
    );

    it("shows absent required evaluations and input/binding diagnostics", async () => {
        respond({
            ...report,
            inputDiagnosticCodes: ["MissingTimeframe"],
            bindingDiagnosticCodes: ["PrerequisiteUnavailable"],
            selectedSessionLastFrame: {
                ...report.selectedSessionLastFrame,
                hasCompleteRequiredCoverage: false,
                missingRequiredRuleIds: ["NQ-H4-001"],
            },
        });
        expect(
            await screen.findByText(
                /Reglas obligatorias sin evaluación: NQ-H4-001/,
            ),
        ).toBeTruthy();
        expect(
            screen.getByText(/Diagnósticos de entrada: MissingTimeframe/),
        ).toBeTruthy();
        expect(
            screen.getByText(
                /Diagnósticos de evidencia humana: PrerequisiteUnavailable/,
            ),
        ).toBeTruthy();
    });

    it("keeps an absent session frame unavailable instead of using a different day", async () => {
        respond({
            ...report,
            selectedSessionLastFrame: null,
            finalVerdict: null,
        });
        expect(
            await screen.findByText(
                /No hay un frame disponible para la sesión seleccionada/,
            ),
        ).toBeTruthy();
        expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
    });

    it("shows factual availability and human review separately from a trading verdict", async () => {
        respond({
            ...report,
            selectedSessionLastFrame: {
                ...report.selectedSessionLastFrame,
                factualEvaluationState: "Available",
                factualRequiresHumanValidation: true,
            },
        });
        expect(
            await screen.findByText("Evaluación factual: Available"),
        ).toBeTruthy();
        expect(
            screen.getByText(
                "La evaluación factual requiere validación humana.",
            ),
        ).toBeTruthy();
        expect(
            screen.queryByText(
                /No hay una operación histórica factual completa/,
            ),
        ).toBeNull();
    });

    it.each([
        [404, /Reporte histórico no disponible/],
        [422, /reporte histórico es inválido/],
        [503, /No se pudo consultar el reporte histórico/],
    ])(
        "handles HTTP %s without presenting a successful report",
        async (status, message) => {
            respond({}, status as number);
            expect((await screen.findByRole("alert")).textContent).toMatch(
                message as RegExp,
            );
            expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
        },
    );

    it("handles connection failure", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn().mockRejectedValue(new TypeError("Failed to fetch")),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        expect((await screen.findByRole("alert")).textContent).toContain(
            "No se pudo consultar",
        );
    });
});

// Synthetic bounded transport fixture; no price or trade is supplied.
function boundedReport(step = 7596): NasdaqHistoricalReport {
    const steps = [7591, 7592, 7593, 7594, 7595, 7596];
    const times = [
        "2026-09-08T13:31:00Z",
        "2026-09-08T13:32:00Z",
        "2026-09-08T13:33:00Z",
        "2026-09-08T13:34:00Z",
        "2026-09-08T13:35:00Z",
        "2026-09-08T13:36:00Z",
    ];
    const index = steps.indexOf(step);
    return {
        ...report,
        sessionId: null,
        tradingDay: "2026-09-08",
        schemaVersion: 1,
        schema: "moneyway.canonical-preentry-window",
        reportScope: "BoundedCanonicalDiagnostic",
        frameCount: 6,
        evaluatedPrefixFrameCount: 7596,
        evaluatedPrefixStartUtc: "2026-08-31T22:01:00Z",
        evaluatedPrefixEndUtc: times[5],
        replayStartUtc: times[0],
        replayEndUtc: times[5],
        finalVerdict: null,
        canonicalRuntimeCommit: "synthetic:runtime",
        sourceRecordIds: index >= 3 ? ["early", "reversal"] : ["early"],
        selectedSessionLastFrame: null,
        selectedFrame: {
            ...report.selectedSessionLastFrame!,
            step,
            asOfUtc: times[index],
            verdict: "Wait",
            blockingRuleId:
                index < 3
                    ? "NQ-WF2-001"
                    : index === 3
                      ? "NQ-WF2-002"
                      : "NQ-SL-001",
            blockingResult: "Waiting",
            reason: `source canonical ${step}`,
            rules: ["NQ-WF2-001", "NQ-WF2-002", "NQ-SL-001", "NQ-RISK-001"].map(
                (id, i) => ({
                    ruleId: id,
                    definitionStatus: "Confirmed",
                    result:
                        (i === 0 && index >= 3) || (i === 1 && index >= 4)
                            ? "Passed"
                            : "Waiting",
                    sequence: 90 + i * 10,
                    isRequired: true,
                    reason: `source rule ${id} ${step}`,
                    evaluatedAtUtc: times[index],
                    evidenceId: null,
                }),
            ),
        },
        navigation: {
            firstStep: 7591,
            lastStep: 7596,
            selectedStep: step,
            previousStep: steps[index - 1] ?? null,
            nextStep: steps[index + 1] ?? null,
            sessionFrameCount: 6,
        },
    };
}

describe("Bounded canonical diagnostic", () => {
    it("distinguishes the window from prefix and omits final session results and invented evidence", async () => {
        respond(boundedReport());
        expect(
            await screen.findByText("Diagnóstico canónico acotado"),
        ).toBeTruthy();
        expect(
            screen.getByText("Frames exportados de la ventana"),
        ).toBeTruthy();
        expect(screen.getByText("Frames del prefijo evaluado")).toBeTruthy();
        expect(screen.getByText("6")).toBeTruthy();
        expect(screen.getByText("7596")).toBeTruthy();
        expect(
            screen.queryByText("Resultado final del reporte completo"),
        ).toBeNull();
        expect(
            screen.getByText(/No se aporta un resultado final de sesión/),
        ).toBeTruthy();
        expect(screen.getByText(/Hora America\/Bogota:.*8:36:00/)).toBeTruthy();
        expect(
            screen.getAllByText(/2026-09-08T13:36:00Z/).length,
        ).toBeGreaterThan(0);
        expect(screen.getAllByText("NQ-SL-001").length).toBeGreaterThan(0);
        expect(screen.getAllByText("NQ-RISK-001").length).toBeGreaterThan(0);
        expect(screen.queryByText("Ready")).toBeNull();
        expect(screen.queryByText(/29662/)).toBeNull();
        expect(
            screen.getByText(/No hay una operación histórica factual completa/),
        ).toBeTruthy();
        expect(
            screen.getByText(/moneyway.canonical-preentry-window/),
        ).toBeTruthy();
        expect(screen.queryByText(/EvidenceId:/)).toBeNull();
    });

    it("preserves Step navigation and WF2 transitions without later evidence on an earlier frame", async () => {
        const fetchMock = vi.fn((url: string) =>
            Promise.resolve(
                jsonResponse(
                    boundedReport(
                        Number(
                            new URL(url).searchParams.get("frameStep") ?? 7596,
                        ),
                    ),
                ),
            ),
        );
        vi.stubGlobal("fetch", fetchMock);
        const user = userEvent.setup();
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source canonical 7596");
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source canonical 7595");
        expect(screen.getAllByText("Passed")).toHaveLength(2);
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source canonical 7594");
        expect(screen.getAllByText("Passed")).toHaveLength(1);
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source canonical 7593");
        expect(screen.queryByText("Passed")).toBeNull();
        expect(screen.queryByText(/early, reversal/)).toBeNull();
        expect(screen.queryByText(/source rule .*7596/)).toBeNull();
        expect(String(fetchMock.mock.calls.at(-1)?.[0])).toContain(
            "frameStep=7593",
        );
        await user.click(screen.getByRole("button", { name: "Último frame" }));
        await screen.findByText("source canonical 7596");
    });
});
