import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import {
    PreparationArtifactPanel,
    type PreparationArtifact,
} from "./PreparationArtifactPanel";
import type { PreparationView } from "./ProspectivePreparationPanel";

afterEach(() => vi.unstubAllGlobals());
const encoded =
    "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAADklEQVR4nGP4z8AAQv8BD/kD/YURmXYAAAAASUVORK5CYII=";
const bytes = Uint8Array.from(atob(encoded), (c) => c.charCodeAt(0));
const hash = "6fe8b897998e41db82f635c2bc1ee3c552369debf7fe862f043f6c6feb2bb60b";
const artifact: PreparationArtifact = {
    artifactId: "11111111-1111-4111-8111-111111111111",
    attachedInRevision: 2,
    byteLength: 71,
    sha256: hash,
    receivedAtUtc: "2026-11-18T13:04:00Z",
    receiptClockSource: "synthetic ordinary clock",
    source: {
        originalFilename: "synthetic.png",
        contentType: "image/png",
        sourceProviderId: "synthetic-provider",
        originalSymbol: "US100",
        sourceDescription: "synthetic pixels, not market data",
        revisionReason: "synthetic upload",
        observationReference: "H4",
        originalTime: null,
        originalClockDomain: null,
        timePrecision: "Unknown",
    },
};
function view(artifacts: PreparationArtifact[] = []) {
    return {
        record: {
            recordId: "22222222-2222-4222-8222-222222222222",
            revision: artifacts.length ? 2 : 1,
            artifacts,
            evidence: {
                sourceProviderId: "synthetic-provider",
                originalSymbol: "US100",
                references: [
                    {
                        localReferenceId: "documentary:HH",
                        category: "Structure",
                        role: "HH",
                    },
                ],
            },
        },
        revisionHash: "a".repeat(64),
    } as PreparationView;
}
function mount(preparation = view()) {
    const attached = vi.fn();
    const lock = vi.fn();
    render(
        <PreparationArtifactPanel
            apiBaseUrl=""
            sessionKey="synthetic-session"
            preparation={preparation}
            onAttached={attached}
            onLockChange={lock}
            disabled={false}
        />,
    );
    return { attached, lock };
}
function fill(
    file = new File([bytes], "synthetic.png", { type: "image/png" }),
) {
    fireEvent.change(screen.getByLabelText("Archivo PNG original"), {
        target: { files: [file] },
    });
    fireEvent.change(
        screen.getByLabelText("Descripción de fuente del adjunto"),
        { target: { value: "synthetic pixels, not market data" } },
    );
    fireEvent.change(screen.getByLabelText("Motivo de revisión con adjunto"), {
        target: { value: "synthetic upload" },
    });
}
function submit() {
    fireEvent.submit(
        screen
            .getByRole("button", {
                name: "Preservar adjunto en nueva revisión",
            })
            .closest("form")!,
    );
}
it("uploads exact selected bytes and annotations against the immutable persisted preparation revision", async () => {
    const fetcher = vi
        .fn()
        .mockResolvedValue({ ok: true, json: async () => view([artifact]) });
    vi.stubGlobal("fetch", fetcher);
    const { attached, lock } = mount();
    fill();
    fireEvent.change(screen.getByLabelText("Observación asociada"), {
        target: { value: "H4" },
    });
    submit();
    await waitFor(() => expect(attached).toHaveBeenCalledOnce());
    const body = JSON.parse(fetcher.mock.calls[0][1].body);
    expect(body.bytes).toBe(encoded);
    expect(body.expectedRevision).toBe("a".repeat(64));
    expect(body.preparationRevision).toBe(1);
    expect(body.source.originalTime).toBeNull();
    expect(body.source.observationReference).toBe("H4");
    expect(body.source).not.toHaveProperty("trusted");
    expect(body).not.toHaveProperty("receivedAtUtc");
    expect(lock).toHaveBeenLastCalledWith(false);
});
it("retrieves controlled original bytes, verifies digest and exposes only a safe image blob and download", async () => {
    const digest = Uint8Array.from(hash.match(/../g)!, (h) =>
        parseInt(h, 16),
    ).buffer;
    vi.stubGlobal("crypto", {
        subtle: { digest: vi.fn().mockResolvedValue(digest) },
    });
    const create = vi.fn().mockReturnValue("blob:synthetic-png");
    const revoke = vi.fn();
    vi.stubGlobal(
        "URL",
        class extends URL {
            static createObjectURL = create;
            static revokeObjectURL = revoke;
        },
    );
    const fetcher = vi
        .fn()
        .mockResolvedValueOnce({ ok: true, json: async () => artifact })
        .mockResolvedValueOnce({
            ok: true,
            headers: new Headers({ "content-type": "image/png" }),
            arrayBuffer: async () => bytes.buffer,
        });
    vi.stubGlobal("fetch", fetcher);
    mount(view([artifact]));
    fireEvent.click(
        screen.getByRole("button", {
            name: "Inspeccionar original synthetic.png",
        }),
    );
    expect(await screen.findByRole("img")).toHaveProperty(
        "src",
        "blob:synthetic-png",
    );
    expect(
        screen.getByRole("link", {
            name: "Descargar bytes originales synthetic.png",
        }),
    ).toHaveProperty("download", "synthetic.png");
    expect(fetcher.mock.calls[0][0]).toContain("/revisions/2/artifacts/");
    expect(fetcher.mock.calls[1][0]).toMatch(/\/content$/);
    expect(
        screen.getByText(/Integridad SHA256 ≠ disponibilidad UTC histórica/),
    ).not.toBeNull();
});
it("fails closed on changed artifact identity without rendering a preview", async () => {
    vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({ ...artifact, sha256: "0".repeat(64) }),
        }),
    );
    mount(view([artifact]));
    fireEvent.click(
        screen.getByRole("button", {
            name: "Inspeccionar original synthetic.png",
        }),
    );
    expect(
        await screen.findByText("Identidad del adjunto no coincide."),
    ).not.toBeNull();
    expect(screen.queryByRole("img")).toBeNull();
});
it("retries uncertain upload with the identical submission and bytes", async () => {
    const fetcher = vi
        .fn()
        .mockRejectedValueOnce(new Error("synthetic lost reply"))
        .mockResolvedValueOnce({
            ok: true,
            json: async () => view([artifact]),
        });
    vi.stubGlobal("fetch", fetcher);
    const { attached } = mount();
    fill();
    submit();
    fireEvent.click(
        await screen.findByRole("button", { name: "Reintentar mismo adjunto" }),
    );
    await waitFor(() => expect(attached).toHaveBeenCalledOnce());
    expect(fetcher.mock.calls[1][1].body).toBe(fetcher.mock.calls[0][1].body);
});
it("rejects unsupported selected files locally and unlocks editing without a request", async () => {
    const fetcher = vi.fn();
    vi.stubGlobal("fetch", fetcher);
    const { lock } = mount();
    fill(new File(["<script>"], "synthetic.html", { type: "text/html" }));
    submit();
    expect(
        await screen.findByText("Selecciona un PNG original de hasta 4 MiB."),
    ).not.toBeNull();
    expect(fetcher).not.toHaveBeenCalled();
    expect(lock).toHaveBeenLastCalledWith(false);
});
it("shows definitive revision conflicts and permits reload instead of retaining an obsolete retry", async () => {
    vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue({
            ok: false,
            status: 409,
            json: async () => ({
                code: "EvidenceConflict",
                detail: "PreparationRevisionConflict_ReloadOriginal",
            }),
        }),
    );
    const { lock } = mount();
    fill();
    submit();
    await screen.findByText(/PreparationRevisionConflict_ReloadOriginal/);
    expect(
        screen.queryByRole("button", { name: "Reintentar mismo adjunto" }),
    ).toBeNull();
    expect(lock).toHaveBeenLastCalledWith(false);
});
