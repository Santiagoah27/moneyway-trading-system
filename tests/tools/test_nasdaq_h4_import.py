import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("h4_import", ROOT / "tools/research/import_nasdaq_h4.py")
module = importlib.util.module_from_spec(spec)
import sys
sys.path.insert(0, str(ROOT / "tools/research"))
spec.loader.exec_module(module)
from inspect_nasdaq_h4_coverage import inspect


class NasdaqH4ImportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / "US100_H4.tsv"
        self.mapping = self.root / "mapping.json"
        self.output = self.root / "import-v1"
        self.evidence = ROOT / "docs/data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json"
        self.rows = [
            "2026.06.22\t00:00:00\t30335.0\t30377.9\t30092.1\t30371.5\t84046\t0\t18",
            "2026.06.22\t04:00:00\t30371.6\t30413.0\t30278.9\t30337.5\t96604\t0\t18",
        ]
        self.write_source()
        self.write_mapping()

    def write_source(self):
        self.source.write_text("<DATE>\t<TIME>\t<OPEN>\t<HIGH>\t<LOW>\t<CLOSE>\t<TICKVOL>\t<VOL>\t<SPREAD>\n" + "\n".join(self.rows) + "\n")

    def write_mapping(self, rows=None, **changes):
        data = dict(schemaVersion=1, providerId=module.PROVIDER, symbol="US100", timeframe="H4",
                    sourceSha256=module.digest(self.source.read_bytes()), mappingStatus="DocumentaryAudited",
                    evidenceReference=str(self.evidence), evidenceSha256=module.digest(self.evidence.read_bytes()),
                    rows=rows if rows is not None else [
                        dict(nativeOpenTime="2026.06.22 00:00:00", openUtc="2026-06-21T21:00:00Z"),
                        dict(nativeOpenTime="2026.06.22 04:00:00", openUtc="2026-06-22T01:00:00Z")])
        data.update(changes)
        self.mapping.write_text(json.dumps(data))

    def run_import(self):
        return module.import_h4(self.source, module.digest(self.source.read_bytes()), self.mapping,
                                module.digest(self.mapping.read_bytes()), self.output, "2026-11-20T15:00:00Z")

    def test_valid_import_idempotence_and_coverage_integration(self):
        first = self.run_import()
        self.assertEqual(first, self.run_import())
        self.assertEqual(2, first["closedCandleCount"])
        self.assertEqual(self.source.read_bytes(), (self.output / "original.tsv").read_bytes())
        self.assertEqual("Unauthenticated", first["documentaryHistoricalAvailability"])
        result = inspect(ROOT, self.root, "2026-06-22T05:00:00Z", 8, self.output / "manifest.json")
        self.assertTrue(result["requestedCoverageAvailable"])
        self.assertEqual(2, result["closedCandleCount"])
        self.assertEqual(1, inspect(ROOT, self.root, "2026-06-22T04:59:59Z", 8,
                                    self.output / "manifest.json")["closedCandleCount"])

    def test_audited_segment_imports_continuous_native_history(self):
        self.write_mapping(rows=None, segments=[dict(nativeStartInclusive="2026.06.22 00:00:00",
                                                    nativeEndExclusive="2026.06.22 08:00:00",
                                                    serverUtcOffsetMinutes=180)])
        mapping = json.loads(self.mapping.read_text())
        mapping.pop("rows")
        self.mapping.write_text(json.dumps(mapping))
        self.assertEqual(2, self.run_import()["closedCandleCount"])
        self.assertTrue(inspect(ROOT, self.root, "2026-06-22T05:00:00Z", 8,
                                self.output / "manifest.json")["requestedCoverageAvailable"])

    def test_gap_and_future_invariance(self):
        self.rows[1] = self.rows[1].replace("04:00:00", "08:00:00")
        self.write_source()
        self.write_mapping(rows=[dict(nativeOpenTime="2026.06.22 00:00:00", openUtc="2026-06-21T21:00:00Z"),
                                 dict(nativeOpenTime="2026.06.22 08:00:00", openUtc="2026-06-22T05:00:00Z")])
        self.assertEqual(1, len(self.run_import()["missingSourceRanges"]))
        early = inspect(ROOT, self.root, "2026-06-22T01:00:00Z", 4, self.output / "manifest.json")
        self.assertEqual(1, early["closedCandleCount"])

    def test_invalid_ohlc_and_malformed_rows(self):
        self.rows[0] = self.rows[0].replace("30377.9", "30000.0")
        self.write_source()
        self.write_mapping()
        with self.assertRaisesRegex(ValueError, "Invalid OHLC"):
            self.run_import()
        self.rows[0] = "broken"
        self.write_source()
        self.write_mapping()
        with self.assertRaisesRegex(ValueError, "Malformed"):
            self.run_import()

    def test_nonfinite_price_is_rejected(self):
        self.rows[0] = self.rows[0].replace("30377.9", "NaN")
        self.write_source()
        self.write_mapping()
        with self.assertRaisesRegex(ValueError, "Invalid OHLC"):
            self.run_import()

    def test_mismatch_and_unresolved_mapping(self):
        for change in (dict(providerId="other"), dict(symbol="NAS100"), dict(timeframe="H1"),
                       dict(mappingStatus="Unresolved")):
            self.write_mapping(**change)
            with self.assertRaisesRegex(ValueError, "identity or audit status"):
                self.run_import()
        self.write_mapping(rows=[dict(nativeOpenTime="2026.06.22 00:00:00", openUtc="2026-06-21T21:00:00Z")])
        with self.assertRaisesRegex(ValueError, "Unresolved timestamp"):
            self.run_import()

    def test_order_duplicates_and_conflict(self):
        self.rows.reverse()
        self.write_source()
        self.write_mapping()
        with self.assertRaisesRegex(ValueError, "Out-of-order"):
            self.run_import()
        self.rows.reverse()
        self.rows.append(self.rows[1])
        self.write_source()
        self.write_mapping()
        self.assertEqual(2, self.run_import()["closedCandleCount"])
        self.rows[-1] = self.rows[-1].replace("30413.0", "30414.0")
        self.write_source()
        self.write_mapping()
        with self.assertRaisesRegex(ValueError, "Conflicting duplicate"):
            self.run_import()

    def test_source_hash_and_existing_conflict(self):
        with self.assertRaisesRegex(ValueError, "SHA256 mismatch"):
            module.import_h4(self.source, "0" * 64, self.mapping, module.digest(self.mapping.read_bytes()),
                             self.output, "2026-11-20T15:00:00Z")
        with self.assertRaisesRegex(ValueError, "SHA256 mismatch"):
            module.import_h4(self.source, module.digest(self.source.read_bytes()), self.mapping, "0" * 64,
                             self.output, "2026-11-20T15:00:00Z")
        self.run_import()
        self.rows[0] = self.rows[0].replace("30377.9", "30378.0")
        self.write_source()
        self.write_mapping()
        with self.assertRaisesRegex(ValueError, "Existing import conflicts"):
            self.run_import()

    @unittest.skipUnless(Path("C:/MoneyWay/session/raw/2026-06-29/US100_H4_202606220000_202606302000.csv").exists(),
                         "Registered external June source unavailable")
    def test_registered_original_june_export_imports_without_changing_source(self):
        original = Path("C:/MoneyWay/session/raw/2026-06-29/US100_H4_202606220000_202606302000.csv")
        normalized = Path("C:/MoneyWay/session/normalized/2026-06-29/4H.csv")
        self.source.write_bytes(original.read_bytes())
        with normalized.open(newline="") as handle:
            rows = list(csv.DictReader(handle))
        points = []
        from datetime import timedelta
        from inspect_nasdaq_h4_coverage import utc
        for row in rows:
            native = (utc(row["open_time_utc"]) + timedelta(hours=3)).strftime("%Y.%m.%d %H:%M:%S")
            points.append(dict(nativeOpenTime=native, openUtc=row["open_time_utc"]))
        self.write_mapping(rows=points)
        report = self.run_import()
        self.assertEqual(42, report["closedCandleCount"])
        self.assertEqual(original.read_bytes(), (self.output / "original.tsv").read_bytes())
        self.assertEqual(4, inspect(ROOT, self.root, "2026-06-22T13:00:00Z", 40,
                                    self.output / "manifest.json")["closedCandleCount"])


if __name__ == "__main__":
    unittest.main()
