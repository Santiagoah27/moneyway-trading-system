import type { SystemStatus } from "../types/system";

export async function getSystemStatus(
    apiBaseUrl: string,
    signal: AbortSignal,
): Promise<SystemStatus> {
    const response = await fetch(`${apiBaseUrl}/api/system/status`, { signal });

    if (!response.ok) {
        throw new Error("API unavailable");
    }

    return response.json();
}
