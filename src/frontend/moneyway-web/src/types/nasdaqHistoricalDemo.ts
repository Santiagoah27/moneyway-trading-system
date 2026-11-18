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
    supervisedSnapshot?: SupervisedHistoricalTradeSnapshot | null;
    historicalEntry?: {
        asOfUtc: string;
        selectionStatus: string;
        scope: string;
        entryAuthorization: string;
        nativeExecutionIndependentlyVerified: boolean;
        failureClassification: string;
        observations: {
            availability: string;
            nativeExecutionStatus: string;
            evidence: {
                reviewId: string;
                sourceReference: string;
                humanValidationIdentity: string;
                origin: string;
                eventKind: string;
                sourcePlatform: string;
                sourceProviderId: string | null;
                sourceSymbol: string | null;
                originalTime: string | null;
                timePrecision: string;
                earliestEventUtc: string | null;
                latestEventUtc: string | null;
                timeMappingReference: string | null;
                historicalAvailableAtUtc: string | null;
                reviewCreatedOn: string | null;
                sourceEntryPriceText: string | null;
                sourcePriceUnit: string | null;
                executionReference: string | null;
            };
            canonicalAtEntry: {
                step: number;
                asOfUtc: string;
                verdict: string | null;
                hasCompleteRequiredCoverage: boolean | null;
                rules: HistoricalRule[];
            } | null;
            issues: string[];
        }[];
    } | null;
    scenario2TakeProfit?: {
        providerId: string;
        symbol: string;
        direction: string;
        referenceKind: string;
        referenceDescription: string;
        referenceSha256: string;
        targetReferencePriceText: string;
        takeProfitPriceText: string;
        priceUnit: string;
        effectiveAtUtc: string;
        observedAtUtc: string;
        source: { sourceReference: string; sourceReferenceSha256: string };
        referenceSource: {
            sourceReference: string;
            sourceReferenceSha256: string;
        };
    } | null;
    scenario2RiskExposure?: {
        accountBasisAmountText: string;
        reportedMaximumLossText: string;
        plannedEntryPriceText: string;
        quantityText: string | null;
        stopPriceText: string;
        riskRatioText: string | null;
        exposure: {
            accountBasisAmount: number;
            currency: string;
            accountBasis: string;
            reportedMaximumLoss: number;
            plannedEntryPrice: number;
            costTreatment: string;
            calculationSourceReference: string;
            quantity: number | null;
            quantityUnit: string | null;
        };
        stopPrice: number;
        riskRatio: number | null;
        ratioExceedsDecimalRange: boolean;
        limit: number;
        isWithinLimit: boolean;
        effectiveAtUtc: string;
        observedAtUtc: string;
        source: { sourceReference: string; sourceReferenceSha256: string };
    } | null;
    scenario2StopLoss?: {
        stopPriceText?: string;
        protectivePriceText?: string;
        referenceId: string;
        protectivePrice: number;
        stopPrice: number;
        effectiveAtUtc: string;
        observedAtUtc: string;
        source: { sourceReference: string; sourceReferenceSha256: string };
        protectiveSource: {
            sourceReference: string;
            sourceReferenceSha256: string;
        };
    } | null;
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

export interface SupervisedHistoricalTradeSnapshot {
    documentaryExit?: HistoricalExitAssessment | null;
    historicalExitObserved?: boolean;
    nativeExitIndependentlyVerified?: boolean;
    entryExitRelationshipIndependentlyVerified?: boolean;
    session: {
        providerId: { value: string };
        symbol: { value: string };
        tradingDay: string;
    };
    frameStep: number;
    asOfUtc: string;
    canonicalVerdict: string | null;
    components: HistoricalTradeLifecycle["stages"];
    missingEvidence: string[];
    instrumentCorrespondence: string;
    historicalEntryObserved: boolean;
    canonicalEligibilityRecordedAtEntry: boolean;
    stopLossSupported: boolean;
    riskExposureSupported: boolean;
    takeProfitSupported: boolean;
    nativeExecutionDocumented: boolean;
    canonicalBindingsConsistent: boolean;
    documentedParameterSetAvailable: boolean;
    documentaryEntry: HistoricalTradeLifecycle["historicalEntry"];
    stopLoss: HistoricalTradeLifecycle["scenario2StopLoss"];
    risk: HistoricalTradeLifecycle["scenario2RiskExposure"];
    takeProfit: HistoricalTradeLifecycle["scenario2TakeProfit"];
    scope: string;
    scenario: string;
    direction: string;
    entryAuthorization: string;
    nativeExecutionIndependentlyVerified: boolean;
    exitSupported: boolean;
    exit: { price: number; effectiveAtUtc: string } | null;
    economicOutcomeSupported: boolean;
    economicOutcome: string | null;
    completeSupervisedTrade: boolean;
    failureClassification: string;
}

export interface HistoricalExitAssessment {
    asOfUtc: string;
    humanValidationRequired: boolean;
    nativeExitIndependentlyVerified: boolean;
    entryExitRelationshipIndependentlyVerified: boolean;
    completeSupervisedTrade: boolean;
    economicOutcomeSupported: boolean;
    observations: {
        evidence: {
            reviewId: string;
            sourceReference: string;
            humanValidationIdentity: string;
            origin: string;
            eventKind: string;
            sourcePlatform: string;
            sourceSymbol: string | null;
            originalTime: string | null;
            originalClockDomain: string | null;
            timePrecision: string;
            nominalEventUtc: string | null;
            effectiveAtUtc: string | null;
            historicalAvailableAtUtc: string | null;
            exitPriceText: string | null;
            quantityText: string | null;
            executionReference: string | null;
            linkedEntryReviewId: string | null;
            associationSourceReference: string | null;
            documentedCloseMechanism: string | null;
            reportedStopProtection: string | null;
            stopProtectionSourceReference: string | null;
            limitations: string | null;
        };
        availability: string;
        observationStatus: string;
        entryAssociation: string;
        nativeExitStatus: string;
        nativeIdentityConsistent: boolean;
        issues: string[];
    }[];
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
