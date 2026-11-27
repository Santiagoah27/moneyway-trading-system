# Nasdaq Trigger-Anchored Downstream Research V1

`EXP-NQ-TRIGGER-ANCHORED-DOWNSTREAM-RESEARCH-V1` / `1.0.0` enlaza el recorrido
`TriggerAnchored` de V2 con los candidatos existentes de SL y TP. El recorrido
es investigación condicional. Su preparación H4, revisión independiente y
disponibilidad temporal autenticada tienen indicadores separados.
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)

## Integración y límites

El informe [end-to-end V1](nasdaq-end-to-end-multitimeframe-research-v1.md) usa
`M1M5Binding.FvgId` y líneas de vida del FVG posterior para proyectar la cadena.
El informe [opcional V2](nasdaq-m5-to-m1-optional-fvg-path-v2.md) ya construye el
primer pullback y realineamiento desde el trigger sin requerir FVG, pero no
entregaba ese cierre M1 a los candidatos de SL/TP. El nuevo modo toma el
realineamiento exacto que seleccionó V2, y conserva todas las identidades SC/IFVG
empatadas, junto con las velas y los antecedentes de su toma.

- SL reutiliza `EvaluateM5StructuralStopLossCandidateUseCase` y los perfiles
  simétricos 1/1, 2/2 y 3/3. Se conserva el último HL para BUY o LH para SELL según
  el perfil, el cierre del vecino derecho y la comprobación del lado protectivo.
- TP reutiliza `NasdaqStructuralTakeProfitCandidate` para el swing M5 favorable
  más cercano y para los extremos de sesión H1 exactos del calculador existente.
  Los extremos de sesión se reconstruyen a la hora del cierre M1. Solo se
  consideran referencias observables y se preservan empates. Se conservan por
  separado los resultados de SL y TP, incluso si uno queda no disponible.
- Cada evaluación se comparte por evento de mercado M1 dentro del mismo
  instrumento, proveedor y día. Las asociaciones preservan episodio y
  configuración. Los niveles son referencias geométricas sin precio ejecutable,
  estimación de rentabilidad, fills ni cálculo nuevo de reward/risk.
- El FVG opcional conserva la formación, las tres velas, la disponibilidad nominal
  y el estado al realineamiento. Las formaciones posteriores se muestran como
  `laterOptionalFvgs` sin estado al realineamiento. Un empate temporal no acredita
  orden intrabar. Un IFVG invalidado conserva su identidad y la hora de
  invalidación. La geometría de una rama invalidada sigue siendo un dato
  condicional y no restablece la rama.
- Se exponen `M5TriggerObserved`, `M1PullbackObserved`, `M1RealignmentObserved`,
  `StructuralSlCandidateAvailable`, `StructuralTpCandidateAvailable`,
  `H4ContextAvailable`, `IndependentHumanValidation` y
  `AuthenticatedTemporalAvailability`. Las tres últimas distinguen H4
  experimental de su eventual admisión. `CanonicalAdmissionPermitted=false`
  y `OperationalSignal=false`.

## Development del 23 de junio de 2026

OANDA Global Markets MT5 Demo / US100. Corte 16:30 UTC documental. Los números
de eventos excluyen duplicaciones por perfil o asociación.

| Etapa | Triggers M5 distintos | Realineamientos M1 distintos |
| --- | ---: | ---: |
| Realineamiento V2 TriggerAnchored | 5 | 6 |
| SL protectivo disponible | 3 | 3 |
| TP favorable disponible | 5 | 6 |
| Ambos | 3 | 3 |

Los realineamientos de 13:30, 13:32 y 13:58 se comparten con `FvgAnchored`.
Los de 13:38, 13:41 y 13:56 son exclusivos de `TriggerAnchored`.
Los de 14:07 y 14:17 se observan exclusivamente con `FvgAnchored` en V2.
A las 13:30, 13:32 y 13:38 el SL propuesto quedó igual o más allá del cierre M1
en los tres perfiles. A las 13:41 pasan la comprobación geométrica solo los
perfiles 2/2 y 3/3. Ningún evento M1 de la muestra quedó sin TP.

De las 576 asociaciones de configuración, 320 quedaron sin trigger. De ellas,
288 carecían de toma calificable y 32 quedaron fuera por la ventana de la toma.
Las 256 restantes tuvieron primer realineamiento. Cuarenta de esas asociaciones
quedaron sin SL protector en todos sus perfiles, 136 quedaron sin FVG opcional
elegible visible al realineamiento para ninguno de sus triggers y 16 conservaron
un IFVG invalidado al realineamiento. No aparecieron discontinuidades
inexplicadas M5/M1 en esa muestra. Los controles sintéticos incluyen ausencia de
TP, intervalos faltantes, BUY, SELL y empate SC/IFVG.

El resultado H4 fue `H4ContextUnavailable` y hubo cero cadenas Standard completas.
Para el demo futuro, el informe permite identificar qué combinación exacta de
toma, trigger, pullback, ancla y niveles necesita revisión. Siguen pendientes
una dirección H4 admisible, evidencia temporal autenticada, revisión humana
independiente, elección del perfil SL/TP y parámetros ejecutables. La ventana
del reporte se toma de V2 y no cambia la candidata 0.4.0.

## Reproducción

Desde la raíz con `MoneyWay.sln` compilada, indicar una salida inexistente.

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$out = Join-Path $env:TEMP ('moneyway-trigger-downstream-' + [guid]::NewGuid())
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $out --compare-trigger-anchored-downstream-v1 --cutoff-utc 2026-06-23T16:30:00Z
```

El Worker acepta el paquete Development con SHA-256
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`.
Dos ejecuciones del nuevo reporte produjeron SHA-256
`3c930b6a44ec20542673ef3ba981a29654d0bd5787f4092e609816bd7eb13a92`.
La geometría de los seis eventos coincidió con la corrida aislada de 13:58.
Las pruebas mutaron y extendieron el futuro en un corte sintético fijo.
Los reportes previos conservaron sus bytes, con SHA-256
`d19c506bbce9609a8d3237e9f21b527d4d0150a1ed0339dc3a86f04735b8cb61` para
end-to-end V1 y `2f9687c2dc812f5623b3085d5b012d32c71b5e621c534f12911379c7c4bea345`
para opcional V2.
