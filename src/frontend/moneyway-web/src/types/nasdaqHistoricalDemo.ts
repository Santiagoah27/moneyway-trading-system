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
}

export interface NasdaqHistoricalReport {
    sessionId: string;
    strategyId: string;
    strategyVersion: string;
    providerId: string;
    symbol: string;
    tradingDay: string;
    schemaVersion: number;
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
}
