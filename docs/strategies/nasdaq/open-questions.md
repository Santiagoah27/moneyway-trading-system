# MoneyWay Nasdaq open questions

No answer is proposed from external trading theory. `Blocking level` is the earliest affected capability.

Both candidate-confirmation plus origin-extension collisions are resolved by direct human review: bullish `Close > prior HH` with `High > origin ceiling` validates the old HL and rolls to new HH + HL; bearish `Close < prior LL` with `Low < origin floor` validates the old LH and rolls to new LL + LH. In both, the confirming candle stays outside old-turn geometry. An exact corrective body includes it exactly once as the first member of the *next* correction; otherwise the valid side awaits correction start with an empty new turn. Valid bullish `prior validated HL < prior structural HH` and bearish `prior structural LL < prior validated LH` make simultaneous strict confirmation and opposite-point invalidation unreachable; wick penetration plus an invalidating close remains an invalidation event. Ordinary non-confirming resets and reset/invalidation precedence remain separate. `STRUCTURAL_CANDIDATE_TURN_BOUNDARY = READY_FOR_DETERMINISTIC_IMPLEMENTATION` for supplied valid structural references and an active candidate turn; this does not resolve full raw-history reconstruction.

## 4H structure

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-H4-001 | ¿Qué regla reproducible selecciona el primer visible anchor estructural y termina el primer recorrido de raw closed 4H candles? | The evident upper origin of a dominant bearish leg and the mirrored lower/origin bullish structure are confirmed visual concepts. Selection still uses visual clarity and dismissal of minor pauses; neither exact swing geometry nor a raw-history stopping boundary is defined. Latest-break and active-pair rules apply only after valid references exist. | `human_validation_required` | `semi_automatic_backtesting` | Annotated starting histories showing included/excluded initial anchors, competing visible extremes and the exact initial historical scan termination boundary |
| NQ-Q-H4-002 | ¿Qué threshold, si alguno, distingue un near-doji/small body fuera de las comparaciones exactas? | Exact `Close == Open` is neutral; mirrored active-turn membership, including prior-HH/LL equality and wick-only penetration absent another terminal event, plus reset, invalidation, collision precedence and opening-gap behavior are confirmed. Near-doji classification remains non-reproducible. | `PARTIALLY_DEFINED` | `semi_automatic_backtesting` | Direct near-doji/small-body evidence if a distinction beyond exact body direction is intended |
| NQ-Q-H4-003 | ¿Qué tolerancia, si alguna, fusiona body prices distintos en una structural zone y cuáles son las coordenadas para structural points distintos de los candidate HL/LH? | Exact equal coordinates now form one price-level identity without candle ownership, but near-equal merging and other-point coordinates remain undefined. Bullish reset/invalidation membership constrains the supplied turn; for a selected LH turn, the highest `Open`/`Close` body edge remains confirmed and separate from the sell SL wick anchor. | `PARTIALLY_DEFINED` | `semi_automatic_backtesting` | Direct near-equal tolerance evidence and coordinates for other structural-point classes |
| NQ-Q-H4-004 | ¿Cuál es la vigencia y política de conflicto/reinicialización de un ancla H4 elegida por una persona? | Its body-edge structural coordinate, wick protection coordinate and exact source-event identity are now confirmed, but the input cannot be accepted unambiguously across sessions, after `StructureInvalidated` or with competing human choices; the runtime result when absent is also unspecified. | `UNRESOLVED` | `semi_automatic_backtesting` | Human-reviewed examples of reuse across sessions, replacement after invalidation, competing assertions and a missing-anchor replay frame |

Confirmed or materially narrowed by the new human source review:

- No fixed candle/day/session count, time window, scan count, pivot period or numeric lookback exists for 4H initialization: `H4_FIXED_LOOKBACK_RULE = CONFIRMED_NONE`. The mentor does not scan indefinitely in the reviewed clean-chart example, but no deterministic stopping boundary follows: `H4_INITIAL_HISTORICAL_SCAN_TERMINATION = HUMAN_VALIDATION_REQUIRED`.
- The mentor's initial visible upper extreme for the dominant bearish leg and mirrored lower/origin bullish structure are confirmed concepts: `H4_INITIAL_VISIBLE_ANCHOR_CONCEPT = CONFIRMED`. Choosing either from arbitrary raw candles remains `H4_INITIAL_VISIBLE_ANCHOR_SELECTION = HUMAN_VALIDATION_REQUIRED`. The currently visible chart, zoom, screen width, highest/lowest visible candle and arbitrary loaded history cannot become engine selectors.
- For a human-selected initial 4H source candle, `H4_INITIAL_ANCHOR_STRUCTURAL_COORDINATE = CONFIRMED`: High-side `max(Open, Close)`, Low-side `min(Open, Close)`. `H4_INITIAL_ANCHOR_PROTECTION_COORDINATE = CONFIRMED` separately uses High-side `High` or Low-side `Low`; neither wick price becomes the structural break level or an executable Stop Loss.
- `H4_INITIAL_ANCHOR_SOURCE_EVENT_IDENTITY = CONFIRMED`: the reviewed candle/event defines where the directional push and operational initialization begin. It need not own the price level exclusively. Two High-side body tops at `100` still form one exact-price level if the human selects the second candle as the source. The choice is not an automatic last-candle rule; historical candle time and later human-observation time remain distinct.
- `H4_INITIAL_ANCHOR_LIFETIME`, `H4_INITIAL_ANCHOR_REINITIALIZATION`, `H4_INITIAL_ANCHOR_CONFLICT_POLICY` and `H4_MISSING_INITIAL_ANCHOR_RUNTIME_STATUS` remain `UNRESOLVED`. No per-session rule, replacement rule, duplicate precedence or missing-input status is copied from preparation completion.
- Once valid structural references exist, strict formally closed `Close > prior structural HH` or `Close < prior structural LL` supplies inherited bullish or bearish direction: `H4_DIRECTION_FROM_LATEST_CONFIRMED_BREAK = CONFIRMED`. Equality, wick-only penetration and current candle colors alone do not.
- After that break, operational history compresses to the latest HH+HL or LL+LH active pair under existing causal candidate rules: `H4_ACTIVE_STRUCTURAL_PAIR_AFTER_CONFIRMED_BREAK = CONFIRMED` and `H4_HISTORY_COMPRESSION_TO_ACTIVE_PAIR = CONFIRMED`. Older structure remains auditable; minor internal oscillations do not replace the active pair.
- Finding the latest broken structural level or scanning backward for the first validated pair is circular at initialization because the compared historical levels need an already-established anchor. `H4_RAW_CANDLE_INITIALIZATION = HUMAN_VALIDATION_REQUIRED`; neither active-pair compression nor a visible absolute extreme initializes arbitrary raw history.
- Exact equal body coordinates among candles in the same selected turn represent one structural price level: `STRUCTURAL_EQUAL_PRICE_IDENTITY = CONFIRMED_PRICE_LEVEL`. No first/last candle, timestamp or sequence priority is required, and multiple supporting candles do not create multiple HL/LH points: `STRUCTURAL_EQUAL_PRICE_CANDLE_TIE_BREAK = CONFIRMED_NOT_REQUIRED` and `STRUCTURAL_EQUAL_PRICE_SHARED_ZONE = CONFIRMED`.
- For a lower/HL turn, structural price is the minimum body edge and protection is the minimum `Low`; for an upper/LH turn, structural price is the maximum body edge and protection is the maximum `High`. Different candles may supply those extrema without creating two structural points: `STRUCTURAL_PROTECTION_WICK_ACROSS_TURN = CONFIRMED`. The executable Stop Loss offset remains unresolved.
- “Shared zone” for exact equality defines no numeric width. Whether different prices should merge remains `NEAR_EQUAL_STRUCTURAL_ZONE_TOLERANCE = UNRESOLVED`; no tick size, epsilon, point distance or percentage band is inferred.
- On the relevant 1H or 4H timeframe, HH/LL require a formally closed candle body beyond the prior human-selected structural High/Low; wick-only penetration is insufficient and may instead be classified contextually as liquidity take, Wickfill or Fakeout.
- A candidate low becomes a validated HL only when the impulse originating from it later produces a formally closed body above the prior HH on the same relevant timeframe. A rebound without that close does not validate the low as structural HL.
- A candidate high becomes a validated LH only when the impulse originating from it later produces a formally closed body below the prior LL on the same relevant timeframe. A decline without that close does not validate the high as structural LH.
- Before the confirming break candle closes, the preceding retracement remains candidate/unconfirmed. An open 1H/4H candle that temporarily trades beyond the level cannot validate it.
- For candidate HL, the mentor looks backward across the human-delimited corrective phase and discards preliminary/intermediate lows. The selected candidate is the deepest low of that correction and the base from which the impulse that later closes above the prior HH originates.
- Small one- or two-candle pauses inside that impulse do not create another candidate HL.
- For bullish candidate HL, the prior HH remains the upper structural boundary. Once HH production stops, the first bearish candle starts the correction; it remains active through deeper lows/zigzags and ends only when a formally closed candle has `Close > prior HH`.
- When the current HH-producing candle is distinct from that first bearish candle, the HH candle belongs to the prior bullish impulse and is excluded from the correction turn and its body/wick geometry. The bearish mirror excludes a distinct LL-producing candle before the first bullish correction candle. An exact-doji candle producing either extreme is also excluded before the first opposite body; it cannot start the correction. These pre-start exclusions are confirmed and do not alter active-turn continuity or same-candle reset-and-start membership.
- Every candle printed while awaiting the first opposite-direction body remains outside the future correction, not only the distinct HH/LL candle. In bullish structure, same-direction bullish bodies and exact dojis before the first `Close < Open` remain in the prior impulse/exhaustion area; a strict new `High` updates the upper price extreme without starting the turn. In bearish structure, bearish bodies and exact dojis before the first `Close > Open` likewise remain outside; a strict new `Low` updates the floor. Contained waiting candles remain excluded in either direction. After `ResetAndAwaitCorrectionStart`, the reset candle and subsequent waiting candles are also excluded until the first opposite body. None contributes OHLC to the later correction-turn geometry. No numeric zone width or near-doji threshold follows from the exhaustion-area description.
- The lifecycle disposition after `ResetAndAwaitCorrectionStart` is `AwaitingCorrectionStart` for the still-valid structural side; it is distinct from `StructureInvalidated` after either prior-HL/LH body-close invalidation. Both have an empty turn, but only the former permits `CorrectionStartTransition` on subsequent closed candles. The invalidation candle belongs to the new opposite impulse context without validating an opposite swing or pair; no same-side correction may restart from that invalidated state. A later valid opposite structural context must be established first under existing bootstrap/structure rules. Runtime preserves this distinction, but no primitive owns automatic active-turn membership at the prior structural validation boundary; initial-anchor, historical scan and candidate-detection limits still prevent automatic reconstruction.
- Once that bullish correction is active, every in-range intermediate bearish, bullish or exact-doji candle remains in the same correction turn until a terminal/reset event. Color alternation, consolidation and internal oscillation do not fragment the turn or create parallel HL candidates.
- During the active bullish correction, `High > current correction-origin ceiling` first resets the correction, updates the ceiling and discards its current candidate HL. That higher price extreme is not automatically a validated structural HH. The same closed candle is then classified: a bearish body is excluded from the old turn and included as the first member of the new bullish-correction turn; a neutral or bullish body starts no new correction.
- A formally closed `Close < prior validated HL` invalidates the bullish structure and ends pursuit of the same candidate HL. Equality and wick-only `Low < prior HL` with `Close >= prior HL` do not invalidate under this rule. The invalidation candle is excluded from the destroyed turn and candidate geometry, cannot alter its structural price or protection anchor, and supplies new bearish-impulse evidence without automatically validating a structural LL.
- A deeper low inside that active correction replaces a shallower candidate. The deepest candidate becomes validated only from the causal close `AsOfUtc`; later data cannot alter an earlier historical evaluation.
- If the first bearish correction candle also prints a higher `High`, the higher extreme takes precedence: its `High` updates the correction-origin ceiling and the correction begins only when the same candle closes bearish. No finer intrabar path is inferred.
- The updated correction-origin ceiling is not thereby a validated structural HH. Structural HH validation still requires the formally closed body break beyond the prior structural HH.
- An exact doji (`Close == Open`) is directionally neutral and starts neither correction. At that `AsOfUtc`, correction remains not started; wait for a later qualifying bearish-body candle in bullish structure or bullish-body candle in bearish structure without switching timeframe to manufacture direction.
- If the exact doji prints a new `High` above the current correction-origin ceiling or a new `Low` below the current correction-origin floor, update that price extreme but keep directional correction not started. Extreme update and correction start are separate.
- Near-doji/small-body treatment remains unresolved because no numeric epsilon, tick distance or body-size threshold is supplied.
- Opening/quotation gaps are no longer open for structural mechanics. `previous.Close != current.Open` has no special structural role or size threshold: exact body direction still governs correction start, strict High/Low extremes govern reset, formally closed Close governs invalidation, and a gap alone does not fragment an active turn. The missing interval is not interpolated and is not an FVG/IFVG.
- Direct bearish evidence shows the retracement originating from the prior LL area. Its candidate high remains provisional until a formally closed candle has `Close < prior LL`; only then are the new LL and preceding LH validated from that causal `AsOfUtc`. Wick-only or open-candle penetration is insufficient.
- After a prior LL, once the bearish impulse stops producing new lows, the first bullish candle that begins upward displacement starts the bearish correction. The correction ends only at the causal formally closed `Close < prior LL`.
- If that first bullish candle also prints a lower `Low`, the lower extreme takes precedence: its `Low` updates the correction-origin floor and the correction begins only when the same candle closes bullish. No finer intrabar path is inferred.
- The updated correction-origin floor is not thereby a validated structural LL. Structural LL validation still requires the formally closed body break beyond the prior structural LL.
- Within one recognized bearish correction, the candidate LH is the highest candidate high. A later higher high replaces the prior candidate, and superseded intermediate highs do not survive. This complex candidate-LH identity and highest-high replacement are directly confirmed; the candidate becomes validated only from the causal closed `Close < prior LL` `AsOfUtc`.
- The bearish correction start, exact-doji no-start behavior, in-range turn membership including non-confirming prior-LL equality/wick-only candles, strict new-Low reset, same-candle bullish restart, prior-LH body-close invalidation, invalidation-candle exclusion and invalidation-dominant collision are confirmed. Near-doji treatment and broader correction/impulse segmentation remain unresolved. The causal break candle is confirmed excluded from correction membership/scanning and belongs to the impulsive leg. Within an already selected LH turn, the structural coordinate and bearish body anchor are confirmed as the highest `Open`/`Close` body edge among member candles.
- The sell risk example anchors Stop Loss above the highest wick/tail of the relevant LH. This wick anchor remains separate from the confirmed body-based structural LH coordinate for target selection, reconstruction or comparisons.
- Within the selected candidate-HL turn, the structural level is the lowest `Open` or `Close` of the included candle bodies. An isolated wick does not define it.
- Exact-doji behavior and mirrored bullish/bearish terminal/reset membership are confirmed. Prior-HH/LL equality and wick-only penetration stay in the active turn when no independent terminal event occurs; their OHLC remains eligible for selected-turn geometry. The formally closed body-break candle is excluded. Near-doji thresholds remain unresolved. Exact equal body coordinates define one structural price level without a candle/timestamp tie-break; near-equal merging tolerance remains unresolved.
- In the general case, the causal break candle is excluded from the correction set and retrospective correction scan. Its close confirms the new HH/LL, ends the correction and validates the preceding candidate from that `AsOfUtc`.
- In the single-expansive-candle case, the same candle may contain impulse-origin evidence and the confirming close without becoming an ordinary correction member. Its `Open` is the structural HL/LH origin coordinate and its `Close` is the causal validation event. The buy protection anchor remains its `Low`/lowest wick and the sell protection anchor its `High`/highest wick; no intrabar chronology is inferred.
- At 08:00 `America/Bogota`, use only information observable through the closed 4H candle. Lower-timeframe noise does not independently create 4H structural points.

The latest human review further confirms that 4H bootstrap starts visually/contextually from the evident extreme that originated the current directional leg, then reconstructs push, retracement, structural break and current structure. There is no fixed lookback, but the first-anchor selector and historical scan termination remain human-reviewed. Once references exist, the latest confirmed strict body-close break supplies inherited direction and the decision focuses on the resulting active HH/HL or LL/LH pair without deleting older audit history. Asia/London extrema, weekly open and previous-day references remain separate operational context and do not seed 4H structure.

Mirrored terminal boundaries are no longer open: `BEARISH_RESET_INVALIDATION_SYMMETRY = CONFIRMED_MIRROR`; `BEARISH_CORRECTION_TURN_MEMBERSHIP = CONFIRMED`; `BEARISH_NEW_LOW_RESET_CANDLE_DUAL_ROLE = CONFIRMED`; `BEARISH_RESET_BULLISH_CANDLE_NEW_TURN_MEMBERSHIP = CONFIRMED_INCLUDED`; neutral and bearish reset candles have `CONFIRMED_NO_START`; prior-LH body-close invalidation, invalidation-candle exclusion and its new-bullish-impulse role are confirmed. The corresponding bullish terminal classifications remain confirmed as previously documented.

The strict dual-event collision is no longer open: `BULLISH_RESET_INVALIDATION_COLLISION_PRECEDENCE = CONFIRMED_INVALIDATION_DOMINATES`; `BULLISH_COLLISION_UPPER_EXTREME_ROLE = CONFIRMED`; `BULLISH_COLLISION_CANDLE_PREVIOUS_TURN_MEMBERSHIP = CONFIRMED_EXCLUDED`; `BULLISH_COLLISION_CANDLE_PREVIOUS_CANDIDATE_GEOMETRY = CONFIRMED_EXCLUDED`; `BULLISH_COLLISION_CANDLE_NEW_BEARISH_IMPULSE_ROLE = CONFIRMED`; `BULLISH_COLLISION_STRUCTURAL_CHANGE_ROLE = CONFIRMED`. The collision requires both `High > current correction-origin ceiling` and `Close < prior validated HL` on the same formally closed candle. Its `High` remains an upper price extreme and bearish-origin reference, not an automatically validated LH or executable Stop Loss. The closed observation changes context from bullish to bearish without synthetic intrabar chronology or retroactive mutation of earlier replay snapshots. No canonical Step-3/Step-4 `RuleId` mapping or same-frame propagation is inferred.

The bearish mirror collision is also closed: `BEARISH_RESET_INVALIDATION_COLLISION_PRECEDENCE = CONFIRMED_INVALIDATION_DOMINATES` and `BEARISH_COLLISION_LOWER_EXTREME_ROLE = CONFIRMED`. It requires both `Low < current correction-origin floor` and `Close > prior validated LH` on the same formally closed candle. The candidate LH is destroyed, the candle is excluded from the old turn and geometry, no new bearish correction starts, and the candle belongs to the new bullish impulse context. Its `Low` remains a price extreme, bullish-origin reference and wick-based protection evidence rather than an automatically validated HL, Structural Low or executable Stop Loss.

The reviewed bearish collision has narrow context-specific Step-3/Step-4 evidence: the mentor treats its lower wick/new Low as liquidity take and its body close above prior LH as structural change. It remains open whether and how those observations map to canonical `NQ-LIQ-003` and `NQ-M5-001` after selected-liquidity, 4H alignment, timeframe and other prerequisites are proven. Current replay gating checks downstream eligibility against prerequisites established before the current frame, so it cannot propagate a newly established Step 3 into Step 4 eligibility within that same frame. No graph or runtime resolution is inferred.

Broader structural states remain: `BULLISH_CORRECTION_START = CONFIRMED`; `BEARISH_CORRECTION_START = CONFIRMED`; `CANDIDATE_HL_SELECTION = PARTIALLY_DEFINED`; `CANDIDATE_LH_SELECTION = CONFIRMED` for highest-candidate identity within a recognized bearish correction; `BULLISH_CORRECTION_WINDOW = PARTIALLY_DEFINED`; `BEARISH_CORRECTION_WINDOW = PARTIALLY_DEFINED`; `TURN_MEMBERSHIP_SEGMENTATION = PARTIALLY_DEFINED`; `STRUCTURAL_CANDIDATE_DETECTION = PARTIALLY_DEFINED`; `NQ_H4_DIRECTION_BOOTSTRAP = PARTIALLY_DEFINED`. Pre-start waiting exclusion and non-confirming prior-HH/LL equality/wick-only inclusion remain confirmed. Both confirmation-plus-origin-extension cases now validate the old candidate, roll the active pair and assign exact-body next-turn membership; valid reference ordering excludes strict confirmation plus invalidation. Initial-anchor selection, historical scan termination, near-equal tolerance, near-doji treatment, other structural coordinates, mitigation, PDH/PDL ranking, TP hierarchy/full-exit behavior and executable Stop Loss offset remain open for broader use.

Still unresolved for broader structural detection: deterministic first-anchor selection and historical scan termination, near-equal structural-zone tolerance, near-doji/small-body threshold, other structural-point coordinates, mitigation semantics and executable Stop Loss offset. Prior-HH/LL equality and wick-only geometry membership are resolved; the prior structural validation reference remains distinct from the correction-origin reset extreme. Exact equal-coordinate body identity is one price level with no candle tie-break. Exact `Close == Open` is neutral/no-start, and ordinary reset/invalidation terminal membership remains confirmed. The absence of a fixed lookback is confirmed; no invented pivot, threshold, tolerance or fixed-point initialization may replace the missing first-anchor rule. Replay must not relabel prior decisions using future evidence.

## Resolved workflow relationships

- Canonical order is Step 1 4H context → Step 2 liquidity marking → Step 3 liquidity take → Step 4 5M Structural Change `OR` IFVG → Step 5 separate mandatory 5M FVG → Step 6 1M pullback/FVG interaction and realignment → entry eligibility.
- Every downstream gate requires its prerequisites in chronological order; a later pattern cannot repair a missing earlier gate through hindsight.
- Step 1 market structure and Step 2 session-liquidity references are separate.
- A Step-4 IFVG is not a direct entry and does not automatically satisfy the Step-5 FVG gate.
- At `StrategyReplayContext.AsOfUtc`, future liquidity takes, FVGs or 1M realignments cannot change a prior eligibility state.
- Preparation starts from 08:00 and Steps 1 and 2 must complete in `[08:00, 08:30)` for the same session; the operational entry-acquisition interval is `[08:30, 11:00)` in `America/Bogota`. The former 11:30 cutoff was an incorrect interpretation and is not a second limit.
- At 11:00, an unfinished pre-entry setup expires. A dead-zone signal cannot revive it; no forced close of an already-entered conceptual trade is established.
- At or after 08:30, the first valid event that exceeds a relevant High or goes below a relevant Low is the effective Step 3 for the scenario it may orient; it is not target consumption before Step 3.
- A Low take can orient a possible buy progression and a High take can orient a possible sell progression only when aligned with the Step-1 4H permitted direction. Without alignment, Step 4 is blocked, the prior imagined scenario remains discarded and price is not chased.
- A valid liquidity take activates waiting for Step 4. Continued manipulation-direction movement and a wick-only break do not confirm Step 4 and do not alone cancel the setup.
- Before Step 4 and before cutoff, each valid farther same-side take keeps one active setup at Step 3 and updates its current 5M structural reference while prior observations remain auditable. It does not create a concurrent setup or satisfy Step 4.
- Once Step 3 has activated a setup, exact touch of its already-selected intended target before completed pre-entry progression cancels that setup; wick contact is sufficient without close or tolerance. Do not chase price. Later evidence cannot revive it. This active-setup cancellation is distinct from selecting a structural fallback when a session target was consumed before the operational setup context.
- The opposite take never reverses permitted direction automatically. When aligned with the existing Step-1 4H direction, it may activate Step 3 of a new chronological setup; otherwise it creates no countertrend setup.
- Exact runtime mapping of an unmet prerequisite to `failed`, `waiting` or `not_applicable` remains unaudited.

## Break

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-BR-001 | ¿Cómo se selecciona el structural support/resistance level o zone relevante y qué width, tolerance o minimum penetration se aplica a un marginal close? | A 4H body close beyond the selected level/zone is confirmed and wick-only penetration is rejected, but the structural input and numeric boundary remain undefined. | `unresolved` | `semi_automatic_backtesting` | Annotated level/zone selections and positive, negative and marginal close cases |

## Wick

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-WI-001 | ¿Qué prior wick extreme completa Wickfill, basta tocarlo o debe superarse, se requiere close y qué retracement/zone/tolerance aplica? | The post-Breakout wickfill progression is confirmed, but its completion and selected inputs are not deterministic. | `human_validation_required` | `analysis` | Annotated complete/incomplete Wickfill sequences, including multiple extrema and boundary cases |

Confirmed by manual source-video re-verification: Wickfill follows an extended Breakout that left a wick/extreme; after price moves away or retraces, a later impulse seeks to travel through that space and reach or exceed the prior extreme. Approximate evidence: definition `04:23–04:32`, example `06:55–07:25`. “Reach or exceed” does not resolve the exact completion comparison.

## Fake

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-FA-001 | ¿Cómo se selecciona el relevant 4H range/zone, cuánto puede tardar el retorno y qué tolerance, returning-candle choice, invalidation y bias effect aplican? | A new candle closing back inside after a failed Breakout is confirmed, but zone selection and lifecycle remain non-deterministic. | `human_validation_required` | `analysis` | Annotated positive, negative, marginal and overlapping-context examples |

Confirmed by manual source-video re-verification: Fakeout is a Breakout that did not complete; price returns and a new candle closes back inside the relevant range/zone. Wick-only return is insufficient. Approximate evidence: definition `10:40`, return example `10:55–11:25`.

## 4H context relationship

Confirmed conceptually: at 08:00 `America/Bogota`, Breakout `OR` Wickfill `OR` Fakeout are alternative current 4H context classifications. Causally, Wickfill may follow an extended Breakout and Fakeout is a failed Breakout that closes back inside. Formal mutual exclusivity and precedence remain `human_validation_required` when visual conditions appear to coexist.

## Liquidity

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-LQ-001 | ¿Cómo se tratan near-dojis fuera de las comparaciones exactas, qué significa no mitigado y cómo se ordenan PDH/PDL frente a 1H/4H? | Exact doji no-start, non-confirming prior-HH/LL boundary inclusion, opening-gap behavior, mirrored terminal membership, confirming-candle exclusion and the single-candle `Open` origin coordinate are confirmed. Mitigation and cross-class ranking remain unresolved or partial. | `PARTIALLY_DEFINED` | `semi_automatic_backtesting` | Direct near-doji evidence, other structural classes, mitigation boundaries, PDH/PDL coexistence and ranking ties |
| NQ-Q-LQ-002 | ¿Un nivel ya barrido expira o puede reutilizarse como future relevant level, y altera su consumo previo alguna validity/probability/risk consideration? | Same-side setup association is resolved, but level reuse and any probability/risk effect remain undefined. | `unresolved` | `semi_automatic_backtesting` | Repeated-level cases with explicit validity, probability and risk outcomes |
| NQ-Q-LQ-004 | ¿Qué algoritmo detecta los candidate turning points 1H/4H y qué tolerancia define que los niveles ya validados coinciden con un extremo de sesión? | Causal validation and structural confluence are confirmed, but candidate detection and coincidence geometry are not deterministic. | `human_validation_required` | `semi_automatic_backtesting` | Annotated candidate turns, confirming closes and coincidence boundary cases |

### Resolved session-liquidity question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-LQ-003 | For day `D`, Asia is `[D-1 17:00, D 02:00)` and London is `[D 02:00, D 07:00)` in `America/Bogota`, start-inclusive and end-exclusive. Completed 1H candles belong by local `OpenTime`; session High/Low are maximum `High`/minimum `Low`. | `confirmed` | Manual source-video re-verification |

## Session schedule

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SC-001 | ¿Qué días, feriados, cierres o sesiones se permiten? | Preparation starts at 08:00 and must complete before 08:30 `America/Bogota`, but calendar eligibility remains undefined. | `unresolved` | `paper_trading` | Verified calendar policy and examples |
| NQ-Q-SC-002 | ¿Cómo se gestionan posiciones abiertas después de las 11:00? | The 11:00 cutoff governs acquisition of new entries; no forced close of an already-entered conceptual trade is confirmed. | `unresolved` | `paper_trading` | Explicit post-entry management examples after cutoff |
| NQ-Q-SC-003 | ¿Cómo observa el replay que Steps 1 y 2 se completaron para el mismo día antes de 08:30? | [ADR 0006](../../decisions/0006-define-nasdaq-preparation-replay-input-contract.md) specifies one immutable, source-backed positive completion assertion for session `D`, with a causal UTC timestamp and `AsOfUtc` isolation. `StrategyReplayContext` now carries this bounded observation; human-dependent `NQ-H4-001` and `NQ-LIQ-002` cannot be inferred from candles. The [runtime state matrix](strategy-specification.md#60-preparation-completion-nq-time-003) is confirmed. | `confirmed` | `semi_automatic_backtesting` | Implement the deterministic evaluator; no producer, storage or UI mechanism is selected here |

The confirmed preparation prerequisite is not enforced by current canonical workflow metadata: `NQ-LIQ-003` lists `NQ-H4-001`, `NQ-LIQ-002` and `NQ-TIME-001`, but omits `NQ-TIME-003`. The latter is already required and confirmed in rule metadata, while its replay capability remains `NotImplemented`. Prerequisite reconciliation awaits a deterministic runtime result; a later Step 3 cannot repair missed preparation for day `D`.

## Liquidity sweep

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SW-001 | ¿Qué close-back, rechazo o desplazamiento adicional, si alguno, califica un take después de superar el selected High o bajar del selected Low? | Direction-dependent setup association is now confirmed, but any qualification beyond exceed/break-below remains undefined. | `human_validation_required` | `semi_automatic_backtesting` | Positive, negative and marginal take examples |
| NQ-Q-SW-002 | ¿Puede utilizarse una toma anterior a 08:30? | Affects operating sequence. | `unresolved` | `paper_trading` | Explicit pre-open examples and mentor decision |

Resolved by direct human review of Video 3 at approximately `06:20–06:40` and `10:30–10:55`: same-side subsequent takes retain one pending setup at Step 3 and roll its active 5M reference; opposite-side target consumption cancels the setup; the opposite take can become Step 3 of a new setup only when aligned with Step-1 4H context. There is no source support for concurrent same-direction setups or automatic bias reversal.

### Resolved post-08:30 Step-3 orientation question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-SW-006 | After 08:30, the first valid take of relevant liquidity is itself the effective Step 3; it is not target consumption in a separate interval before Step 3. Taking a relevant Low can orient a possible buy progression, and taking a relevant High can orient a possible sell progression, only when aligned with the Step-1 4H permitted direction. Without alignment, Step 4 is not enabled, the originally imagined scenario remains discarded and price is not chased. | `confirmed` | Direct human review of Video 3 approx. `07:50–09:15`; alignment reference approx. `04:25` |

The confirmed Step-3 boundary remains `price exceeds the selected relevant High` or `price goes below the selected relevant Low`. The reviewed conversational words "touch/take" do not unequivocally establish equality-only activation, and no threshold or tolerance is inferred. This `CONFIRMED_EXCEEDS` geometry is distinct from `TARGET_TOUCH_EVENT`, where exact contact is sufficient for an already-selected target.

### Resolved target-consumption question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-SW-003 | The candidates are London Low/Asia Low for sells and London High/Asia High for buys. When distinct, the immediate target is the first relevant level encountered in the expected direction of travel. Exact contact with the horizontal session-extreme wick level is sufficient, including wick contact; no body close, candle close, tolerance or penetration buffer applies. A pre-entry touch cancels the active setup. | `confirmed` | Direct human review of Video 3 approx. `09:40–10:55` and `13:25–13:40` |
| NQ-Q-SW-005 | No candle timeframe is normative. The target is an absolute Asia/London price level and `TARGET_TOUCH_EVENT` is a timeframe-independent price-quote touch. The mentor's 1M chart is the visual post-entry management surface, not part of the target definition. | `confirmed` | Direct human review of Video 3 approx. `13:10–13:45` |

### Resolved target edge and fallback semantics

- When the relevant Asia and London targets have the same absolute price, they collapse into one unique target rather than two sequential targets. In the reviewed scenario, the shared level is final Take Profit and does not create an artificial intermediate Break-Even transition.
- When a relevant session target was already consumed before the operational setup context, or otherwise no longer remains the next future-valid target before that context, the source confirms structural fallback from Step 2. Sell candidates include relevant unmitigated support represented by a validated 1H/4H HL or LL and Previous Day Low. Buy candidates include relevant unmitigated resistance represented by a validated 1H/4H LH or HH and Previous Day High.
- In reviewed paths where distinct relevant 1H and 4H fallback levels coexist, 1H acts as the first objective or management level and the farther 4H level as the final or extended objective; 4H is structurally stronger. This evidence-bounded relationship is not a universal ranking formula.
- Structural support/resistance is marked from candle bodies, not an isolated long wick. Exact HL/LL/LH/HH detection, OHLC mitigation semantics, candle/body selection, reduction of a body zone to one decimal, PDH/PDL priority and cross-class ties remain unresolved. The fallback is therefore `PARTIALLY_DEFINED`, not deterministic.
- Structural fallback does not rescue an originally imagined scenario after an opposite-liquidity take at or after 08:30.
- Once a setup is active with a selected target, touching that target before entry completion cancels the setup. This is not a fallback event: do not chase price and do not revive the cancelled setup with later evidence.
- Direct source trace for the latest fallback reconciliation: Video 3 approx. `03:10–03:25` and `05:55–06:40`; Video 1 approx. `10:10–10:35`; Video 2 approx. `08:35–09:20` and `11:10–11:45`.
- At or after 08:30, the first valid opposite-liquidity take is the effective Step 3 for the newly oriented scenario. The prior imagined scenario is discarded. Progression to Step 4 requires alignment with Step-1 4H; otherwise there is no new setup, revival or chase.

### Replay data-resolution limitation

`NQ-Q-SW-004` is reclassified as `MARKET_DATA_RESOLUTION_LIMITATION`, not an unresolved strategy timeframe rule. Current canonical replay exposes closed OHLC `CandleSeries`. A candle range can establish `TARGET_WAS_TOUCHED_DURING_INTERVAL`, but when the same minimum-granularity candle also completes `NQ-M1-003`, OHLC cannot establish intrabar order. The audit categories are `UNAMBIGUOUS_CANDLE_TOUCH` for a target first observed in a later candle after entry progression was already established, and `AMBIGUOUS_SAME_OBSERVATION_TOUCH` when touch and final confirmation share one minimum observation. No runtime result or verdict mapping is selected.

## 5M structure

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-M5-001 | ¿Cómo se convierte el extreme left by the liquidity take en la primera referencia 5M y cómo se seleccionan después swing, pivots, internal swings, updated active references y marginal closes? | A newer extreme may update the active reference before Step 4, but exact reference/associated-low selection and strong/decisive close criteria are not reproducible. | `human_validation_required` | `semi_automatic_backtesting` | Annotated post-take structure with accepted/rejected initial and updated references, associated levels and closes |

## IFVG

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-IF-001 | ¿Cuál es la geometría, dirección, close, mitigation, confirming candle, expiry y relación con displacement del IFVG? | IFVG is an OR alternative but lacks operational definition. | `unresolved` | `semi_automatic_backtesting` | Audited mentor definition; no external ICT/SMC source |
| NQ-Q-IF-002 | ¿Cómo afecta una actualización same-side de la active 5M reference a un IFVG candidate previo o en formación? | The setup-level reference update is confirmed, but branch-specific reset/retention behavior was not stated. | `unresolved` | `semi_automatic_backtesting` | Sequenced same-side update examples that include the IFVG branch |

Resolved: IFVG is only an alternative inside Step 4. It does not enable direct entry, bypass Step 5 or automatically count as the mandatory Step-5 FVG.

## Continuation FVG

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-FVG-001 | ¿Qué tamaño relativo/absoluto hace el FVG claro y fuerte? | One/two points are insufficient, but minimum is null. | `human_validation_required` | `semi_automatic_backtesting` | Annotated accepted/rejected FVGs across volatility conditions |
| NQ-Q-FVG-002 | ¿Cómo se tratan fill, invalidation, lifetime y multiple FVG selection? | Controls whether the mandatory FVG remains usable. | `unresolved` | `semi_automatic_backtesting` | Sequenced examples and explicit lifecycle rules |

## 1M entry

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-M1-001 | ¿Qué exact interaction depth/tolerance con el Step-5 FVG, corrective swing selection y timeout confirman el pullback y la realineación 1M? | Countertrend pullback toward/into the FVG followed by intended-direction realignment is confirmed, but its geometry is not reproducible. | `human_validation_required` | `semi_automatic_backtesting` | Annotated FVG interactions, corrections, pivots, failures and timeouts |
| NQ-Q-M1-002 | ¿Qué order type, timing, slippage, distance and maximum attempts apply? | Defines executable entry mechanics. | `unresolved` | `paper_trading` | Audited execution examples and explicit limits |

### Resolved entry-cutoff question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-M1-003 | A confirmation at or after 11:00 cannot complete or revive an unfinished pre-entry setup. The operational entry-acquisition interval is `[08:30, 11:00)`; after cutoff, do not chase and return the next operational day. This does not define forced closure of a trade entered before cutoff. | `confirmed` | Direct human source re-review; Video 3 approx. `04:15–04:30` |

## Stop Loss

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SL-001 | ¿Qué algoritmo identifica el structural 5M HL para buys y el structural 5M LH para sells? | The conceptual reference is confirmed, including the buy `Low`/lowest-wick and sell `High`/highest-wick protection anchors after the relevant turn is selected, but deterministic swing selection is not. | `human_validation_required` | `semi_automatic_backtesting` | Annotated HL/LH selections and rejected alternatives |
| NQ-Q-SL-002 | ¿Qué offset exacto más allá de la wick estructural aplica, incluidos spread, maximum Stop and oversized-stop behavior? | The buy `Low` and sell `High` protection anchors are confirmed, but “below/above” does not define an executable price, buffer, spread/cost adjustment or broker order semantics. This does not redefine structural coordinates. | `unresolved` | `paper_trading` | Approved quantitative offset/cost policy and boundary cases |

## Break Even

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-BE-003 | ¿Qué relación conserva el earlier post-entry swing observation con el nuevo first-important-liquidity trigger? | The authoritative trigger changed; silently combining or substituting both would invent management behavior. | `unresolved` | `paper_trading` | Complete examples showing both events and the mentor's chosen trigger |
| NQ-Q-BE-004 | ¿Mover a entry/Break-Even usa el entry price bruto o incorpora costos, y el trigger aplica universalmente a toda operación? | Touch and target selection are narrowed, but exact executable BE price and universality are not established. | `unresolved` | `paper_trading` | Approved cost-basis policy and diverse complete trades |

### Resolved Break-Even questions

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-BE-001 | For distinct targets, a sell uses the first relevant selected Low encountered downward and a buy uses the first relevant selected High encountered upward. When the relevant Asia/London prices are equal, they are one final target in the reviewed scenario and do not create an intermediate Break-Even step. Structural-fallback management beyond its target role remains unaudited. | `confirmed` for the reviewed session-target cases | Direct human review of Video 3 approx. `03:10–03:25`, `08:50–10:55` and `13:25–13:40`; Video 1 approx. `10:10–10:30` |
| NQ-Q-BE-002 | Exact touch is sufficient, including wick contact, without body close, candle close or tolerance. The source supports moving Stop Loss to entry/Break-Even; executable cost basis remains covered by `NQ-Q-BE-004`. | `confirmed` | Direct human review of Video 3 approx. `09:40–10:55` and `13:25–13:40` |

## Take Profit

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-TP-001 | ¿El primer target Asia/London encontrado es siempre el TP final y exige salida completa, o puede ser solo un target de gestión previo? | One reviewed case supports full exit on touch, but it does not establish universal equivalence between the first BE target and final TP. | `unresolved` | `paper_trading` | Complete trades distinguishing first management target from final exit |
| NQ-Q-TP-002 | ¿Existe fixed RR, partials, trailing or manual close? | Required for reproducible results and management. | `unresolved` | `paper_trading` | Complete audited trades and explicit management statements |

Confirmed target scope: primary buy candidates are London High and Asia High; primary sell candidates are London Low and Asia Low. When distinct and future-valid, first encountered in the expected price path determines the immediate target. Equal relevant session prices collapse into one unique target and the reviewed equal case treats it as final Take Profit without an intermediate Break-Even transition. If no relevant session candidate remains future-valid before the operational setup context, structural fallback is `PARTIALLY_DEFINED`: sells may use relevant unmitigated support at a validated 1H/4H HL or LL or Previous Day Low; buys may use relevant unmitigated resistance at a validated 1H/4H LH or HH or Previous Day High. In reviewed multi-level paths, 1H is the first objective or management level and the farther 4H level is final or extended, without establishing a universal formula. Candidate-HL and candidate-LH selected-turn body coordinates are confirmed, while deterministic detection, correction-edge handling, mitigation, other structural-point coordinates and PDH/PDL ranking remain unresolved or partial. The selected target is an absolute price and its touch is a timeframe-independent quote-level event without close or tolerance. Video 3 approximately `13:25–13:40` supports complete exit in one additional distinct-target case only, so universal final-exit behavior remains unresolved. Closed-candle replay can observe occurrence from OHLC range but not same-candle intrabar order.

## Risk

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-R-001 | ¿Cómo se calcula position size using points/ticks, tick value, spread, commissions and account size? | A confirmed 1% cap is not executable without sizing. | `unresolved` | `paper_trading` | Nasdaq-specific approved formula and calculation cases |
| NQ-Q-R-002 | ¿Qué trade-count, consecutive-loss, reduction, kill-switch and rejected-order controls apply? | Missing controls block safe automation. | `unresolved` | `demo_execution` | Approved risk policy and failure-path tests |

## Daily limits

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-DL-001 | ¿El límite diario de pérdida de 1% es correcto, qué incluye y cuándo se reinicia? | Current evidence is candidate only. | `candidate` | `paper_trading` | Manual verification of closing segment and explicit scope |

## Reentries

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-RE-001 | ¿La frase de no reentry aplica tras BE, SL, same setup, whole day or independent setup, and how many attempts? | Prevents an unsupported universal ban. | `unresolved` | `paper_trading` | Full quoted context and multiple exit/reentry examples |

## News

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-NE-001 | ¿Qué events/source/windows/open-position/new-entry/resumption policy applies? | The 10:00/09:45 example is contextual only. | `unresolved` | `paper_trading` | Explicit policy across multiple news cases and calendar source |

## Market data

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-MD-001 | ¿Qué instrument, symbol, provider and timestamp normalization are authoritative, and how is missing session data reported? | Required for trustworthy Asia/London levels, closes and replay; timezone, session intervals and 1H membership are already confirmed. | `unresolved` | `semi_automatic_backtesting` | Provider mapping, timestamp comparisons and missing-data cases |

## Automation

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-AU-001 | ¿Qué subjective criteria can become deterministic with validated accuracy? | Swing, context, sweep and FVG quality block full automation. | `unresolved` | `semi_automatic_backtesting` | Audited dataset, approved labels and out-of-sample tests |
| NQ-Q-AU-002 | ¿Qué manual timestamp verification and approvals enable demo execution? | Critical source claims require comparison with original video. | `unresolved` | `demo_execution` | Timestamp audit, approval protocol and traceability review |
| NQ-Q-AU-003 | ¿Qué evidence could ever permit autonomous execution? | The current version explicitly prohibits it. | `unresolved` | `autonomous_execution` | Future human decision after all rules, risk and controls are audited |
