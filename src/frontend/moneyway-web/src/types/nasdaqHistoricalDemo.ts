export interface HistoricalRule {
    ruleId: string;
    definitionStatus: string;
    result: string;
    sequence: number;
    isRequired: boolean;
    reason: string;
    evaluatedAtUtc: string;
    evidenceId: string | null;
}

export interface HistoricalFrame {
    step: number;
    asOfUtc: string;
    verdict: string | null;
    reason: string | null;
    blockingRuleId: string | null;
    blockingResult: string | null;
    hasCompleteRequiredCoverage: boolean | null;
    missingRequiredRuleIds: string[];
    snapshotState: string | null;
    snapshotDiagnostic: string | null;
    factualEvaluationState: string | null;
    factualRequiresHumanValidation: boolean | null;
    rules: HistoricalRule[];
    tradeLifecycle?: HistoricalTradeLifecycle | null;
}

export interface HistoricalTradeLifecycle {
    path: string;
    stages: {
        stage: string;
        status: string;
        ruleId: string | null;
        ruleResult: string | null;
        reason: string;
    }[];
    entry: { price: number; effectiveAtUtc: string } | null;
    stopPrice: number | null;
    takeProfitPrice: number | null;
    riskRatio: number | null;
    exit: { price: number; effectiveAtUtc: string } | null;
    exitScope: string | null;
    documentedExitReason: string | null;
    direction?: string | null;
    accountBasisAmount?: number | null;
    reportedMaximumLoss?: number | null;
    riskCurrency?: string | null;
    failureClassification: string;
    failedRuleIds: string[];
    sources: {
        inputType: string;
        observedAtUtc: string;
        sourceReference: string;
        sourceReferenceSha256: string;
        truncated: boolean;
    }[];
    omittedSources: number;
    evidenceScope: string;
    completeSupervisedTrade: boolean;
}

export interface NasdaqHistoricalReport {
    sessionId: string | null;
    strategyId: string;
    strategyVersion: string;
    providerId: string;
    symbol: string;
    tradingDay: string;
    schemaVersion: number;
    reportScope?: "FullHistoricalReport" | "BoundedCanonicalDiagnostic";
    schema?: string | null;
    evaluatedPrefixFrameCount?: number | null;
    evaluatedPrefixStartUtc?: string | null;
    evaluatedPrefixEndUtc?: string | null;
    canonicalRuntimeCommit?: string | null;
    sourceRecordIds?: string[] | null;
    frameCount: number;
    finalVerdict: string | null;
    replayStartUtc: string | null;
    replayEndUtc: string | null;
    reportSha256: string;
    reportBytes: number;
    validation: string;
    evidenceValidation: string;
    inputDiagnosticCodes: string[];
    bindingDiagnosticCodes: string[];
    selectedSessionLastFrame: HistoricalFrame | null;
    selectedFrame?: HistoricalFrame | null;
    navigation?: {
        firstStep: number | null;
        lastStep: number | null;
        selectedStep: number | null;
        previousStep: number | null;
        nextStep: number | null;
        sessionFrameCount: number;
    } | null;
}
