"""Documentary June-23 context and redacted projection of the actual V3 export.
No detectors are run. Registered original timeframe files are read-only.
"""
import argparse
import csv
import datetime as dt
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import tempfile

EXPORT_SHA = "8368a3b6ed259f19e757edac632341674814185c7adab787697258683ebc4861"
START = "2026-06-21T21:00:00Z"
END = "2026-06-23T16:30:00Z"
IDS = ["m5-20260623-1300-ascending-v1", "m5-20260623-1400-descending-v1"]
FRAMES = [("H4", "4H", 4, "Hour", 240), ("H1", "1H", 1, "Hour", 60), ("M5", "5M", 5, "Minute", 5), ("M1", "1M", 1, "Minute", 1)]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def number(text):
    value = float(text)
    if Decimal(str(value)) != Decimal(text):
        raise ValueError("Source decimal changed in JSON projection")
    return value


def build(repo, session, export, target):
    original = export.read_bytes()
    if sha(original) != EXPORT_SHA:
        raise ValueError("The actual supplied export does not match its supplied SHA256")
    evidence = json.loads(original)
    if evidence["schemaVersion"] != "research-label-export-1":
        raise ValueError("Unsupported actual export")
    catalog_path = repo / "src/frontend/moneyway-web/src/research/blind/data/manifest.json"
    catalog = json.loads(catalog_path.read_bytes())
    state = evidence["labels"]
    if (state["datasetId"], state["datasetVersion"], state["datasetContentSha256"]) != (catalog["datasetId"], catalog["datasetVersion"], catalog["datasetContentSha256"]):
        raise ValueError("Export dataset identity mismatch")
    if set(state["records"]) != set(IDS):
        raise ValueError("Only the two actual Development labels are authorized here")
    if evidence["manifest"] != catalog:
        raise ValueError("Export manifest differs from the registered dataset")
    reviews = []
    for identifier in IDS:
        entry = next(e for e in catalog["episodes"] if e["id"] == identifier)
        history = state["records"][identifier]
        report_file = repo / "src/frontend/moneyway-web/src/research/blind/data/reports" / (identifier + ".json")
        report_bytes = report_file.read_bytes()
        report = json.loads(report_bytes)
        if history["inputSha256"] != entry["inputSha256"] or history["revealedReportSha256"] != sha(report_bytes) or entry["reportSha256"] != sha(report_bytes):
            raise ValueError("Original label/report identity mismatch")
        if history["exposure"] != "SelfDeclaredIndependent" or len(history["revisions"]) != 1 or history["revealRevision"] != 1:
            raise ValueError("Actual original revision/exposure mismatch")
        label = history["revisions"][0]
        if history["episodeId"] != identifier or label["canonicalAdmissionPermitted"] is not False or label["operationalSignal"] is not False:
            raise ValueError("Original research identity/scope mismatch")
        if label["classification"] != "IndependentRetrospectiveResearch" or label["revision"] != 1 or label["kind"] != "Reference" or label["confirmationOpenUtc"] is not None or label["confirmationCloseUtc"] is not None:
            raise ValueError("Actual original human label contract mismatch")
        lookup = {c["openTimeUtc"]: c for c in report["comparison"]["evidenceCandles"]}
        coordinate = "low" if label["extreme"] == "Low" else "high"
        if not label["selectedOpens"] or any(Decimal(str(lookup[t][coordinate])) != Decimal(str(label["price"])) for t in label["selectedOpens"]):
            raise ValueError("Original human price not present in original source candles")
        if label["cutoffUtc"] != entry["cutoffUtc"]:
            raise ValueError("Original human cutoff changed")
        # Explicit privacy projection: original bytes remain external and immutable.
        projection = {k: label[k] for k in ["revision", "kind", "selectedOpens", "extreme", "price", "confirmationOpenUtc", "confirmationCloseUtc", "cutoffUtc", "classification", "canonicalAdmissionPermitted", "operationalSignal"]}
        reviews.append(dict(episodeId=identifier, datasetId=state["datasetId"], datasetVersion=state["datasetVersion"],
                            inputSha256=history["inputSha256"], reportSha256=history["revealedReportSha256"],
                            originalExposure=history["exposure"], originalRevealRevision=history["revealRevision"],
                            reviewerAlias="human-reviewer-1", label=projection))
    package = json.loads((repo / "docs/data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json").read_bytes())
    artifacts = {a["id"]: a for a in package["artifacts"]}
    series = {}
    for key, source_tf, amount, unit, minutes in FRAMES:
        paths = {}
        for kind in ["raw", "normalized"]:
            artifact = artifacts[kind + "-" + source_tf]
            path = (session / artifact["relativePath"]).resolve()
            if not path.is_relative_to(session.resolve()) or sha(path.read_bytes()) != artifact["sha256"]:
                raise ValueError("Registered source path/hash mismatch: " + key)
            paths[kind] = path
        raw = {r["<DATE>"] + " " + r["<TIME>"]: r for r in csv.DictReader(paths["raw"].open(), delimiter="\t")}
        rows = list(csv.DictReader(paths["normalized"].open()))
        selected = [r for r in rows if r["open_time_utc"] >= START and r["close_time_utc"] <= END]
        if not selected:
            raise ValueError("No suitable original timeframe context: " + key)
        candles, gaps = [], []
        previous = START
        for r in selected:
            opening = dt.datetime.fromisoformat(r["open_time_utc"])
            closing = dt.datetime.fromisoformat(r["close_time_utc"])
            if closing - opening != dt.timedelta(minutes=minutes) or r["provider_id"] != package["providerId"] or r["symbol"] != "US100" or (r["timeframe_amount"], r["timeframe_unit"]) != (str(amount), unit):
                raise ValueError("Original timeframe/interval/source mismatch")
            if r["open_time_utc"] < previous:
                raise ValueError("Overlapping source context")
            if r["open_time_utc"] > previous:
                gaps.append(dict(startUtc=previous, endUtc=r["open_time_utc"], classification="NoSourceRows_NotATradingScheduleConclusion"))
            native_open = (opening + dt.timedelta(hours=3)).strftime("%Y.%m.%d %H:%M:%S")
            original_row = raw[native_open]
            if any(Decimal(r[k]) != Decimal(original_row["<" + k.upper() + ">"]) for k in ["open", "high", "low", "close"]) or Decimal(r["volume"]) != Decimal(original_row["<TICKVOL>"]):
                raise ValueError("Raw/normalized OHLC or tick-volume mismatch")
            candles.append(dict(providerId=dict(value=package["providerId"]), symbol=dict(value="US100"), timeframe=dict(amount=amount, unit=unit),
                                openTimeUtc=r["open_time_utc"], closeTimeUtc=r["close_time_utc"],
                                **{k: number(r[k]) for k in ["open", "high", "low", "close", "volume"]}, nativeOpenTime=native_open))
            previous = r["close_time_utc"]
        series[key] = dict(sourceTimeframe=source_tf, derived=False, intervalMinutes=minutes, count=len(candles),
                           firstOpenUtc=candles[0]["openTimeUtc"], lastClosedUtc=candles[-1]["closeTimeUtc"], sourceGaps=gaps,
                           rawRelativePath=artifacts["raw-" + source_tf]["relativePath"], rawSha256=artifacts["raw-" + source_tf]["sha256"],
                           normalizedRelativePath=artifacts["normalized-" + source_tf]["relativePath"], normalizedSha256=artifacts["normalized-" + source_tf]["sha256"], candles=candles)
    result = dict(schemaVersion=1, id="nasdaq-context-20260623-v1", tradingDay="2026-06-23", providerId=package["providerId"], symbol="US100",
                  timeZone="America/Bogota", classification="PreviouslyExposed_RetrospectiveDocumentaryResearch", requestedStartUtc=START, maximumCutoffUtc=END,
                  documentaryMapping="June pack only: nominal UTC = MT5 server minus 3h; close = open + native timeframe. Not authenticated publication or availability.",
                  sessionBoundariesStatus="UnresolvedForContextualResearch_NoAsiaLondonExtrema", windowInstruction="08:30–11:30 America/Bogota", windowRuntime="[08:30,11:00) America/Bogota",
                  canonicalAdmissionPermitted=False, operationalSignal=False, series=series)
    review = dict(schemaVersion=1, sourceFileName=export.name, sourceSha256=EXPORT_SHA, evidenceClass="UnauthenticatedBrowserLocalHumanResearchExport",
                  privacy="Reviewer name, device/reveal timestamps and unrelated manifest details omitted from this public projection; original external bytes remain unchanged.",
                  canonicalAdmissionPermitted=False, operationalSignal=False, reviews=reviews)
    for name, value in [("june23-context.json", result), ("development-review.json", review)]:
        (target / name).write_text(json.dumps(value, indent=2, ensure_ascii=True) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--session-root", type=Path, required=True)
    parser.add_argument("--review-export", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    target = args.output.resolve()
    if target.exists():
        parser.error("Output must be a new directory; original evidence is never replaced")
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="moneyway-context-build-", dir=target.parent) as work:
        staging = Path(work) / "context"
        staging.mkdir()
        build(Path(__file__).resolve().parents[2], args.session_root.resolve(), args.review_export.resolve(), staging)
        if target.exists():
            raise FileExistsError(target)
        staging.rename(target)
    print("Published original-timeframe documentary context and privacy-projected actual labels: " + str(target))
