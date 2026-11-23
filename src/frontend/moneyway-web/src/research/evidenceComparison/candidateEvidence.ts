import {
    frames,
    requireValue,
    type Frame,
    type OriginalPackage,
} from "../integratedReview/originalPackage";
import {
    reference,
    type CandleReference,
    type Target,
} from "../integratedReview/reviewHistory";
import {
    atMost,
    sameTime,
    timeKey,
    type Evidence,
} from "../observation/sessionEvidence";
import type { SessionReport } from "../observation/sessionReport";

type TimedId = { id: string; at: string | null };
export type CandidateEvent = {
    id: string;
    type: Target;
    description: string;
    observations: TimedId[];
    references: CandleReference[];
    at: string | null;
    configurations: TimedId[];
    associationCount: number;
};

export const candleKey = (c: CandleReference) =>
    JSON.stringify([
        c.timeframe,
        c.originalSha256,
        c.nativeOpen,
        timeKey(c.openUtc),
        timeKey(c.closeUtc),
    ]);

type Bucket = {
    event: CandidateEvent;
    observations: Map<string, string | null>;
    configurations: Map<string, string | null>;
    associations: Set<string>;
};

function retainFirst(
    ids: Map<string, string | null>,
    id: string,
    at: string | null,
) {
    const prior = ids.get(id);
    if (
        prior === undefined ||
        (at !== null && (prior === null || atMost(at, prior)))
    )
        ids.set(id, at);
}

export function candidateEvents(
    p: OriginalPackage,
    report: SessionReport,
    selectedConfiguration: string | null,
) {
    const buckets = new Map<string, Bucket>();
    const configurations = new Set<string>();
    const byOpen = Object.fromEntries(
        frames.map((tf) => [
            tf,
            new Map(
                p.sources[tf].bars.map((bar) => [timeKey(bar.openUtc), bar]),
            ),
        ]),
    ) as Record<
        Frame,
        Map<string, OriginalPackage["sources"][Frame]["bars"][number]>
    >;
    const evidenceCache = new WeakMap<Evidence, CandleReference[]>();

    function resolve(e: Evidence) {
        const cached = evidenceCache.get(e);
        if (cached) return cached;
        const candles = (e.candles ?? []).map((c) => ({
            timeframe:
                (c.timeframe.unit === "Hour" ? "H" : "M") + c.timeframe.amount,
            openUtc: c.openTimeUtc,
            closeUtc: c.closeTimeUtc,
            ohlc: c,
        }));
        const refs = [
            ...candles,
            ...(e.sourceCoordinates ?? []).map((c) => ({ ...c, ohlc: null })),
        ].map((c) => {
            const tf = frames.find((f) => f === c.timeframe);
            const original = tf && byOpen[tf].get(timeKey(c.openUtc));
            requireValue(
                tf &&
                    original &&
                    sameTime(original.closeUtc, c.closeUtc) &&
                    (!c.ohlc ||
                        (original.open === c.ohlc.open &&
                            original.high === c.ohlc.high &&
                            original.low === c.ohlc.low &&
                            original.close === c.ohlc.close &&
                            original.volume === c.ohlc.volume)),
                "La vela del informe no coincide con la fuente original y su intervalo exacto.",
            );
            return reference(p, tf, original.openUtc);
        });
        const unique = [
            ...new Map(refs.map((r) => [candleKey(r), r])).values(),
        ];
        evidenceCache.set(e, unique);
        return unique;
    }

    function include(
        type: Target,
        e: Evidence,
        ids: string[],
        association: string,
        observedAt = e.at,
    ) {
        if (
            selectedConfiguration !== null &&
            !ids.includes(selectedConfiguration) &&
            type !== "H1Liquidity"
        )
            return;
        const refs = resolve(e);
        requireValue(
            !observedAt ||
                (atMost(observedAt, report.cutoff) &&
                    refs.every((r) => atMost(r.closeUtc, observedAt))),
            "La evidencia experimental excede su propio corte.",
        );
        // These are existing candidate counting identities, supported by exact source candles.
        // Event correspondence with a human target is a separate decision.
        const shared =
            (type === "M5SC" || type === "M1Realignment") &&
            refs.length === 1 &&
            e.direction
                ? JSON.stringify([type, e.direction, candleKey(refs[0])])
                : JSON.stringify([type, e.id]);
        let bucket = buckets.get(shared);
        if (!bucket) {
            bucket = {
                event: {
                    id: shared,
                    type,
                    description: e.kind,
                    observations: [],
                    references: refs,
                    at: observedAt,
                    configurations: [],
                    associationCount: 0,
                },
                observations: new Map(),
                configurations: new Map(),
                associations: new Set(),
            };
            buckets.set(shared, bucket);
        } else {
            requireValue(
                JSON.stringify(
                    bucket.event.references.map(candleKey).sort(),
                ) === JSON.stringify(refs.map(candleKey).sort()),
                "Una identidad experimental contiene fuentes contradictorias.",
            );
            if (
                observedAt &&
                (!bucket.event.at || atMost(observedAt, bucket.event.at))
            )
                bucket.event.at = observedAt;
        }
        retainFirst(bucket.observations, e.id, observedAt);
        for (const id of ids) {
            if (
                selectedConfiguration === null ||
                id === selectedConfiguration
            ) {
                retainFirst(bucket.configurations, id, observedAt);
                bucket.associations.add(
                    JSON.stringify([
                        association,
                        id,
                        e.id,
                        observedAt && timeKey(observedAt),
                    ]),
                );
            }
        }
    }

    const h4Configurations = new Map<string, Set<string>>();
    for (const ep of report.episodes) {
        for (const chain of ep.chains) {
            const chainIds = new Set<string>();
            for (const geometry of chain.geometry) {
                const ids = geometry.contexts.flatMap((c) =>
                    c.profiles.map((profile) => {
                        const id = profile + "/" + geometry.id;
                        configurations.add(id);
                        chainIds.add(id);
                        const set =
                            h4Configurations.get(profile) ?? new Set<string>();
                        set.add(id);
                        h4Configurations.set(profile, set);
                        return id;
                    }),
                );
                for (const e of geometry.stop?.references ?? [])
                    include(
                        "StructuralSL",
                        e,
                        ids,
                        chain.key,
                        geometry.stop?.at,
                    );
                for (const e of geometry.target?.references ?? [])
                    include(
                        "StructuralTP",
                        e,
                        ids,
                        chain.key,
                        geometry.target?.at,
                    );
            }
            const ids = [...chainIds];
            if (ep.take) include("LiquidityTake", ep.take, ids, chain.key);
            for (const e of chain.confirmations) {
                if (e.kind === "StructuralChange" || e.kind === "IFVG")
                    include(
                        e.kind === "IFVG" ? "M5IFVG" : "M5SC",
                        e,
                        ids,
                        chain.key,
                    );
            }
            for (const e of chain.zones) include("M5FVG", e, ids, chain.key);
            for (const e of chain.countertrend) {
                const end = e.candles?.at(-1)?.closeTimeUtc ?? e.at;
                include("M1Pullback", e, ids, chain.key, end);
            }
            if (chain.realignment)
                include("M1Realignment", chain.realignment, ids, chain.key);
        }
        if (ep.take && ep.chains.length === 0)
            include("LiquidityTake", ep.take, [], ep.id);
    }
    requireValue(
        selectedConfiguration === null ||
            configurations.has(selectedConfiguration),
        "La configuración exacta del manifiesto no pertenece al informe.",
    );
    for (const profile of report.h4.filter((h) => h.available))
        for (const e of profile.evidence.filter(
            (e) => e.sourceCoordinates?.length,
        ))
            include(
                "H4Context",
                e,
                [...(h4Configurations.get(profile.id) ?? [])],
                profile.id,
            );
    for (const session of report.liquidity)
        for (const e of session.endpoints ?? [])
            include("H1Liquidity", e, [], session.id);

    return [...buckets.values()]
        .map(({ event, observations, configurations, associations }) => ({
            ...event,
            observations: [...observations.keys()]
                .sort()
                .map((id) => ({ id, at: observations.get(id) ?? null })),
            configurations: [...configurations.keys()]
                .sort()
                .map((id) => ({ id, at: configurations.get(id) ?? null })),
            associationCount: associations.size,
        }))
        .sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}
