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

After a valid liquidity take, wait for Step 4 while the setup remains inside the operational window and no audited cancellation has occurred. A subsequent same-side take or farther extreme before Step 4 keeps the same single setup at Step 3 and updates its active 5M structural reference; it does not create a parallel setup or advance Step 4. Historical references remain auditable. If price instead consumes the intended opposite-side target before formal entry confirmation, cancel the setup and do not chase price. That opposite take does not reverse the permitted direction automatically: only alignment with the existing Step-1 4H context may make it a new Step-3 activation for a new setup. Exact target selection/touch geometry, active-reference geometry and IFVG-specific interaction remain unresolved.

No downstream pattern is valid for this strategy when a mandatory prerequisite was not satisfied in chronological order. Management concepts then use a structural 5M HL/LH wick anchor for Stop Loss, relevant directional liquidity for Take Profit and the first important favorable liquidity target for Break-Even; their exact executable geometry remains unresolved.

## Critical open variables

- Exact HH/LL and structural swing detection.
- Liquidity priority, structural-point coincidence and sweep qualification beyond the confirmed session extrema.
- Deterministic 5M HL/LH detection and exact Stop Loss offset beyond the structural wick.
- Deterministic priority among relevant upside/downside liquidity targets.
- Deterministic first-important-liquidity and touch semantics for Break-Even.
- News policy.
- Reentries.
- FVG quality threshold.

The preparation and trading-window times use `America/Bogota` and do not use DST. The confirmed conceptual Stop Loss and Take Profit references remain non-automatable until their structural-selection algorithms are defined.

> No utilizar esta documentación para operación autónoma. Una regla `confirmed` puede seguir siendo no automatizable cuando su evaluación sea subjetiva.
