# MoneyWay Nasdaq changelog

## nasdaq-0.1.0-draft

- Initial repository documentation.
- Based on the complete 58:24 mentorship analysis.
- Confirmed sequence separated from subjective and unresolved criteria.
- Manual source-video re-verification confirmed preparation at 08:00, trading from 08:30 through 11:30, and `America/Bogota` without DST for those times.
- Manual source-video re-verification confirmed the Asia interval `[D-1 17:00, D 02:00)` and London interval `[D 02:00, D 07:00)` in `America/Bogota`, with extrema calculated from completed 1H candles by local `OpenTime`.
- Manual source-video re-verification clarified 4H HH/HL/LL/LH structure and Breakout/Wickfill/Fakeout semantics: structural breaks require body closes, HL/LH confirmation is retrospective without future leakage, and the three current-context states use `OR`. Approximate evidence: Breakout `02:20–02:40`, Wickfill `04:23–04:32` and `06:55–07:25`, Fakeout `10:40` and `10:55–11:25`; pivot/retracement selection, zone geometry, Wickfill completion and classification precedence remain unresolved.
- Human re-review clarified retrospective 4H structural-point confirmation, selection of the relevant retracement turning extreme after the confirming break, rejection of minor fluctuations, body-based structural marking and the 08:00 closed-4H filter. Approximate evidence: `03:05–04:55`, `06:20–07:35`, `08:35–09:20` and `11:10–11:45`; prior-level bootstrap, retracement boundaries and exact numeric body coordinates remain unresolved.
- Human review of Video 3 clarified the end-to-end Nasdaq workflow as a strict chronological gate from 4H context through separate liquidity marking/take, 5M Structural Change `OR` IFVG, mandatory 5M FVG confirmation, 1M pullback/realignment and entry eligibility, followed by structural-wick Stop Loss, directional liquidity Take Profit and first-important-liquidity Break-Even concepts. Asia/London levels remain separate liquidity references and do not seed 4H structure; exact algorithms and runtime status mapping remain unresolved.
- The same re-verification resolved the prior Stop Loss interpretation in favor of the structural 5M HL for buys and structural 5M LH for sells; deterministic swing geometry remains unresolved.
- Take Profit direction was confirmed as important highs for buys and important lows for sells; the deterministic meaning of important remains unresolved.
- The six-stage strategy sequence was re-verified directly against the source video.
- News and reentry policies remain unresolved.
- Not approved for autonomous execution.
