"""Import an original MT5 US100 H4 TSV into a versioned documentary source."""
import argparse
import csv
from datetime import datetime, timedelta, timezone
from decimal import Decimal, InvalidOperation
import hashlib
import io
import json
from pathlib import Path
import tempfile

from inspect_nasdaq_h4_coverage import stamp, utc

PROVIDER = "oanda-global-markets-mt5-demo"
SYMBOL = "US100"
H4 = timedelta(hours=4)
NATIVE_COLUMNS = ("<DATE>", "<TIME>", "<OPEN>", "<HIGH>", "<LOW>", "<CLOSE>", "<TICKVOL>")
CANONICAL_COLUMNS = ("provider_id", "symbol", "timeframe_amount", "timeframe_unit", "open_time_utc",
                     "close_time_utc", "open", "high", "low", "close", "volume")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_exact(path, expected):
    data = path.read_bytes()
    if digest(data) != expected:
        raise ValueError("Source SHA256 mismatch: " + str(path))
    return data


def import_h4(source, source_sha256, mapping, mapping_sha256, output, imported_at_utc):
    imported_at = stamp(utc(imported_at_utc))
    raw_bytes = read_exact(source, source_sha256)
    map_bytes = read_exact(mapping, mapping_sha256)
    time_map = json.loads(map_bytes)
    if (time_map.get("schemaVersion"), time_map.get("providerId"), time_map.get("symbol"),
            time_map.get("timeframe"), time_map.get("sourceSha256"), time_map.get("mappingStatus")) != (
            1, PROVIDER, SYMBOL, "H4", source_sha256, "DocumentaryAudited"):
        raise ValueError("Timestamp mapping identity or audit status is unresolved")
    if not time_map.get("evidenceReference") or not time_map.get("evidenceSha256"):
        raise ValueError("Timestamp mapping evidence reference and hash are required")
    evidence = Path(time_map["evidenceReference"])
    if not evidence.is_file() or digest(evidence.read_bytes()) != time_map["evidenceSha256"]:
        raise ValueError("Timestamp mapping evidence integrity mismatch")
    evidence_bytes = evidence.read_bytes()
    points = time_map.get("rows")
    segments = time_map.get("segments")
    if bool(points) == bool(segments):
        raise ValueError("Exactly one explicit per-row map or audited segment map is required")
    if points and not isinstance(points, list) or segments and not isinstance(segments, list):
        raise ValueError("Timestamp map rows or segments must be a list")
    coordinates = {}
    if points:
        for point in points:
            native = point["nativeOpenTime"]
            datetime.strptime(native, "%Y.%m.%d %H:%M:%S")
            if native in coordinates:
                raise ValueError("Duplicate native timestamp mapping")
            coordinates[native] = stamp(utc(point["openUtc"]))
    else:
        for segment in segments:
            beginning = datetime.strptime(segment["nativeStartInclusive"], "%Y.%m.%d %H:%M:%S")
            ending = datetime.strptime(segment["nativeEndExclusive"], "%Y.%m.%d %H:%M:%S")
            offset = segment["serverUtcOffsetMinutes"]
            if ending <= beginning or type(offset) is not int or not -840 <= offset <= 840:
                raise ValueError("Invalid audited timestamp segment")
        for earlier, later in zip(segments, segments[1:]):
            if earlier["nativeEndExclusive"] > later["nativeStartInclusive"]:
                raise ValueError("Overlapping audited timestamp segments")

    try:
        raw_text = raw_bytes.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError("Native source must be UTF-8") from error
    reader = csv.DictReader(io.StringIO(raw_text), delimiter="\t")
    if not reader.fieldnames or any(key not in reader.fieldnames for key in NATIVE_COLUMNS):
        raise ValueError("Native MT5 columns are missing")
    rows = {}
    last_native = None
    for line, raw in enumerate(reader, 2):
        if None in raw or any(value is None for value in raw.values()):
            raise ValueError(f"Malformed native source row {line}")
        native = raw["<DATE>"] + " " + raw["<TIME>"]
        datetime.strptime(native, "%Y.%m.%d %H:%M:%S")
        if last_native is not None and native < last_native:
            raise ValueError(f"Out-of-order native source row {line}")
        last_native = native
        if segments:
            matching = [s for s in segments if s["nativeStartInclusive"] <= native < s["nativeEndExclusive"]]
            if len(matching) != 1:
                raise ValueError(f"Unresolved timestamp normalization at row {line}")
            coordinates[native] = stamp((datetime.strptime(native, "%Y.%m.%d %H:%M:%S") -
                                         timedelta(minutes=matching[0]["serverUtcOffsetMinutes"])).replace(tzinfo=timezone.utc))
        if native not in coordinates:
            raise ValueError(f"Unresolved timestamp normalization at row {line}")
        try:
            values = {key: Decimal(raw["<" + key.upper() + ">"])
                      for key in ("open", "high", "low", "close")}
            volume = Decimal(raw["<TICKVOL>"])
        except (InvalidOperation, TypeError) as error:
            raise ValueError(f"Invalid native numeric value at row {line}") from error
        if (not all(value.is_finite() for value in (*values.values(), volume))
                or values["high"] < max(values["open"], values["close"], values["low"])
                or values["low"] > min(values["open"], values["close"]) or volume < 0):
            raise ValueError(f"Invalid OHLC or tick volume at row {line}")
        opening = utc(coordinates[native])
        item = {"nativeOpenTime": native, "openUtc": stamp(opening), "nominalCloseUtc": stamp(opening + H4),
                **{key: str(value) for key, value in values.items()}, "volume": str(volume)}
        previous = rows.get(item["openUtc"])
        if previous is not None and previous != item:
            raise ValueError(f"Conflicting duplicate candle at row {line}")
        rows[item["openUtc"]] = item
    if not rows or set(coordinates) != {r["nativeOpenTime"] for r in rows.values()}:
        raise ValueError("Timestamp map and native rows must match exactly")
    candles = sorted(rows.values(), key=lambda row: row["openUtc"])
    for prior, current in zip(candles, candles[1:]):
        if current["openUtc"] < prior["nominalCloseUtc"]:
            raise ValueError("Overlapping normalized H4 candles")
    normalized_buffer = io.StringIO(newline="")
    writer = csv.DictWriter(normalized_buffer, CANONICAL_COLUMNS, lineterminator="\n")
    writer.writeheader()
    for row in candles:
        writer.writerow(dict(provider_id=PROVIDER, symbol=SYMBOL, timeframe_amount=4, timeframe_unit="Hour",
                             open_time_utc=row["openUtc"], close_time_utc=row["nominalCloseUtc"],
                             **{key: row[key] for key in ("open", "high", "low", "close", "volume")}))
    normalized_bytes = normalized_buffer.getvalue().encode("utf-8")
    missing = [{"startUtc": a["nominalCloseUtc"], "endUtc": b["openUtc"],
                "classification": "NoSourceRows_NotATradingScheduleConclusion"}
               for a, b in zip(candles, candles[1:]) if a["nominalCloseUtc"] < b["openUtc"]]
    manifest = {"schemaVersion": 1, "sourceKind": "OriginalMt5H4Documentary", "providerId": PROVIDER,
                "symbol": SYMBOL, "originalTimeframe": "H4", "derived": False,
                "importedAtUtc": imported_at, "documentaryHistoricalAvailability": "Unauthenticated",
                "authenticatedHistoricalAvailability": False, "sourceSha256": source_sha256,
                "mappingSha256": mapping_sha256, "normalizedSha256": digest(normalized_bytes),
                "sourceFile": "original.tsv", "mappingFile": "timestamp-map.json", "normalizedFile": "normalized.csv",
                "mappingEvidenceFile": "mapping-evidence", "mappingEvidenceSha256": digest(evidence_bytes),
                "closedCandleCount": len(candles), "firstOpenUtc": candles[0]["openUtc"],
                "lastNominalCloseUtc": candles[-1]["nominalCloseUtc"], "missingSourceRanges": missing,
                "canonicalAdmissionPermitted": False, "operationalSignal": False}
    if output.exists():
        existing = json.loads((output / "manifest.json").read_bytes())
        comparable = ("schemaVersion", "sourceKind", "sourceSha256", "mappingSha256", "normalizedSha256",
                      "mappingEvidenceSha256", "providerId", "symbol", "originalTimeframe", "derived",
                      "documentaryHistoricalAvailability", "authenticatedHistoricalAvailability",
                      "canonicalAdmissionPermitted", "operationalSignal", "closedCandleCount",
                      "firstOpenUtc", "lastNominalCloseUtc", "missingSourceRanges")
        if any(existing.get(key) != manifest[key] for key in comparable) or any(
                digest((output / name).read_bytes()) != expected for name, expected in (
                    ("original.tsv", source_sha256), ("timestamp-map.json", mapping_sha256),
                    ("normalized.csv", manifest["normalizedSha256"]),
                    ("mapping-evidence", manifest["mappingEvidenceSha256"]))):
            raise ValueError("Existing import conflicts; choose a new versioned directory")
        return existing
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="moneyway-h4-import-", dir=output.parent) as staging:
        root = Path(staging) / "source"
        root.mkdir()
        (root / "original.tsv").write_bytes(raw_bytes)
        (root / "timestamp-map.json").write_bytes(map_bytes)
        (root / "normalized.csv").write_bytes(normalized_bytes)
        (root / "mapping-evidence").write_bytes(evidence_bytes)
        (root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
        root.rename(output)
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "timestamp-map", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--source-sha256", required=True)
    parser.add_argument("--mapping-sha256", required=True)
    parser.add_argument("--imported-at-utc", required=True)
    args = parser.parse_args()
    try:
        report = import_h4(args.source, args.source_sha256, args.timestamp_map,
                           args.mapping_sha256, args.output, args.imported_at_utc)
    except (ValueError, FileNotFoundError, KeyError, csv.Error) as error:
        parser.error(str(error))
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
