import {
    isPinnedDevelopment,
    isUtc,
    type Frame,
    type OriginalPackage,
} from "./originalPackage";

export const sessionConfiguration = "nasdaq-0.3.0-session-candidate";
const hour = 3_600_000;
const windows = [
    {
        name: "Asia",
        color: "#66aaff",
        hours: 9,
        endHour: "02",
        codes: ["AH", "AL"],
    },
    {
        name: "Londres",
        color: "#ffc363",
        hours: 6,
        endHour: "08",
        codes: ["LH", "LL"],
    },
] as const;

export function sessionOverlays(packet: OriginalPackage, cutoffUtc: string) {
    if (
        !isPinnedDevelopment(packet) ||
        !isUtc(cutoffUtc) ||
        Date.parse(cutoffUtc) > Date.parse(packet.cutoffUtc)
    )
        return [];
    const source = packet.sources.H1;
    const cutoff = Date.parse(cutoffUtc);
    const usableMapping = source.mapping === "DocumentaryAudited";
    // ADR 0028 uses Bogotá local time. This projection is scoped to the pinned June date.
    return windows.map(({ name, color, hours, endHour, codes }) => {
        const end = Date.parse(
            `${packet.operatingDate}T${endHour}:00:00-05:00`,
        );
        const start = end - hours * hour;
        const closed = usableMapping
            ? source.bars.filter((bar) => {
                  const open = Date.parse(bar.openUtc);
                  const close = Date.parse(bar.closeUtc);
                  return (
                      open >= start &&
                      open < end &&
                      close <= end &&
                      close <= cutoff
                  );
              })
            : [];
        const complete =
            closed.length === hours &&
            closed.every(
                (bar, i) =>
                    Date.parse(bar.openUtc) === start + i * hour &&
                    Date.parse(bar.closeUtc) === start + (i + 1) * hour,
            );
        const status:
            "MappingUnavailable" | "SessionOpen" | "Complete" | "Incomplete" =
            !usableMapping
                ? "MappingUnavailable"
                : cutoff < end
                  ? "SessionOpen"
                  : complete
                    ? "Complete"
                    : "Incomplete";
        const levels =
            status === "Complete"
                ? (["high", "low"] as const).map((side, i) => {
                      const prices = closed.map((bar) => bar[side]);
                      const price =
                          side === "high"
                              ? Math.max(...prices)
                              : Math.min(...prices);
                      return {
                          code: codes[i],
                          price,
                          sources: closed.filter((bar) => bar[side] === price),
                      };
                  })
                : [];
        return {
            name,
            color,
            startUtc: new Date(start).toISOString(),
            endUtc: new Date(end).toISOString(),
            cutoffUtc,
            status,
            expected: hours,
            observed: closed.length,
            levels,
            sourceSha256: source.originalSha256,
            provider: packet.provider,
            instrument: packet.instrument,
            operatingDate: packet.operatingDate,
            timeframe: "H1",
        };
    });
}

export type SessionOverlayAssistance = {
    configurationId: typeof sessionConfiguration;
    frame: Frame;
    bands: boolean;
    levels: boolean;
};

export function availableSessionLayers(
    packet: OriginalPackage,
    frame: Frame,
    cutoffUtc: string,
) {
    if (
        !isUtc(cutoffUtc) ||
        Date.parse(cutoffUtc) > Date.parse(packet.cutoffUtc)
    )
        return { bands: false, levels: false };
    const cutoff = Date.parse(cutoffUtc);
    const bars = packet.sources[frame].bars
        .filter((b) => Date.parse(b.closeUtc) <= cutoff)
        .slice(-80);
    const first = bars[0];
    if (!first) return { bands: false, levels: false };
    const sessions = sessionOverlays(packet, cutoffUtc);
    return {
        bands:
            frame !== "H4" &&
            sessions.some(
                (session) =>
                    Date.parse(session.startUtc) < cutoff &&
                    Date.parse(session.endUtc) > Date.parse(first.openUtc),
            ),
        levels: sessions.some((session) => session.levels.length > 0),
    };
}
