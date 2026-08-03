import { useEffect, useState } from "react";
import "./App.css";

type ApiState = "loading" | "connected" | "disconnected";
interface SystemStatus {
    application: string;
    status: string;
    executionMode: string;
    realMoneyTradingEnabled: boolean;
}
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";

export default function App() {
    const [apiState, setApiState] = useState<ApiState>("loading");
    const [systemStatus, setSystemStatus] = useState<SystemStatus | null>(null);

    useEffect(() => {
        const controller = new AbortController();
        async function loadStatus() {
            try {
                const response = await fetch(
                    `${apiBaseUrl}/api/system/status`,
                    { signal: controller.signal },
                );
                if (!response.ok) throw new Error("API unavailable");
                setSystemStatus((await response.json()) as SystemStatus);
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

    return (
        <main>
            <header className="hero">
                <p className="eyebrow">Supervised · Auditable · Versioned</p>
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
                        <dt>Execution</dt>
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
                </dl>
            </section>
            <section aria-labelledby="strategies-heading">
                <h2 id="strategies-heading">Strategy boundaries</h2>
                <div className="cards">
                    {["MoneyWay Forex", "MoneyWay Nasdaq"].map((strategy) => (
                        <article key={strategy}>
                            <h3>{strategy}</h3>
                            <p>Audited documentation available.</p>
                            <p>
                                Deterministic engine not implemented. Execution
                                disabled.
                            </p>
                        </article>
                    ))}
                </div>
            </section>
            <aside aria-label="Safety notice">
                No market analysis or order execution is available. Real-money
                trading is prohibited. Next: build the deterministic domain
                safely.
            </aside>
        </main>
    );
}
