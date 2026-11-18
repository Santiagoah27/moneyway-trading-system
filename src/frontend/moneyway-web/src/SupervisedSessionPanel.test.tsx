import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { SupervisedSessionPanel } from "./SupervisedSessionPanel";

afterEach(() => vi.unstubAllGlobals());
const evidence = {
    recordId: "synthetic:record",
    revision: 1,
    revisionHash: "a".repeat(64),
    sessionId: "synthetic:session",
    tradingDay: "2026-11-18",
    providerId: "synthetic-provider",
    symbol: "US100",
    scenario: "Standard",
};
function state(terminal = false) {
    const close = {
        revision: 3,
        registeredAtUtc: "2026-11-18T14:10:00Z",
        command: {
            action: "ConfirmClosure",
            reviewer: "synthetic:operator",
            sourceReference: "note:synthetic close",
            closureKind: "BreakEven",
            reason: "synthetic evidenced closure",
            execution: evidence,
        },
    };
    return {
        sessionKey: "synthetic-key",
        sessionId: evidence.sessionId,
        path: "Standard",
        status: terminal ? "Terminal" : "Open_NoTrade",
        revision: terminal ? 3 : 1,
        revisionHash: evidence.revisionHash,
        identity: {
            tradingDay: evidence.tradingDay,
            providerId: { value: evidence.providerId },
            symbol: { value: "US100" },
        },
        terminal,
        localEntryIntentPermitted: !terminal,
        localGuardReason: terminal
            ? "TerminalSession_NoReentryNoNewSetupNoSecondTrade"
            : "LocalTerminalAndTimeGuardPassed_NotCanonicalEntryAuthorization",
        activeSetup: null,
        executionConfirmation: terminal ? close : null,
        closureConfirmation: terminal ? close : null,
        events: terminal ? [close] : [],
    };
}
it("does not convert selected documentary evidence into ownership or send requests on mount", () => {
    const fetcher = vi.fn();
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedSessionPanel apiBaseUrl="" evidence={evidence} />);
    expect(fetcher).not.toHaveBeenCalled();
    expect(screen.queryByRole("heading", { name: /TERMINAL/ })).toBeNull();
});
it("displays recovered terminal provenance and sends a blocked attempt to the actual API guard", async () => {
    const fetcher = vi
        .fn()
        .mockResolvedValueOnce({ ok: true, json: async () => state(true) })
        .mockResolvedValueOnce({
            ok: false,
            status: 409,
            json: async () => ({
                code: "TerminalSession_NoReentryNoNewSetupNoSecondTrade",
            }),
        });
    vi.stubGlobal("fetch", fetcher);
    render(
        <SupervisedSessionPanel
            apiBaseUrl="http://localhost:5097"
            evidence={evidence}
        />,
    );
    fireEvent.click(
        screen.getByRole("button", {
            name: "Abrir o recuperar ámbito de sesión",
        }),
    );
    expect(
        await screen.findByRole("heading", {
            name: "TERMINAL · SIN REENTRADA",
        }),
    ).not.toBeNull();
    expect(screen.getByText(/no verificada por broker/)).not.toBeNull();
    expect(
        screen.getByText(/synthetic evidenced closure/, { selector: "p" }),
    ).not.toBeNull();
    fireEvent.change(
        screen.getByLabelText("Sesión: referencia de setup supervisado"),
        { target: { value: "synthetic:second setup" } },
    );
    expect(
        screen.getByRole("button", { name: "Registrar setup supervisado" }),
    ).toHaveProperty("disabled", true);
    expect(
        screen.getByRole("button", {
            name: "Registrar intento de entrada sin orden",
        }),
    ).toHaveProperty("disabled", true);
    fireEvent.click(
        screen.getByRole("button", { name: "Comprobar rechazo de reentrada" }),
    );
    await waitFor(() =>
        expect(screen.getByRole("status").textContent).toContain(
            "TerminalSession_NoReentryNoNewSetupNoSecondTrade",
        ),
    );
    const [url, options] = fetcher.mock.calls[1];
    expect(url).toContain("/synthetic-key/actions");
    expect(JSON.parse(options.body).action).toBe("EntryIntent");
    expect(
        screen.getByRole("button", { name: "Recuperar estado persistido" }),
    ).toHaveProperty("disabled", false);
});
it("requires a separate explicit ownership confirmation and freezes the selected evidence reference", async () => {
    const fetcher = vi
        .fn()
        .mockResolvedValue({ ok: true, json: async () => state() });
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedSessionPanel apiBaseUrl="" evidence={evidence} />);
    fireEvent.click(
        screen.getByRole("button", {
            name: "Abrir o recuperar ámbito de sesión",
        }),
    );
    const button = await screen.findByRole("button", {
        name: "Registrar ejecución propia ya realizada",
    });
    expect(button).toHaveProperty("disabled", true);
    fireEvent.click(screen.getByLabelText(/Confirmo que MoneyWay ejecutó/));
    fireEvent.click(button);
    await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
    const command = JSON.parse(fetcher.mock.calls[1][1].body);
    expect(command.action).toBe("ConfirmExecution");
    expect(command.moneyWayOwnedExecutionConfirmed).toBe(true);
    expect(command.execution).toEqual({
        recordId: evidence.recordId,
        revision: 1,
        revisionHash: evidence.revisionHash,
    });
    expect(command.moneyWayTradeClosedConfirmed).toBeUndefined();
});
it("does not carry ownership or closed checkboxes to another selected evidence revision", async () => {
    vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue({ ok: true, json: async () => state() }),
    );
    const view = render(
        <SupervisedSessionPanel apiBaseUrl="" evidence={evidence} />,
    );
    fireEvent.click(
        screen.getByRole("button", {
            name: "Abrir o recuperar ámbito de sesión",
        }),
    );
    await screen.findByRole("button", {
        name: "Registrar ejecución propia ya realizada",
    });
    fireEvent.click(screen.getByLabelText(/Confirmo que MoneyWay ejecutó/));
    fireEvent.click(
        screen.getByLabelText(/Confirmo que la operación propia está cerrada/),
    );
    view.rerender(
        <SupervisedSessionPanel
            apiBaseUrl=""
            evidence={{
                ...evidence,
                revision: 2,
                revisionHash: "b".repeat(64),
            }}
        />,
    );
    expect(
        screen.getByLabelText(/Confirmo que MoneyWay ejecutó/),
    ).toHaveProperty("checked", false);
    expect(
        screen.getByLabelText(/Confirmo que la operación propia está cerrada/),
    ).toHaveProperty("checked", false);
    expect(
        screen.getByRole("button", {
            name: "Registrar ejecución propia ya realizada",
        }),
    ).toHaveProperty("disabled", true);
});
