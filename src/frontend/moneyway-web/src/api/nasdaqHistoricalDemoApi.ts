import type { NasdaqHistoricalReport } from "../types/nasdaqHistoricalDemo";

export class HistoricalReportError extends Error {
    readonly status: number;
    readonly code: string | undefined;

    constructor(status: number, code?: string) {
        super("Historical report unavailable");
        this.status = status;
        this.code = code;
    }
}

export async function getNasdaqHistoricalReport(
    apiBaseUrl: string,
    signal: AbortSignal,
    selection?: { frameStep?: number; reportSha256?: string },
): Promise<NasdaqHistoricalReport> {
    const query = new URLSearchParams();
    if (selection?.frameStep !== undefined)
        query.set("frameStep", String(selection.frameStep));
    if (selection?.reportSha256)
        query.set("reportSha256", selection.reportSha256);
    const suffix = query.size ? `?${query}` : "";
    const response = await fetch(
        `${apiBaseUrl}/api/nasdaq/historical-demo${suffix}`,
        {
            signal,
            cache: "no-store",
        },
    );
    if (!response.ok) {
        const problem = await response.json().catch(() => null);
        throw new HistoricalReportError(
            response.status,
            typeof problem?.code === "string" ? problem.code : undefined,
        );
    }
    return response.json();
}
