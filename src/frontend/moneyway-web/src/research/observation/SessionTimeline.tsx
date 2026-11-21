import { useState } from "react";
import { CandleChart } from "../StrategyLab";
import { bogotaTime, timeKey, type Evidence } from "./sessionEvidence";
import type { Chain, Episode, Geometry } from "./sessionChains";
import type { SessionReport } from "./sessionReport";

export const policyLabel = (policy: string) =>
    policy.match(/-(E[123])-V1$/)?.[1] ?? policy;
const number = new Intl.NumberFormat("es-CO", { maximumFractionDigits: 6 });
export const formatNumber = (value: number | null | undefined) =>
    value == null ? "Sin evidencia" : number.format(value);

function distinct(items: Evidence[]): Evidence[] {
    return [
        ...new Map(
            items.map((e) => [
                [e.kind, e.id, e.at ? timeKey(e.at) : ""].join("|"),
                e,
            ]),
        ).values(),
    ];
}

type Stage = {
    key: string;
    title: string;
    frame: string;
    at: string | null;
    status: string;
    evidence: Evidence[];
    notes?: string[];
};
function buildStages(
    report: SessionReport,
    episode: Episode | undefined,
    chain: Chain | undefined,
    geometry: Geometry | undefined,
    h4: SessionReport["h4"][number],
): Stage[] {
    const stages: Stage[] = [];
    function add(
        key: string,
        title: string,
        frame: string,
        status: string,
        items: Evidence[] = [],
        notes?: string[],
    ) {
        const groups = new Map<string, Evidence[]>();
        for (const item of distinct(items)) {
            const id = item.at ? timeKey(item.at) : "";
            const group = groups.get(id) ?? [];
            group.push(item);
            groups.set(id, group);
        }
        if (!groups.size)
            stages.push({
                key,
                title,
                frame,
                at: null,
                status,
                evidence: [],
                notes,
            });
        for (const [t, group] of groups) {
            stages.push({
                key: key + t,
                title,
                frame,
                at: group[0].at,
                status,
                evidence: group,
                notes,
            });
        }
    }
    add(
        "prep",
        "Preparación y contexto",
        "H4",
        h4.reason,
        h4.at
            ? [
                  {
                      id: h4.id,
                      at: h4.at,
                      kind: h4.classification ?? "Contexto H4 no disponible",
                      profile:
                          "EXP-NQ-H4-CONTEXT-V1 / EXP-NQ-H4-MARKET-STRUCTURE-V1",
                      details: h4.details,
                  },
              ]
            : [],
    );
    add(
        "liquidity",
        "Liquidez Asia y Londres",
        "H1",
        "Referencias de sesión",
        report.liquidity,
    );
    add(
        "take",
        "Toma de liquidez",
        "M5",
        chain?.reason ??
            (episode
                ? episode.take
                    ? "Toma observada"
                    : "WaitingForLiquidityTake"
                : "Sin episodio"),
        episode?.take ? [episode.take] : [],
        episode ? [episode.reason] : undefined,
    );
    add(
        "trigger",
        "Primera confirmación SC o IFVG",
        "M5",
        chain?.firstOutcome ?? "Sin confirmación",
        chain?.confirmations,
    );
    add(
        "fvg",
        "FVG direccional posterior",
        "M5",
        chain?.zones.length ? "Zona observada" : "Sin zona direccional",
        chain?.zones,
    );
    add(
        "counter",
        "Contratendencia",
        "M1",
        chain?.countertrend.length
            ? "Contratendencia observada"
            : (chain?.m1Reason ?? "Sin observación"),
        chain?.countertrend,
    );
    add(
        "realignment",
        "Realineación",
        "M1",
        chain?.m1Reason ?? "Sin realineación",
        chain?.realignment ? [chain.realignment] : [],
    );
    function reference(type: "SL" | "TP", observation: Geometry["stop"]) {
        const status =
            observation?.protective === false
                ? "SL no protector"
                : (observation?.reason ?? "Sin referencia observable");
        const frame =
            type === "SL"
                ? "M5"
                : [
                      ...new Set(
                          observation?.references.map((e) => e.timeframe) ?? [],
                      ),
                  ].join(" / ") || "M5 / H1";
        const title = "Referencia estructural " + type;
        add(
            type,
            title,
            frame,
            status,
            observation
                ? [
                      {
                          id: observation.profile,
                          at: observation.at,
                          kind: title,
                          direction: observation.direction,
                          profile: observation.profile,
                          details: [
                              "Nivel " +
                                  formatNumber(observation.price) +
                                  " · Distancia " +
                                  formatNumber(observation.distance),
                              ...observation.gaps.map(
                                  (gap) => "Brecha de fuente " + gap,
                              ),
                              ...observation.references.map(
                                  (e) =>
                                      e.kind +
                                      " · " +
                                      e.id +
                                      (e.at
                                          ? " · Disponible " + bogotaTime(e.at)
                                          : "") +
                                      (e.details?.length
                                          ? " · " + e.details.join(" · ")
                                          : ""),
                              ),
                          ],
                      },
                  ]
                : [],
        );
    }
    reference("SL", geometry?.stop ?? null);
    reference("TP", geometry?.target ?? null);
    if (chain?.invalidations.length)
        add(
            "invalidation",
            "Cronología del contexto",
            "M5",
            "Estado posterior de la evidencia",
            chain.invalidations,
        );
    const full = geometry?.contexts.find((o) => o.profiles.includes(h4.id));
    if (full?.completedAt)
        add(
            "completed",
            "Finalización histórica",
            "H4–M1",
            "Cadena de investigación observada",
            [
                {
                    id: geometry!.id,
                    at: full.completedAt,
                    kind: "Finalización histórica",
                },
            ],
        );
    add(
        "result",
        "Diagnóstico final",
        "Replay",
        full?.outcome ?? report.outcome,
        [
            {
                id: report.runId,
                at: report.cutoff,
                kind: full?.outcome ?? report.outcome,
                details: geometry?.completedAt
                    ? [
                          "Geometría SL/TP observada " +
                              bogotaTime(geometry.completedAt),
                      ]
                    : undefined,
            },
        ],
    );
    return stages.sort((a, b) =>
        (a.at ? timeKey(a.at) : "Z").localeCompare(b.at ? timeKey(b.at) : "Z"),
    );
}

function EvidenceCards({
    stage,
    report,
    chart,
}: {
    stage: Stage;
    report: SessionReport;
    chart?: boolean;
}) {
    const bars = [
        ...new Map(
            stage.evidence
                .flatMap((e) => e.candles ?? [])
                .map((c) => [timeKey(c.openTimeUtc), c]),
        ).values(),
    ].sort((a, b) =>
        timeKey(a.openTimeUtc).localeCompare(timeKey(b.openTimeUtc)),
    );
    const [selected, setSelected] = useState(0);
    const bar = bars[selected];
    return (
        <div className="ob-evidence">
            <p className="ob-state">{stage.status}</p>
            {stage.notes?.map((note) => (
                <p key={note}>{note}</p>
            ))}
            {distinct(stage.evidence).map((item) => (
                <div
                    key={[item.id, item.kind, item.at].join("|")}
                    className="ob-evidence-item"
                >
                    <strong>{item.kind}</strong>
                    {item.direction && (
                        <span>
                            {item.direction === "Bullish"
                                ? "Alcista"
                                : item.direction === "Bearish"
                                  ? "Bajista"
                                  : item.direction}
                        </span>
                    )}
                    {item.reason && <span>{item.reason}</span>}
                    {item.profile && <code>{item.profile}</code>}
                    <code>{item.id}</code>
                    {item.at && <small>Origen temporal {item.at}</small>}
                    {item.details?.map((detail, i) => (
                        <small key={i}>{detail}</small>
                    ))}
                </div>
            ))}
            {chart && bars.length > 0 && (
                <details className="ob-chart-detail">
                    <summary>Inspeccionar velas originales</summary>
                    <CandleChart
                        report={{
                            input: {
                                symbol: report.instrument,
                                timeframe: stage.frame,
                                evaluationCutoffUtc: stage.at ?? report.cutoff,
                            },
                            evidenceCandles: bars,
                            results: [],
                        }}
                        visible={[]}
                        selected={selected}
                        onSelect={setSelected}
                        timeLabel={bogotaTime}
                        timeZoneLabel="America/Bogota"
                    />
                    {bar && (
                        <p>
                            {bogotaTime(bar.openTimeUtc)} –{" "}
                            {bogotaTime(bar.closeTimeUtc)} · O {bar.open} · H{" "}
                            {bar.high} · L {bar.low} · C {bar.close}
                        </p>
                    )}
                </details>
            )}
        </div>
    );
}

export function SessionTimeline({
    report,
    episode,
}: {
    report: SessionReport;
    episode?: Episode;
}) {
    // Use the first stored association with an observed realignment for initial inspection.
    const [chainKey, setChain] = useState(
        episode?.chains.find((c) => c.realignment)?.key ??
            episode?.chains[0]?.key ??
            "",
    );
    const chain = episode?.chains.find((c) => c.key === chainKey);
    const [geometryId, setGeometry] = useState("");
    const geometry =
        chain?.geometry.find((g) => g.id === geometryId) ?? chain?.geometry[0];
    const [h4Id, setH4] = useState(report.h4[0].id);
    const h4 = report.h4.find((p) => p.id === h4Id) ?? report.h4[0];
    const stages = buildStages(report, episode, chain, geometry, h4);
    const [expanded, expand] = useState<string | null>(null);
    const price = geometry?.stop?.close ?? geometry?.target?.close;
    return (
        <section className="ob-timeline" aria-labelledby="ob-timeline-heading">
            <div className="ob-section-heading">
                <h2 id="ob-timeline-heading">Cronología de evidencia</h2>
                <span>America/Bogota</span>
            </div>
            {episode && (
                <h3>
                    {episode.category} · {policyLabel(episode.policy)}
                </h3>
            )}
            {chain && (
                <details className="ob-configuration">
                    <summary>Configuraciones de investigación</summary>
                    <div className="ob-config-grid">
                        <label>
                            Secuencia M5/M1
                            <select
                                value={chain.key}
                                onChange={(e) => {
                                    setChain(e.target.value);
                                    setGeometry("");
                                    expand(null);
                                }}
                            >
                                {episode?.chains.map((c) => (
                                    <option key={c.key} value={c.key}>
                                        {c.configuration
                                            .split("/")
                                            .slice(0, 4)
                                            .join(" · ")}{" "}
                                        · {c.m1Profile}
                                        {c.realignment?.at
                                            ? " · " +
                                              bogotaTime(
                                                  c.realignment.at,
                                              ).slice(11)
                                            : ""}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label>
                            Perfiles SL/TP
                            <select
                                value={geometry?.id ?? ""}
                                onChange={(e) => {
                                    setGeometry(e.target.value);
                                    expand(null);
                                }}
                            >
                                {chain.geometry.map((g) => (
                                    <option key={g.id} value={g.id}>
                                        {g.slProfile} · {g.tpProfile}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label>
                            Perfil H4
                            <select
                                value={h4.id}
                                onChange={(e) => {
                                    setH4(e.target.value);
                                    expand(null);
                                }}
                            >
                                {report.h4.map((p) => (
                                    <option key={p.id} value={p.id}>
                                        {p.id}
                                    </option>
                                ))}
                            </select>
                        </label>
                    </div>
                    <code>{geometry?.id}</code>
                </details>
            )}
            {geometry && (
                <div
                    className="ob-geometry"
                    aria-label="Referencias hipotéticas"
                >
                    <div className="ob-section-heading">
                        <h3>Referencias hipotéticas</h3>
                        <span>{geometry.outcome}</span>
                    </div>
                    <dl className="ob-metrics">
                        <div>
                            <dt>Cierre M1</dt>
                            <dd>{formatNumber(price)}</dd>
                        </div>
                        <div>
                            <dt>SL estructural</dt>
                            <dd>{formatNumber(geometry.stop?.price)}</dd>
                            <small>
                                {geometry.stop?.protective === false
                                    ? "SL no protector"
                                    : "Distancia " +
                                      formatNumber(geometry.stop?.distance)}
                            </small>
                        </div>
                        <div>
                            <dt>TP estructural</dt>
                            <dd>{formatNumber(geometry.target?.price)}</dd>
                            <small>
                                {geometry.target?.price == null
                                    ? "TP no disponible"
                                    : "Distancia " +
                                      formatNumber(geometry.target.distance)}
                            </small>
                        </div>
                        <div>
                            <dt>R:R hipotético</dt>
                            <dd>
                                {geometry.ratio === null
                                    ? "No disponible"
                                    : formatNumber(geometry.ratio)}
                            </dd>
                            <small>
                                {geometry.ratioReason ??
                                    "Requiere ambas distancias"}
                            </small>
                        </div>
                    </dl>
                    {!!chain?.branchStates.length && (
                        <p className="ob-branch-summary">
                            {chain.branchStates.some(
                                (s) => s.atRealignment === "Invalidated",
                            ) && (
                                <strong>
                                    Había contexto M5 invalidado al
                                    realinear.{" "}
                                </strong>
                            )}
                            {chain.branchStates.some(
                                (s) =>
                                    s.atCutoff === "Invalidated" &&
                                    s.atRealignment !== "Invalidated",
                            ) && (
                                <strong>
                                    Contexto M5 invalidado después.{" "}
                                </strong>
                            )}
                            {chain.branchStates.some(
                                (s) => s.atCutoff === "EvidenceLimited",
                            ) && (
                                <strong>
                                    Contexto M5 con evidencia limitada al corte.
                                </strong>
                            )}
                        </p>
                    )}
                </div>
            )}
            <ol
                className="ob-stage-list"
                aria-label="Etapas en orden de observación"
            >
                {stages.map((stage) => {
                    const sharesClose =
                        stage.at &&
                        stages.some(
                            (s) =>
                                s.key !== stage.key &&
                                s.at &&
                                timeKey(s.at) === timeKey(stage.at!),
                        );
                    const open = expanded === stage.key;
                    return (
                        <li key={stage.key}>
                            <button
                                type="button"
                                aria-expanded={open}
                                onClick={() => expand(open ? null : stage.key)}
                            >
                                <span className="ob-stage-frame">
                                    {stage.frame}
                                </span>
                                <span className="ob-stage-name">
                                    <strong>{stage.title}</strong>
                                    <small>{stage.status}</small>
                                    {(sharesClose ||
                                        stage.evidence.length > 1) && (
                                        <small>
                                            Cierre compartido · orden intravela
                                            indeterminado
                                        </small>
                                    )}
                                </span>
                                <span className="ob-stage-time">
                                    {stage.at
                                        ? bogotaTime(stage.at)
                                        : "Sin instante"}
                                </span>
                                <span aria-hidden="true">
                                    {open ? "−" : "+"}
                                </span>
                            </button>
                            {open && (
                                <>
                                    <EvidenceCards
                                        key={stage.key}
                                        stage={stage}
                                        report={report}
                                        chart
                                    />
                                    {stage.key.startsWith("prep") &&
                                        h4.evidence.length > 0 && (
                                            <EvidenceCards
                                                stage={{
                                                    ...stage,
                                                    evidence: h4.evidence,
                                                    status: "Pivotes y observaciones de contexto",
                                                }}
                                                report={report}
                                            />
                                        )}
                                </>
                            )}
                        </li>
                    );
                })}
            </ol>
        </section>
    );
}
