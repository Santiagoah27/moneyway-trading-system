# MoneyWay Nasdaq

- Strategy: MoneyWay Nasdaq.
- Specification version: `nasdaq-0.1.0-draft`.
- Current status: audited draft; not approved for autonomous execution.
- Source material: análisis completo y auditado del video de mentoría de 58:24, incluida una re-verificación manual directa del video fuente.

## Available documents

- [Strategy specification](strategy-specification.md): flujo, alcance y límites operativos.
- [Rule catalog](rule-catalog.md): reglas, evidencia y estado de automatización.
- [Open questions](open-questions.md): definiciones pendientes y evidencia requerida.
- [Reference cases](reference-cases.md): casos conceptuales sin generalizaciones automáticas.
- [Changelog](changelog.md): historial de versiones documentales.

## Permitted modes

- Assisted analysis.
- Manual backtesting.
- Semi-automatic backtesting with human validation.
- Supervised paper trading with human validations.
- Future supervised demo execution only after critical blockers are resolved and timestamps are manually verified.

Real-money trading and autonomous execution are prohibited.

## Canonical gated workflow

1. At 08:00 `America/Bogota`, review the closed 4H context and permitted direction.
2. Mark Asia/London extrema and relevant 1H/4H structural liquidity references; these levels do not seed 4H structure.
3. From 08:30, require a relevant liquidity take before enabling any 5M setup.
4. On 5M, require Structural Change `OR` IFVG, whichever occurs first.
5. Separately require the directional 5M FVG confirmation produced after the Step-4 trigger.
6. On 1M, require a countertrend pullback toward/into that FVG and structural realignment before entry eligibility.

The operational entry-acquisition window is `[08:30, 11:00)` in `America/Bogota`. All pre-entry gates must complete before 11:00. At 11:00, any unfinished setup expires; do not continue into the mentor's "zona muerta," chase price or carry the incomplete setup into a later day. This cutoff does not define forced closure of a conceptual trade entered before 11:00.

After a valid liquidity take, wait for Step 4 while the setup remains inside the operational window and no audited cancellation has occurred. A subsequent same-side take or farther extreme before Step 4 keeps the same single setup at Step 3 and updates its active 5M structural reference; it does not create a parallel setup or advance Step 4. Historical references remain auditable. The primary opposite-side target candidates are the Step-2 Asia/London session extrema: London Low or Asia Low for a sell-oriented setup, and London High or Asia High for a buy-oriented setup. When two future-valid session levels differ, the immediate target is the first relevant level price encounters in the expected direction; neither session has fixed priority. When both prices coincide, they form one shared target rather than two sequential targets, and the reviewed scenario treats that shared level as final Take Profit without a separate intermediate Break-Even target. If a session target was already consumed before the operational period or is otherwise no longer a future objective, the source falls back conceptually to the next relevant structural 1H/4H point from Step 2; its detection, timeframe priority and ranking are not deterministic. Exact target contact remains sufficient, including wick contact, without body close, candle-close confirmation or tolerance. Once a setup is active, a touch before completed pre-entry progression cancels it and price must not be chased. That opposite take does not reverse the permitted direction automatically: only alignment with the existing Step-1 4H context may make it a new Step-3 activation for a new setup.

No downstream pattern is valid for this strategy when a mandatory prerequisite was not satisfied in chronological order. Management concepts use a structural 5M HL/LH wick anchor for Stop Loss and the first distinct favorable target for Break-Even; a coincident Asia/London level used as final Take Profit in the reviewed scenario does not create an artificial BE step at the same price. A target is an absolute price level, and its semantic event is a timeframe-independent price-quote touch. The mentor monitors post-entry management visually on 1M, but 1M does not define the target. Closed-candle replay can prove occurrence within an interval, while optional high-resolution observations can provide finer evidence; unsupported intrabar order remains explicitly unobservable.

## Critical open variables

- Exact HH/LL and structural swing detection.
- Liquidity priority, structural-point coincidence and sweep qualification beyond the confirmed session extrema.
- Deterministic 5M HL/LH detection and exact Stop Loss offset beyond the structural wick.
- Deterministic construction, 1H/4H priority and ranking of the structural target fallback.
- Target handling from 08:30 until Step-3 activation when a session target is touched but no setup is active yet.
- Same-observation intrabar ordering with current closed-candle replay data.
- Whether the first distinct Break-Even target is also the final Take Profit target outside the reviewed complete-exit case.
- News policy.
- Reentries.
- FVG quality threshold.

The preparation and trading-window times use `America/Bogota` and do not use DST. The confirmed conceptual Stop Loss and Take Profit references remain non-automatable until their structural-selection algorithms are defined.

> No utilizar esta documentación para operación autónoma. Una regla `confirmed` puede seguir siendo no automatizable cuando su evaluación sea subjetiva.
