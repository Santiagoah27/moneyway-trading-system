export interface StrategyRuleDefinition {
    ruleId: string;
    name: string;
    stage: string;
    sequence: number;
    isRequired: boolean;
    definitionStatus: string;
    description: string;
    sourceReference: string;
}

export interface StrategyDefinition {
    strategyId: string;
    version: string;
    displayName: string;
    specificationReference: string;
    ruleCount: number;
    requiredRuleCount: number;
    rules: StrategyRuleDefinition[];
}
