import { useEffect, useState } from "react";
import { getStrategyDefinitions } from "./api/strategyDefinitionsApi";
import { getSystemStatus } from "./api/systemApi";
import type { StrategyDefinition } from "./types/strategyDefinitions";
import type { SystemStatus } from "./types/system";
import "./App.css";

type ApiState = "loading" | "connected" | "disconnected";
type DefinitionsState = "loading" | "success" | "error";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";

function StrategyCard({ definition }: { definition: StrategyDefinition }) {
    return (
        <article
            className="strategy-definition"
            aria-labelledby={`strategy-${definition.strategyId}`}
        >
            <div className="strategy-title">
                <div>
                    <p className="strategy-id">{definition.strategyId}</p>
                    <h3 id={`strategy-${definition.strategyId}`}>
                        {definition.displayName}
                    </h3>
                </div>
                {definition.version.includes("-draft") && (
                    <span className="badge">Draft</span>
                )}
            </div>
            <dl className="definition-summary">
                <div>
                    <dt>Version</dt>
                    <dd>{definition.version}</dd>
                </div>
                <div>
                    <dt>Total rules</dt>
                    <dd>{definition.ruleCount}</dd>
                </div>
                <div>
                    <dt>Required rules</dt>
                    <dd>{definition.requiredRuleCount}</dd>
                </div>
                <div>
                    <dt>Specification</dt>
                    <dd>{definition.specificationReference}</dd>
                </div>
            </dl>
            <p className="semantic-note">
                Definition status describes how well the rule is documented. It
                is not the result of a market evaluation.
            </p>
            <div className="rules-table-wrapper">
                <table>
                    <caption>
                        Versioned rules for {definition.displayName}
                    </caption>
                    <thead>
                        <tr>
                            <th scope="col">Seq.</th>
                            <th scope="col">Rule</th>
                            <th scope="col">Stage</th>
                            <th scope="col">Required</th>
                            <th scope="col">Definition status</th>
                            <th scope="col">Description</th>
                        </tr>
                    </thead>
                    <tbody>
                        {definition.rules.map((rule) => (
                            <tr key={rule.ruleId}>
                                <td data-label="Seq.">{rule.sequence}</td>
                                <td data-label="Rule">
                                    <strong>{rule.name}</strong>
                                    <small>{rule.ruleId}</small>
                                </td>
                                <td data-label="Stage">{rule.stage}</td>
                                <td data-label="Required">
                                    {rule.isRequired ? "Required" : "Optional"}
                                </td>
                                <td data-label="Definition status">
                                    <span className="badge status-badge">
                                        {rule.definitionStatus}
                                    </span>
                                </td>
                                <td data-label="Description">
                                    {rule.description}
                                    <small>{rule.sourceReference}</small>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </article>
    );
}

export default function App() {
    const [apiState, setApiState] = useState<ApiState>("loading");
    const [systemStatus, setSystemStatus] = useState<SystemStatus | null>(null);
    const [definitionsState, setDefinitionsState] =
        useState<DefinitionsState>("loading");
    const [definitions, setDefinitions] = useState<StrategyDefinition[]>([]);
    const hasNasdaqDefinition = definitions.some(
        (definition) => definition.strategyId === "moneyway-nasdaq",
    );

    useEffect(() => {
        const controller = new AbortController();
        async function loadStatus() {
            try {
                setSystemStatus(
                    await getSystemStatus(apiBaseUrl, controller.signal),
                );
                setApiState("connected");
            } catch (error) {
                if (
                    error instanceof DOMException &&
                    error.name === "AbortError"
                )
                    return;
                setApiState("disconnected");
            }
        }
        void loadStatus();
        return () => controller.abort();
    }, []);

    useEffect(() => {
        const controller = new AbortController();
        async function loadDefinitions() {
            try {
                setDefinitions(
                    await getStrategyDefinitions(apiBaseUrl, controller.signal),
                );
                setDefinitionsState("success");
            } catch (error) {
                if (
                    error instanceof DOMException &&
                    error.name === "AbortError"
                )
                    return;
                setDefinitionsState("error");
            }
        }
        void loadDefinitions();
        return () => controller.abort();
    }, []);

    return (
        <main>
            <header className="hero">
                <p className="eyebrow">
                    Supervised · Auditable · Execution disabled
                </p>
                <h1>MoneyWay Trading System</h1>
                <p className="purpose">
                    A safe foundation for deterministic strategy evaluation and
                    evidence-backed analysis.
                </p>
            </header>
            <section aria-labelledby="status-heading">
                <div className="section-heading">
                    <h2 id="status-heading">System status</h2>
                    <span className={`connection ${apiState}`} role="status">
                        API {apiState}
                    </span>
                </div>
                <dl className="status-grid">
                    <div>
                        <dt>Phase</dt>
                        <dd>{systemStatus?.status ?? "technical bootstrap"}</dd>
                    </div>
                    <div>
                        <dt>Execution mode</dt>
                        <dd>{systemStatus?.executionMode ?? "disabled"}</dd>
                    </div>
                    <div>
                        <dt>Real-money trading</dt>
                        <dd>
                            {systemStatus?.realMoneyTradingEnabled
                                ? "enabled"
                                : "disabled"}
                        </dd>
                    </div>
                    <div>
                        <dt>Strategy engine evaluation</dt>
                        <dd>Not implemented</dd>
                    </div>
                </dl>
            </section>
            <section aria-labelledby="definitions-heading">
                <h2 id="definitions-heading">Strategy Definitions</h2>
                {definitionsState === "loading" && (
                    <p role="status">Strategy definitions loading.</p>
                )}
                {definitionsState === "error" && (
                    <p role="alert">Strategy definitions unavailable.</p>
                )}
                {definitionsState === "success" &&
                    definitions.map((definition) => (
                        <StrategyCard
                            key={`${definition.strategyId}:${definition.version}`}
                            definition={definition}
                        />
                    ))}
                {definitionsState === "success" && !hasNasdaqDefinition && (
                    <article
                        className="nasdaq-status"
                        aria-labelledby="nasdaq-heading"
                    >
                        <h3 id="nasdaq-heading">MoneyWay Nasdaq</h3>
                        <p>Documentation available.</p>
                        <p>Runtime definition: Not registered yet.</p>
                    </article>
                )}
            </section>
            <aside aria-label="Safety notice">
                No market analysis or order execution is available. Real-money
                trading is prohibited.
            </aside>
        </main>
    );
}
