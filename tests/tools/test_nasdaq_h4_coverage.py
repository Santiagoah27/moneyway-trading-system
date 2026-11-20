import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("h4_coverage", ROOT / "tools/research/inspect_nasdaq_h4_coverage.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
SESSION = Path("C:/MoneyWay/session")


@unittest.skipUnless(SESSION.exists(), "Registered external June source unavailable")
class NasdaqH4CoverageTests(unittest.TestCase):
    def inspect(self, cutoff, hours):
        return module.inspect(ROOT, SESSION, cutoff, hours)

    def test_exact_complete_interval_and_original_source(self):
        report = self.inspect("2026-06-22T13:00:00Z", 16)
        self.assertTrue(report["requestedCoverageAvailable"])
        self.assertEqual(4, report["closedCandleCount"])
        self.assertEqual("H4", report["originalTimeframe"])
        self.assertFalse(report["derived"])
        self.assertEqual("US100", report["instrument"])
        self.assertEqual("oanda-global-markets-mt5-demo", report["providerId"])
        self.assertFalse(report["canonicalAdmissionPermitted"])

    def test_leading_missing_source_is_explicit(self):
        report = self.inspect("2026-06-22T13:00:00Z", 40)
        self.assertFalse(report["requestedCoverageAvailable"])
        self.assertEqual("2026-06-20T21:00:00Z", report["missingSourceRanges"][0]["startUtc"])

    def test_unclosed_candle_excluded_at_cutoff(self):
        before = self.inspect("2026-06-22T12:59:59Z", 16)
        at = self.inspect("2026-06-22T13:00:00Z", 16)
        self.assertEqual(3, before["closedCandleCount"])
        self.assertEqual(4, at["closedCandleCount"])
        self.assertTrue(all(c["nominalCloseUtc"] <= before["cutoffUtc"] for c in before["candles"]))

    def test_reproducible_and_future_invariant(self):
        first = self.inspect("2026-06-22T13:00:00Z", 16)
        self.assertEqual(first, self.inspect("2026-06-22T13:00:00Z", 16))
        later = self.inspect("2026-06-23T13:00:00Z", 40)
        self.assertEqual(first["candles"], [c for c in later["candles"] if first["requestedStartUtc"] <= c["openUtc"] and c["nominalCloseUtc"] <= first["cutoffUtc"]])

    def test_reserved_assets_are_not_modified(self):
        manifest = ROOT / "src/frontend/moneyway-web/src/research/context/data/contextual-validation/manifest.json"
        before = manifest.read_bytes()
        self.inspect("2026-06-26T13:00:00Z", 40)
        self.assertEqual(before, manifest.read_bytes())


if __name__ == "__main__":
    unittest.main()
