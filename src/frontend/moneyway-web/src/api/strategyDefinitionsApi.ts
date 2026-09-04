import type { StrategyDefinition } from "../types/strategyDefinitions";

export async function getStrategyDefinitions(
    apiBaseUrl: string,
    signal: AbortSignal,
): Promise<StrategyDefinition[]> {
    const response = await fetch(`${apiBaseUrl}/api/strategy-definitions`, {
        signal,
    });

    if (!response.ok) {
        throw new Error("Strategy definitions unavailable");
    }

    return response.json();
}
