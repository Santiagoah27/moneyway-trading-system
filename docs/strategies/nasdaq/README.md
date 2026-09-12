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
3. From 08:30, the first valid liquidity take establishes the effective Step 3 for the scenario that may proceed; require alignment with the Step-1 4H permitted direction before enabling any 5M setup.
4. On 5M, require Structural Change `OR` IFVG, whichever occurs first.
5. Separately require the directional 5M FVG confirmation produced after the Step-4 trigger.
6. On 1M, require a countertrend pullback toward/into that FVG and structural realignment before entry eligibility.

The operational entry-acquisition window is `[08:30, 11:00)` in `America/Bogota`. All pre-entry gates must complete before 11:00. At 11:00, any unfinished setup expires; do not continue into the mentor's "zona muerta," chase price or carry the incomplete setup into a later day. This cutoff does not define forced closure of a conceptual trade entered before 11:00.

After 08:30 there is no separate interval in which an effective opposite-liquidity consumption occurs "before Step 3": that first valid take is the observed Step 3. Taking a relevant Low can orient a possible buy progression and taking a relevant High can orient a possible sell progression, but only when that orientation matches the Step-1 4H permitted direction. Otherwise Step 4 is not enabled, the originally imagined scenario remains discarded, and price is not chased. Step-3 geometry remains the audited `exceeds relevant High` or `goes below relevant Low`; conversational use of "touch/take" does not establish equality-only activation.

On the relevant 1H or 4H structure, a candidate low becomes a validated HL only after the impulse originating from it produces a formally closed candle body above the prior HH. Direct bearish evidence separately confirms that a retracement originates from the prior LL area and its candidate high remains provisional until a formally closed candle has `Close < prior LL`; only then are the new LL and preceding highest candidate high validated from that causal `AsOfUtc`. Within the same recognized bearish correction, a higher candidate replaces a lower candidate and superseded intermediate highs do not survive as LH candidates. Wick-only penetration and a still-open candle are insufficient. Historical replay cannot use either candidate as validated before its confirming close. For an already selected LH turn, the structural coordinate is its highest candle-body edge: the maximum `Open` or `Close` among member candles, independent of candle color; a higher wick does not redefine it. For the relevant sell LH, Stop Loss is separately anchored above its highest wick/tail, so structural identity, body-based structural coordinate and wick-based SL anchor remain distinct. The causal break candle is excluded from correction membership and retrospective candidate scanning; it belongs to the impulsive expansion, and its close confirms the new HH/LL, ends the correction and validates the preceding candidate. A single expansive candle may contain impulse-origin evidence and the confirming close without becoming a correction candle, but its exact origin-price coordinate and intrabar path remain unresolved. Bearish correction edge handling and segmentation remain partial. For bullish candidate HL, the prior HH remains the upper boundary; after HH production stops, the first bearish candle starts the correction, which stays active through deeper lows and internal zigzags until a formally closed `Close > prior HH`. The deepest low/base is the candidate and replaces a shallower low; one- or two-candle pauses inside the confirming impulse do not create another HL. Within the selected turn, use the lowest body `Open`/`Close`, not an isolated wick. Equal lowest body coordinates preserve the price when only price is needed but do not choose candle identity. Doji/gap handling, exact boundary inclusivity and turn membership also remain unresolved.

After an aligned valid liquidity take, wait for Step 4 while the setup remains inside the operational window and no audited cancellation has occurred. A subsequent same-side take or farther extreme before Step 4 keeps the same single setup at Step 3 and updates its active 5M structural reference; it does not create a parallel setup or advance Step 4. Historical references remain auditable. The primary opposite-side target candidates are the Step-2 Asia/London session extrema: London Low or Asia Low for a sell-oriented setup, and London High or Asia High for a buy-oriented setup. When two future-valid session levels differ, the immediate target is the first relevant level price encounters in the expected direction; neither session has fixed priority. When both prices coincide, they form one shared target rather than two sequential targets, and the reviewed scenario treats that shared level as final Take Profit without a separate intermediate Break-Even target. If no session target remains future-valid before the operational scenario, a sell may fall back to relevant unmitigated support at a validated 1H/4H HL or LL, or Previous Day Low; a buy may fall back to relevant unmitigated resistance at a validated 1H/4H LH or HH, or Previous Day High. In reviewed paths with distinct relevant structural levels, 1H is the first objective or management level and the farther, structurally stronger 4H level is the final or extended objective. This observed hierarchy is not a universal formula. Candidate-HL selection and its selected-turn lowest-body anchor are partially defined; confirmed bearish candidate-LH identity and its selected-turn highest-body-edge coordinate still require unresolved correction-edge and other structural-point semantics before target selection can be deterministic. Mitigation and PDH/PDL ranking remain non-deterministic. This fallback does not rescue an originally imagined scenario whose opposite liquidity is consumed after 08:30. Exact selected-target contact remains sufficient, including wick contact, without body close, candle-close confirmation or tolerance. Once a setup is active, a selected-target touch before completed pre-entry progression cancels it and price must not be chased. That active-setup lifecycle rule is distinct from the first post-08:30 take that establishes Step 3.

No downstream pattern is valid for this strategy when a mandatory prerequisite was not satisfied in chronological order. Management concepts use a structural 5M HL/LH wick anchor for Stop Loss and the first distinct favorable target for Break-Even; a coincident Asia/London level used as final Take Profit in the reviewed scenario does not create an artificial BE step at the same price. A target is an absolute price level, and its semantic event is a timeframe-independent price-quote touch. The mentor monitors post-entry management visually on 1M, but 1M does not define the target. Closed-candle replay can prove occurrence within an interval, while optional high-resolution observations can provide finer evidence; unsupported intrabar order remains explicitly unobservable.

## Critical open variables

- Bullish correction start/end and candidate-HL price selection are partially defined; bearish prior-LL origin, causal LH validation, complex candidate-LH selection, highest-high replacement and selected-turn highest-body-edge coordinate are confirmed. Confirming-candle correction membership and scan exclusion are confirmed, as are its causal validation role and the single-candle origin/confirmation coexistence case. The bearish correction window remains partially defined; doji/gap handling, exact boundary inclusivity, turn membership and the single-candle origin-price coordinate remain non-deterministic. The sell Stop Loss highest-wick anchor is separate from the confirmed body-based structural LH coordinate.
- Liquidity priority, structural-point coincidence and sweep qualification beyond the confirmed session extrema.
- Deterministic 5M HL/LH detection and exact Stop Loss offset beyond the structural wick.
- Remaining HL/LL/LH/HH candidate construction beyond the partially defined HL case, OHLC meaning of unmitigated, and PDH/PDL ranking or ties within structural fallback.
- Same-observation intrabar ordering with current closed-candle replay data.
- Whether the first distinct Break-Even target is also the final Take Profit target outside the reviewed complete-exit case.
- News policy.
- Reentries.
- FVG quality threshold.

The preparation and trading-window times use `America/Bogota` and do not use DST. The confirmed conceptual Stop Loss and Take Profit references remain non-automatable until their structural-selection algorithms are defined.

> No utilizar esta documentación para operación autónoma. Una regla `confirmed` puede seguir siendo no automatizable cuando su evaluación sea subjetiva.
