"""Freeze earliest eligible unseen documentary session; no detector/label/outcome inputs.
Read-only registered sources. Reserved dates are inspected for neither OHLC nor hypotheses.
"""
import argparse
import csv
import datetime as dt
import hashlib
import json
from pathlib import Path
from build_nasdaq_context import FRAMES, number

EXCLUDED = {"2026-06-23": "PreviouslyExposed", "2026-06-25": "Validation",
            "2026-06-29": "PreviouslyExposed", "2026-06-30": "HeldOut"}
POLICY = "earliest-unused-complete-asia-london-h1-and-preparation-observation-m5-with-preceding-h4-h1-v1"


def stamp(value):
    return value.isoformat().replace("+00:00", "Z")


def coverage(rows, start, end, minutes):
    expected = set()
    while start + dt.timedelta(minutes=minutes) <= end:
        expected.add(stamp(start))
        start += dt.timedelta(minutes=minutes)
    actual = {r["open_time_utc"] for r in rows if dt.datetime.fromisoformat(r["close_time_utc"]) - dt.datetime.fromisoformat(r["open_time_utc"]) == dt.timedelta(minutes=minutes)}
    return sorted(expected - actual)


def build(repo, session, target):
    package = json.loads((repo / "docs/data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json").read_bytes())
    artifacts = {a["id"]: a for a in package["artifacts"]}
    rows, paths = {}, {}
    for key, tf, _, _, _ in FRAMES:
        for kind in ["raw", "normalized"]:
            a = artifacts[kind + "-" + tf]
            path = (session / a["relativePath"]).resolve()
            if not path.is_relative_to(session.resolve()) or hashlib.sha256(path.read_bytes()).hexdigest() != a["sha256"]:
                raise ValueError("Registered source integrity mismatch: " + key)
            paths[key, kind] = path
        rows[key] = list(csv.DictReader(paths[key, "normalized"].open()))
    dates = sorted({r["open_time_utc"][:10] for r in rows["H1"] if r["open_time_utc"][:10] not in EXCLUDED})
    inventory = []
    for day in dates:
        prep = dt.datetime.fromisoformat(day + "T13:00:00+00:00")
        cutoff = prep + dt.timedelta(hours=3, minutes=30)
        missing_h1 = coverage(rows["H1"], prep - dt.timedelta(hours=15), prep, 60)
        missing_m5 = coverage(rows["M5"], prep, cutoff, 5)
        prior = {f: sum(r["close_time_utc"] <= stamp(prep) for r in rows[f]) for f in ["H4", "H1"]}
        eligible = not missing_h1 and not missing_m5 and all(prior.values())
        inventory.append(dict(day=day, eligible=eligible, missingH1=missing_h1, missingM5=missing_m5, precedingClosedCounts=prior))
    chosen = next((r for r in inventory if r["eligible"]), None)
    if chosen is None:
        raise ValueError("No eligible unused session; do not invent independent evidence")
    day = chosen["day"]
    prep = dt.datetime.fromisoformat(day + "T13:00:00+00:00")
    start, end = stamp(prep - dt.timedelta(hours=40)), stamp(prep + dt.timedelta(hours=3, minutes=30))
    series = {}
    for key, tf, amount, unit, minutes in FRAMES:
        native = {r["<DATE>"] + " " + r["<TIME>"]: r for r in csv.DictReader(paths[key, "raw"].open(), delimiter="\t")}
        selected = [r for r in rows[key] if r["open_time_utc"] >= start and r["close_time_utc"] <= end]
        candles = []
        for r in selected:
            opening, closing = (dt.datetime.fromisoformat(r[k]) for k in ["open_time_utc", "close_time_utc"])
            identity = (opening + dt.timedelta(hours=3)).strftime("%Y.%m.%d %H:%M:%S")
            from decimal import Decimal
            raw = native[identity]
            if r["provider_id"] != package["providerId"] or r["symbol"] != "US100" or (r["timeframe_amount"], r["timeframe_unit"]) != (str(amount), unit) or closing - opening != dt.timedelta(minutes=minutes):
                raise ValueError("Original source timeframe/identity mismatch")
            if any(Decimal(r[k]) != Decimal(raw["<" + k.upper() + ">"]) for k in ["open", "high", "low", "close"]) or Decimal(r["volume"]) != Decimal(raw["<TICKVOL>"]):
                raise ValueError("Original native/normalized value mismatch")
            candles.append(dict(providerId=dict(value=package["providerId"]), symbol=dict(value="US100"), timeframe=dict(amount=amount, unit=unit),
                                openTimeUtc=r["open_time_utc"], closeTimeUtc=r["close_time_utc"], nativeOpenTime=identity,
                                **{k: number(r[k]) for k in ["open", "high", "low", "close", "volume"]}))
        series[key] = dict(sourceTimeframe=tf, derived=False, intervalMinutes=minutes, count=len(candles), firstOpenUtc=candles[0]["openTimeUtc"],
                           lastClosedUtc=candles[-1]["closeTimeUtc"], sourceGaps=[],
                           **{kind + suffix: artifacts[kind + "-" + tf][field] for kind in ["raw", "normalized"] for suffix, field in [("RelativePath", "relativePath"), ("Sha256", "sha256")]}, candles=candles)
        previous = start
        for c in candles:
            if c["openTimeUtc"] > previous:
                series[key]["sourceGaps"].append(dict(startUtc=previous, endUtc=c["openTimeUtc"], classification="NoSourceRows_NotATradingScheduleConclusion"))
            previous = c["closeTimeUtc"]
    context = dict(schemaVersion=1, id="nasdaq-context-" + day.replace("-", "") + "-review-v1", tradingDay=day,
                   providerId=package["providerId"], symbol="US100", timeZone="America/Bogota", classification="Unreviewed_SelfDeclarationRequired_DocumentaryResearch",
                   requestedStartUtc=start, maximumCutoffUtc=end, documentaryMapping=package["timeMapping"],
                   sessionBoundariesStatus="ExplicitCandidateWindows_ADR0028", windowInstruction="08:30–11:30 America/Bogota", windowRuntime="[08:30,11:00) America/Bogota",
                   canonicalAdmissionPermitted=False, operationalSignal=False, series=series)
    payload = (json.dumps(context, indent=2) + "\n").encode()
    manifest = dict(schemaVersion=1, datasetId="contextual-sc-review-v1", datasetVersion=1, selectionPolicy=POLICY, excluded=EXCLUDED,
                    inventory=inventory, selectedDay=day, inputSha256=hashlib.sha256(payload).hexdigest(), contextId=context["id"],
                    exposure="Unreviewed_SelfDeclarationRequired", canonicalAdmissionPermitted=False, operationalSignal=False)
    if target.exists():
        raise FileExistsError("New output directory required")
    target.mkdir(parents=True)
    (target / "context.json").write_bytes(payload)
    (target / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"selected": day, "inventory": inventory, "counts": {k: v["count"] for k, v in series.items()}}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--session-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    build(Path(__file__).resolve().parents[2], args.session_root.resolve(), args.output.resolve())
