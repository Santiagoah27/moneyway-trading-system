# MoneyWay Nasdaq

- Strategy: MoneyWay Nasdaq.
- Specification version: `nasdaq-0.1.0-draft`.
- Current status: audited draft; not approved for autonomous execution.
- Source material: análisis completo y auditado del video de mentoría de 58:24, revisado en cinco intervalos.

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

## Critical open variables

- Stop Loss selection.
- Timezone and DST.
- Take Profit.
- News policy.
- Reentries.
- FVG quality threshold.
- Swing-detection algorithms.

> No utilizar esta documentación para operación autónoma. Una regla `confirmed` puede seguir siendo no automatizable cuando su evaluación sea subjetiva.
