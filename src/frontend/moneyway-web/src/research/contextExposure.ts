// Exposure bookkeeping only. Never edits an original V3 label or score.
export const contextExposureKey = "moneyway.research.context-exposure.v1";
export function contextWasSeen(id: string) {
    try {
        const value: unknown = JSON.parse(
            localStorage.getItem(contextExposureKey) ?? "[]",
        );
        return Array.isArray(value) && value.includes(id);
    } catch {
        return false;
    }
}
export function markContextSeen(ids: string[]) {
    const value: unknown = JSON.parse(
        localStorage.getItem(contextExposureKey) ?? "[]",
    );
    if (!Array.isArray(value) || value.some((v) => typeof v !== "string"))
        throw new Error(
            "Registro de exposición local inválido; declare exposición manualmente en V3.",
        );
    localStorage.setItem(
        contextExposureKey,
        JSON.stringify([...new Set([...value, ...ids])]),
    );
}
