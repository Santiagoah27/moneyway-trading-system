"""Read-only, documentary coverage of the registered original Nasdaq H4 source."""
import argparse
import csv
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import hashlib
import json
from pathlib import Path

PACKAGE = "docs/data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json"
H4 = timedelta(hours=4)


def utc(value):
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        raise ValueError("An explicit UTC timestamp is required")
    return parsed


def stamp(value):
    return value.isoformat().replace("+00:00", "Z")


def inspect(repo, session, cutoff, history_hours, import_manifest=None):
    if history_hours <= 0:
        raise ValueError("history-hours must be positive")
    cutoff = utc(cutoff)
    start = cutoff - timedelta(hours=history_hours)
    package = json.loads((repo / PACKAGE).read_bytes())
    if import_manifest is None:
        artifacts = {item["id"]: item for item in package["artifacts"]}
        paths = {}
        for kind in ("raw", "normalized"):
            artifact = artifacts[kind + "-4H"]
            path = (session / artifact["relativePath"]).resolve()
            if not path.is_relative_to(session.resolve()):
                raise ValueError("Source path escapes the session root")
            if hashlib.sha256(path.read_bytes()).hexdigest() != artifact["sha256"]:
                raise ValueError("Registered H4 source integrity mismatch")
            paths[kind] = path
        source_identity = {kind: {"relativePath": artifacts[kind + "-4H"]["relativePath"],
                                  "sha256": artifacts[kind + "-4H"]["sha256"]} for kind in ("raw", "normalized")}
        mapping_rows = None
        mapping_note = package["timeMapping"]
        availability_note = package["availabilityEvidence"]
    else:
        manifest_path = Path(import_manifest).resolve()
        manifest = json.loads(manifest_path.read_bytes())
        if (manifest.get("schemaVersion"), manifest.get("sourceKind"), manifest.get("providerId"),
                manifest.get("symbol"), manifest.get("originalTimeframe"), manifest.get("derived")) != (
                1, "OriginalMt5H4Documentary", package["providerId"], package["originalSymbol"], "H4", False):
            raise ValueError("Unsupported H4 import manifest identity")
        paths = {kind: manifest_path.parent / manifest[key] for kind, key in (
            ("raw", "sourceFile"), ("normalized", "normalizedFile"), ("mapping", "mappingFile"),
            ("evidence", "mappingEvidenceFile"))}
        expected = {"raw": manifest["sourceSha256"], "normalized": manifest["normalizedSha256"],
                    "mapping": manifest["mappingSha256"], "evidence": manifest["mappingEvidenceSha256"]}
        for kind, path in paths.items():
            if path.resolve().parent != manifest_path.parent or hashlib.sha256(path.read_bytes()).hexdigest() != expected[kind]:
                raise ValueError("Imported H4 source integrity mismatch")
        mapping = json.loads(paths["mapping"].read_bytes())
        if mapping.get("sourceSha256") != expected["raw"] or mapping.get("mappingStatus") != "DocumentaryAudited":
            raise ValueError("Imported H4 timestamp mapping mismatch")
        if mapping.get("rows"):
            mapping_rows = {r["openUtc"]: r["nativeOpenTime"] for r in mapping["rows"]}
        else:
            mapping_rows = {}
            with paths["raw"].open(newline="") as source:
                for row in csv.DictReader(source, delimiter="\t"):
                    native_open = row["<DATE>"] + " " + row["<TIME>"]
                    matches = [part for part in mapping["segments"] if
                               part["nativeStartInclusive"] <= native_open < part["nativeEndExclusive"]]
                    if len(matches) != 1:
                        raise ValueError("Imported H4 timestamp mapping is incomplete")
                    opening = (datetime.strptime(native_open, "%Y.%m.%d %H:%M:%S") -
                               timedelta(minutes=matches[0]["serverUtcOffsetMinutes"])).replace(tzinfo=timezone.utc)
                    mapping_rows[stamp(opening)] = native_open
        source_identity = {kind: {"relativePath": str(paths[kind]), "sha256": expected[kind]}
                           for kind in ("raw", "normalized", "mapping")}
        mapping_note = "Explicit documentary mapping from imported evidence; historical publication unauthenticated"
        availability_note = manifest["documentaryHistoricalAvailability"]

    native = {}
    with paths["raw"].open(newline="") as source:
        for row in csv.DictReader(source, delimiter="\t"):
            key = row["<DATE>"] + " " + row["<TIME>"]
            if key in native:
                raise ValueError("Duplicate native H4 timestamp")
            native[key] = row

    selected = []
    previous_open = None
    with paths["normalized"].open(newline="") as source:
        for row in csv.DictReader(source):
            opening, closing = utc(row["open_time_utc"]), utc(row["close_time_utc"])
            if closing - opening != H4 or (previous_open is not None and opening <= previous_open):
                raise ValueError("Invalid H4 order or duration")
            previous_open = opening
            if (row["provider_id"], row["symbol"], row["timeframe_amount"], row["timeframe_unit"]) != (package["providerId"], package["originalSymbol"], "4", "Hour"):
                raise ValueError("Original provider, instrument or timeframe mismatch")
            if opening < start or closing > cutoff:
                continue
            native_open = ((opening + timedelta(hours=3)).strftime("%Y.%m.%d %H:%M:%S")
                           if mapping_rows is None else mapping_rows.get(stamp(opening)))
            original = native.get(native_open)
            if original is None or any(Decimal(row[field]) != Decimal(original["<" + field.upper() + ">"])
                                       for field in ("open", "high", "low", "close")) or Decimal(row["volume"]) != Decimal(original["<TICKVOL>"]):
                raise ValueError("Native and normalized H4 evidence disagree")
            selected.append({"openUtc": stamp(opening), "nominalCloseUtc": stamp(closing), "nativeOpenTime": native_open})

    gaps = []
    cursor = start
    for candle in selected:
        opening, closing = utc(candle["openUtc"]), utc(candle["nominalCloseUtc"])
        if opening < cursor:
            raise ValueError("Overlapping H4 source rows")
        if opening > cursor:
            gaps.append({"startUtc": stamp(cursor), "endUtc": stamp(opening),
                         "classification": "NoSourceRows_NotATradingScheduleConclusion"})
        cursor = closing
    if cursor < cutoff:
        gaps.append({"startUtc": stamp(cursor), "endUtc": stamp(cutoff),
                     "classification": "NoClosedSourceRows_NotATradingScheduleConclusion"})

    return {"schemaVersion": 1, "providerId": package["providerId"], "instrument": package["originalSymbol"],
            "originalTimeframe": "H4", "derived": False, "requestedStartUtc": stamp(start),
            "requestedEndUtc": stamp(cutoff), "cutoffUtc": stamp(cutoff), "historyHours": history_hours,
            "availableStartUtc": selected[0]["openUtc"] if selected else None,
            "availableEndUtc": selected[-1]["nominalCloseUtc"] if selected else None,
            "closedCandleCount": len(selected), "firstCandle": selected[0] if selected else None,
            "lastCandle": selected[-1] if selected else None, "candles": selected,
            "missingSourceRanges": gaps, "requestedCoverageAvailable": bool(selected) and not gaps,
            "source": source_identity,
            "sourceIntegrity": ("RegisteredSha256AndSelectedNativeRowValuesVerified" if import_manifest is None
                                else "ImportManifestSha256AndSelectedNativeRowValuesVerified"),
            "documentaryAvailability": availability_note,
            "timeMapping": mapping_note, "strategyTimeZone": "America/Bogota",
            "canonicalAdmissionPermitted": False, "operationalSignal": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session-root", type=Path, required=True)
    parser.add_argument("--cutoff-utc", required=True)
    parser.add_argument("--history-hours", type=int, required=True)
    parser.add_argument("--import-manifest", type=Path)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    print(json.dumps(inspect(repo, args.session_root.resolve(), args.cutoff_utc,
                             args.history_hours, args.import_manifest), indent=2))


if __name__ == "__main__":
    main()
