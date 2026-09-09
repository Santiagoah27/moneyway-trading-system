# MoneyWay Nasdaq changelog

## nasdaq-0.1.0-draft

- Initial repository documentation.
- Based on the complete 58:24 mentorship analysis.
- Confirmed sequence separated from subjective and unresolved criteria.
- Manual source-video re-verification confirmed preparation at 08:00, trading from 08:30 through 11:30, and `America/Bogota` without DST for those times.
- Manual source-video re-verification confirmed the Asia interval `[D-1 17:00, D 02:00)` and London interval `[D 02:00, D 07:00)` in `America/Bogota`, with extrema calculated from completed 1H candles by local `OpenTime`.
- Manual source-video re-verification clarified 4H HH/HL/LL/LH structure and Breakout/Wickfill/Fakeout semantics: structural breaks require body closes, HL/LH confirmation is retrospective without future leakage, and the three current-context states use `OR`. Approximate evidence: Breakout `02:20–02:40`, Wickfill `04:23–04:32` and `06:55–07:25`, Fakeout `10:40` and `10:55–11:25`; pivot/retracement selection, zone geometry, Wickfill completion and classification precedence remain unresolved.
- The same re-verification resolved the prior Stop Loss interpretation in favor of the structural 5M HL for buys and structural 5M LH for sells; deterministic swing geometry remains unresolved.
- Take Profit direction was confirmed as important highs for buys and important lows for sells; the deterministic meaning of important remains unresolved.
- The six-stage strategy sequence was re-verified directly against the source video.
- News and reentry policies remain unresolved.
- Not approved for autonomous execution.
