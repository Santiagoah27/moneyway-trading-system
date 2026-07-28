# MoneyWay Forex

- Strategy: MoneyWay Forex.
- Specification version: `forex-0.1.0-draft`.
- Specification status: draft; not approved for autonomous execution.
- Source material: análisis auditado de videos teóricos y prácticos de la mentoría MoneyWay.

## Available documents

- [Strategy specification](strategy-specification.md): alcance, flujo y límites operativos.
- [Rule catalog](rule-catalog.md): inventario auditable de reglas y estados.
- [Open questions](open-questions.md): variables pendientes y evidencia requerida.
- [Reference cases](reference-cases.md): casos observados sin generalizar sus detalles.
- [Changelog](changelog.md): historial de versiones documentales.

## Permitted uses

- Assisted analysis.
- Manual backtesting.
- Semi-automatic backtesting only where every required rule is evaluable; unresolved steps require human validation.
- Future paper trading or supervised demo execution only after the missing risk and execution rules are resolved and approved.

## Critical open variables

La construcción de zonas semanales, alineación diaria, geometría de patrones, ruptura, retest, señales de entrada, selección exacta del Stop Loss, gestión de Take Profit, Break Even y riesgo todavía no tienen definición programable completa.

> Esta especificación no debe utilizarse para ejecución autónoma ni con dinero real.
