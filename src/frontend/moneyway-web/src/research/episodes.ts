import { readReport } from "./report";

// Fixed, versioned build resources. No caller-supplied path, upload or filesystem API.
export const episodes = [
    { id: "synthetic-v1", label: "Sintético · divergencias A/B/C/D" },
    { id: "june29-v1", label: "29 jun 2026 · US100 MT5 · documental" },
] as const;

export async function loadEpisode(id: string) {
    switch (id) {
        case "synthetic-v1":
            return readReport(
                (await import("./reports/synthetic-v1.json")).default,
            );
        case "june29-v1":
            return readReport(
                (await import("./reports/june29-v1.json")).default,
            );
        default:
            throw new Error(
                "Episodio no registrado. No se leerán rutas arbitrarias.",
            );
    }
}
