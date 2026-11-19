"""Fixed documentary curation, not a detector. Calls the existing C# Worker.

Requires the registered June raw/normalized files. Publishes a NEW directory.
Held-out inputs are retained separately and are NEVER evaluated or bundled.
"""
import argparse
import csv
import datetime as dt
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

DATASET = "m5-june-documentary-blind-v1"
RAW_SHA = "4f468275b5644d4298ba0c085802f04574c765f7dfa662df1ee99456eb9fcf46"
NORMAL_SHA = "0133f473592a004535bac89437c96b1c369a753205792459c4e4e12c1835c21a"
PLAN = [("2026-06-23", "Development"), ("2026-06-25", "Validation"), ("2026-06-30", "HeldOut")]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=True) + "\n", encoding="utf-8", newline="\n")


def exact_number(text):
    value = float(text)
    if Decimal(str(value)) != Decimal(text):
        raise ValueError("Source price not exactly representable in this documentary JSON projection")
    return value


def overlaps(entries):
    for i, a in enumerate(entries):
        for b in entries[i + 1:]:
            if a["providerId"] == b["providerId"] and a["symbol"] == b["symbol"] and max(a["startUtc"], b["startUtc"]) < min(a["cutoffUtc"], b["cutoffUtc"]):
                raise ValueError("Overlapping source observations: " + a["id"] + "/" + b["id"])


def build(repo, session, output):
    raw = session / "raw/2026-06-29/US100_M5_202606220100_202606302355.csv"
    normalized = session / "normalized/2026-06-29/5M.csv"
    if sha(raw.read_bytes()) != RAW_SHA or sha(normalized.read_bytes()) != NORMAL_SHA:
        raise ValueError("Registered source hash mismatch; refusing alternate data")
    native = {r["<DATE>"] + " " + r["<TIME>"]: r for r in csv.DictReader(raw.open(), delimiter="\t")}
    rows = list(csv.DictReader(normalized.open()))
    worker = repo / "src/backend/MoneyWay.Worker/bin/Debug/net10.0/MoneyWay.Worker.dll"
    entries, charts, authentic_identities = [], {}, set()

    def register(identifier, partition, request, native_opens, exposed=False, original=None):
        held = partition == "HeldOut"
        if request["dataOrigin"] == "RetrospectiveDocumentary":
            authentic_identities.update((request["providerId"], request["symbol"], c["openTimeUtc"]) for c in request["candles"])
        relative = ("held-out/" if held else "inputs/") + identifier + ".json"
        input_file = output / relative
        if original:
            input_file.parent.mkdir(parents=True, exist_ok=True)
            input_file.write_bytes(original.read_bytes())
        else:
            write(input_file, request)
        anchor = request["candles"][0]
        entry = dict(id=identifier, datasetId=DATASET, datasetVersion=1, partition=partition,
                     seriesId=request["seriesId"], providerId=request["providerId"], symbol=request["symbol"], timeframe="5m",
                     startUtc=anchor["openTimeUtc"], cutoffUtc=request["cutoffUtc"], direction=request["direction"],
                     anchorOpenUtc=request["initialization"]["anchorOpenTimeUtc"],
                     anchorPrice=anchor["high"] if request["direction"] == "Ascending" else anchor["low"],
                     candleCount=len(request["candles"]), dataOrigin=request["dataOrigin"],
                     priorExposure="PreviouslyExposed" if exposed else "NotDeclaredExposed",
                     labelAvailability="Unlabelled", inputFile=relative, inputSha256=sha(input_file.read_bytes()),
                     reportSha256=None, nativeOpenIdentities=native_opens,
                     originalClockDomain="Synthetic" if partition == "SoftwareAcceptance" else "MT5 native server; documentary UTC=server-3h",
                     canonicalAdmissionPermitted=False, operationalSignal=False)
        if not held:
            report_dir = output / "generated" / identifier
            subprocess.run(["dotnet", str(worker), "m5-swing-lab", "--input", str(input_file.resolve()), "--output", str(report_dir.resolve())], check=True, capture_output=True)
            report_bytes = (report_dir / "comparison.json").read_bytes()
            envelope = json.loads(report_bytes)
            report = envelope["comparison"]
            if envelope["inputSha256"] != entry["inputSha256"]:
                raise ValueError("Worker input identity mismatch")
            entry["reportSha256"] = sha(report_bytes)
            target = output / "review/reports" / (identifier + ".json")
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(report_bytes)
            charts[identifier] = dict(input=report["input"], evidenceCandles=report["evidenceCandles"])
        entries.append(entry)

    for day, partition in PLAN:
        for hour, direction in [(13, "Ascending"), (14, "Descending")]:
            start = f"{day}T{hour}:00:00Z"
            end = f"{day}T{hour + 1}:00:00Z"
            selected = [r for r in rows if start <= r["open_time_utc"] < end]
            if len(selected) != 12:
                raise ValueError("Fixed documentary window is incomplete; do not replace it opportunistically")
            candles, native_opens = [], []
            previous = None
            for row in selected:
                opening = dt.datetime.fromisoformat(row["open_time_utc"])
                closing = dt.datetime.fromisoformat(row["close_time_utc"])
                if (previous and opening != previous) or closing - opening != dt.timedelta(minutes=5):
                    raise ValueError("Non-contiguous M5 source")
                previous = closing
                if (row["provider_id"], row["symbol"], row["timeframe_amount"], row["timeframe_unit"]) != ("oanda-global-markets-mt5-demo", "US100", "5", "Minute"):
                    raise ValueError("Source identity mismatch")
                native_open = (opening + dt.timedelta(hours=3)).strftime("%Y.%m.%d %H:%M:%S")
                original = native[native_open]
                for k in ["open", "high", "low", "close"]:
                    if Decimal(row[k]) != Decimal(original["<" + k.upper() + ">"]):
                        raise ValueError("Native OHLC mismatch")
                if Decimal(row["volume"]) != Decimal(original["<TICKVOL>"]):
                    raise ValueError("Native tick volume mismatch")
                native_opens.append(native_open)
                candles.append(dict(openTimeUtc=row["open_time_utc"], closeTimeUtc=row["close_time_utc"],
                                    **{k: exact_number(row[k]) for k in ["open", "high", "low", "close", "volume"]}))
            identifier = "m5-" + day.replace("-", "") + f"-{hour}00-" + direction.lower() + "-v1"
            provenance = "Fixed calendar-window research sample; native/normalized June export hashes independently matched. UTC mapping is documentary, not authenticated availability. First-candle anchor and alternating Ascending/Descending are research convenience assumptions, not mentor structural labels. No outcome-driven selection."
            request = dict(seriesId=identifier, providerId="oanda-global-markets-mt5-demo", symbol="US100", timeframe="5m", direction=direction,
                           cutoffUtc=end, dataOrigin="RetrospectiveDocumentary", provenance=provenance,
                           initialization=dict(referenceId="explicit-calendar-" + identifier, anchorOpenTimeUtc=start,
                                               availableAtUtc=candles[0]["closeTimeUtc"], sourceReference="Research-only first-candle anchor; nominal close availability is assumed for calculation, not authenticated historical availability."), candles=candles)
            register(identifier, partition, request, native_opens)
    for identifier, partition, fixture in [
        ("previously-exposed-june29", "PreviouslyExposed", "docs/data/strategy-lab/m5-swing-v2/june29-documentary.json"),
        ("software-synthetic", "SoftwareAcceptance", "docs/data/strategy-lab/m5-swing-v1/synthetic-disagreement.json"),
    ]:
        original = repo / fixture
        request = json.loads(original.read_bytes())
        identities = [] if partition == "SoftwareAcceptance" else [(dt.datetime.fromisoformat(c["openTimeUtc"]) + dt.timedelta(hours=3)).strftime("%Y.%m.%d %H:%M:%S") for c in request["candles"]]
        register(identifier, partition, request, identities, exposed=True, original=original)
    overlaps([e for e in entries if e["dataOrigin"] == "RetrospectiveDocumentary"])
    manifest = dict(schemaVersion=1, datasetId=DATASET, datasetVersion=1, selectionPolicy="Fixed dates 2026-06-23/25/30; 13:00-14:00 ascending, 14:00-15:00 descending nominal UTC. First candle anchor. No algorithm/outcome filtering.",
                    sourceRawSha256=RAW_SHA, sourceNormalizedSha256=NORMAL_SHA,
                    chronologicalSeparation="NonOverlappingSourceDays", splitAssessment="InsufficientForIndependentGeneralizationClaims",
                    heldOutPolicy="Inputs only, not evaluated, not in frontend. Trusted repository owner can inspect files; release requires a separate frozen evaluation decision.",
                    canonicalAdmissionPermitted=False, operationalSignal=False, episodes=entries)
    write(output / "dataset.json", manifest)
    public = dict(manifest)
    public["episodes"] = [e for e in entries if e["partition"] != "HeldOut"]
    public["datasetContentSha256"] = sha((output / "dataset.json").read_bytes())
    public["heldOutEpisodeCount"] = sum(e["partition"] == "HeldOut" for e in entries)
    public["uniqueAuthenticSourceCandleCount"] = len(authentic_identities)
    write(output / "review/manifest.json", public)
    write(output / "review/candles.json", charts)
    generated = output / "generated"
    if generated.resolve().parent != output.resolve():
        raise ValueError("Temporary report cleanup escaped its owned output directory")
    shutil.rmtree(generated)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--session-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    target = args.output.resolve()
    if target.exists():
        parser.error("Output must be a new directory")
    repo = Path(__file__).resolve().parents[2]
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="moneyway-blind-build-", dir=target.parent) as work:
        staging = Path(work) / "dataset"
        staging.mkdir()
        build(repo, args.session_root.resolve(), staging)
        if target.exists():
            raise FileExistsError(target)
        staging.rename(target)
    print("Published fixed documentary dataset; held-out detector results were not generated: " + str(target))
