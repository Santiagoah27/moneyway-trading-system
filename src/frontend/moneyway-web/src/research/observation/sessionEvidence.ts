import type { Candle } from "../report";

export type Row = Record<string, unknown>;
export type Evidence = {
    id: string;
    at: string | null;
    kind: string;
    direction?: string;
    timeframe?: string;
    profile?: string;
    reason?: string;
    candles?: Candle[];
    sourceCoordinates?: {
        timeframe: string;
        openUtc: string;
        closeUtc: string;
    }[];
    endpoints?: Evidence[];
    details?: string[];
};

export const provider = "oanda-global-markets-mt5-demo";
export const instrument = "US100";
const formatter = new Intl.DateTimeFormat("sv-SE", {
    timeZone: "America/Bogota",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hourCycle: "h23",
});
export const bogotaTime = (value: string) => formatter.format(new Date(value));
export const bogotaClock = (value: string) => bogotaTime(value).slice(11, 16);
export const timeKey = (value: string) =>
    value.slice(0, 19) + (value.match(/\.(\d+)/)?.[1] ?? "").padEnd(7, "0");
export const atMost = (first: string, second: string) =>
    timeKey(first) <= timeKey(second);
export const sameTime = (a: string | null, b: string | null) =>
    a === null || b === null ? a === b : timeKey(a) === timeKey(b);

export function check(condition: unknown, label: string): asserts condition {
    if (!condition)
        throw new Error("Informe incompatible o incompleto · " + label + ".");
}
export function row(value: unknown, label: string): Row {
    check(value && typeof value === "object" && !Array.isArray(value), label);
    return value as Row;
}
export const maybeRow = (value: unknown, label: string) =>
    value === null ? null : row(value, label);
export function rows(value: unknown, label: string): Row[] {
    check(Array.isArray(value), label);
    return value.map((v) => row(v, label));
}
export function str(value: unknown, label: string): string {
    check(
        typeof value === "string" && value.trim() && value.length < 8192,
        label,
    );
    return value;
}
export const optional = (value: unknown, label: string) =>
    value === null ? null : str(value, label);
export function strings(value: unknown, label: string): string[] {
    check(Array.isArray(value), label);
    return value.map((v) => str(v, label));
}
export function num(value: unknown, label: string): number {
    check(typeof value === "number" && Number.isFinite(value), label);
    return value;
}
export const decimal = (value: unknown, label: string) =>
    value === null ? null : num(value, label);
export function count(value: unknown, label: string): number {
    const n = num(value, label);
    check(Number.isSafeInteger(n) && n >= 0, label);
    return n;
}
export function bool(value: unknown, label: string): boolean {
    check(typeof value === "boolean", label);
    return value;
}
export function hash(value: unknown): string {
    const s = str(value, "SHA-256");
    check(/^[0-9a-f]{64}$/.test(s), "SHA-256");
    return s;
}
export function utc(value: unknown, label: string, ceiling?: string): string {
    const s = str(value, label);
    check(
        /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?(?:Z|\+00:00)$/.test(s) &&
            Number.isFinite(Date.parse(s)) &&
            new Date(s).toISOString().slice(0, 19) === s.slice(0, 19),
        label,
    );
    check(
        !ceiling || timeKey(s) <= timeKey(ceiling),
        label + " posterior al corte",
    );
    return s;
}
export const optionalUtc = (v: unknown, label: string, ceiling: string) =>
    v === null ? null : utc(v, label, ceiling);
export function safe(r: Row, label: string) {
    check(
        r.canonicalAdmissionPermitted === false &&
            r.operationalSignal === false,
        label,
    );
}
export function index<T>(
    values: T[],
    id: (value: T) => string,
    label: string,
): Map<string, T> {
    const map = new Map(values.map((value) => [id(value), value]));
    check(map.size === values.length, label + " duplicado");
    return map;
}
export function lookup<T>(map: Map<string, T>, id: string, label: string): T {
    const value = map.get(id);
    check(value !== undefined, label);
    return value;
}
export function gapLabel(value: Row) {
    const start = utc(value.startUtc, "inicio de intervalo"),
        end = utc(value.endUtc, "fin de intervalo");
    check(timeKey(start) < timeKey(end), "intervalo sin fuente");
    return (
        bogotaTime(start) +
        " – " +
        bogotaTime(end) +
        (value.closureEvidenceSha256 || value.documentedClosure === true
            ? " · cierre documentado"
            : "")
    );
}

export function candle(
    value: unknown,
    ceiling: string,
    expected: string,
): Candle {
    const c = row(value, "vela de evidencia");
    const tf = row(c.timeframe, "escala de vela");
    const openTimeUtc = utc(c.openTimeUtc, "apertura de vela", ceiling);
    const closeTimeUtc = utc(c.closeTimeUtc, "cierre de vela", ceiling);
    const open = num(c.open, "apertura OHLC"),
        high = num(c.high, "máximo OHLC");
    const low = num(c.low, "mínimo OHLC"),
        close = num(c.close, "cierre OHLC");
    const amount = count(tf.amount, "escala de vela"),
        unit = str(tf.unit, "unidad de vela");
    check(
        row(c.providerId, "proveedor de vela").value === provider &&
            row(c.symbol, "instrumento de vela").value === instrument &&
            timeKey(openTimeUtc) < timeKey(closeTimeUtc) &&
            high >= Math.max(open, close, low) &&
            low <= Math.min(open, close) &&
            (unit === "Hour" ? "H" : unit === "Minute" ? "M" : "") + amount ===
                expected,
        "identidad u OHLC de vela",
    );
    return {
        providerId: { value: provider },
        symbol: { value: instrument },
        timeframe: { amount, unit },
        openTimeUtc,
        closeTimeUtc,
        open,
        high,
        low,
        close,
        volume: decimal(c.volume, "volumen"),
    };
}
