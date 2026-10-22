import type { NasdaqHistoricalReport } from "../types/nasdaqHistoricalDemo";

export class HistoricalReportError extends Error {
    readonly status: number;

    constructor(status: number) {
        super("Historical report unavailable");
        this.status = status;
    }
}

export async function getNasdaqHistoricalReport(
    apiBaseUrl: string,
    signal: AbortSignal,
): Promise<NasdaqHistoricalReport> {
    const response = await fetch(`${apiBaseUrl}/api/nasdaq/historical-demo`, {
        signal,
        cache: "no-store",
    });
    if (!response.ok) throw new HistoricalReportError(response.status);
    return response.json();
}
