# Nasdaq M5 Trigger Evidence Research V1

`EXP-NQ-M5-TRIGGER-EVIDENCE-RESEARCH-V1` · comparación diagnóstica del 27 de noviembre de 2026. Base de código `aba2d48bf453cc24cbe581012fa718f405bd5831`.

## Autoridad y alcance de la evidencia

| Evidencia | Alcance de esta revisión |
| --- | --- |
| Original del mentor | El audiovisual de Video 3 no estaba disponible en el repositorio ni en las ubicaciones locales de fuentes revisadas. Los archivos de mercado de junio son evidencia documental de precios. |
| Revisión humana histórica | La especificación y el changelog conservan el OR cronológico, el cierre de cuerpo para SC y el caso de toma previa al cierre dentro de la misma vela. Video 3, aproximadamente 12:40–12:55, figura como fuente humana para la geometría C1/C3 del FVG de continuación. Son revisiones previas con su alcance original. [Especificación, §§12–13](../strategies/nasdaq/strategy-specification.md) · [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md) |
| Evidencia de Development | Las dos referencias humanas archivadas son etiquetas ya expuestas de selección de swing. El comparador las usa después del cálculo. No se obtuvo una referencia independiente del primer SC/IFVG ni de su disponibilidad histórica. [Comparación previa](m5-inclusive-confirmation-v2.md) |
| Hipótesis e inferencias | Los métodos siguientes producen evidencia experimental. Atribuir al mentor un pivot de ventana fija, un mínimo de cuerpo o una inversión por punto medio carece de respaldo. [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md) |

## Hipótesis comparadas

| Hipótesis | Formación, confirmación y primera observación de replay | Dependencias y límites |
| --- | --- | --- |
| Liquidity take contextual E1/E2/E3 | Penetración estricta del extremo de sesión en la M5 original. E1 busca la primera tras la disponibilidad del nivel, E2 la primera cuya vela abre desde las 08:30 Bogotá y E3 exige una penetración previa, un cierre de retorno posterior y una nueva penetración, en tres velas distintas. La observación OHLC se conoce al cierre de esa M5. | El inventario de niveles y su cobertura no resuelven la relevancia humana ni la alineación H4. El cierre nominal de la vela no da la hora intrabar del take. [Políticas existentes](contextual-swing-sc-v1.md) |
| SC strict-close con swing revisado | Nivel exacto ya disponible. Una M5 cerrada supera estrictamente el High para BUY o cae estrictamente por debajo del Low para SELL. Igualdad y solo mecha no confirman. | Es la candidata mejor sustentada para continuar con **ancla revisada**. La fuerza del cierre, la elección autónoma del swing y la suficiencia de una ruptura marginal siguen abiertas. CME explica niveles y zonas como contexto general, sin fijar ese contrato Nasdaq. [Candidata existente](../strategies/nasdaq/m5-strict-close-candidate.md) · [CME](https://www.cmegroup.com/education/courses/trading-and-analysis/support-and-resistance) |
| SC contextual H1/H2/H3 | H1 congela el extremo de la vela previa, H2 el extremo de la banda continua desde 08:00 y H3 consume solo una referencia A/B/C/D-V2 ya confirmada al abrir la vela del take. Comparten la ruptura estricta y verifican continuidad y ruptura previa del ancla. H1/H2 son nombres de hipótesis, con fuentes M5. | H1 y H2 son controles de referencia, no equivalencias con un swing validado. H3 se abstiene sin ancla confirmada. Ninguna familia cubre por sí sola el bootstrap ni la sustitución de anclas. [Comparador SC](contextual-swing-sc-v1.md) |
| FVG `GeometryOnly` y `MiddleCloseConfirmation` | En tres M5 contiguas, FVG alcista requiere `C3.Low > C1.High` y bajista `C3.High < C1.Low`. Ambos se conocen al cierre de C3. El segundo perfil exige además cierre C2 más allá del extremo C1 del lado correspondiente. | LuxAlgo añade filtros variables de desplazamiento y tamaño. El experimento no fija mínimo, ATR, tolerancia ni calidad humana. Un hueco entre aperturas o intervalos faltantes no basta. [Código y política](nasdaq-m5-fvg-ifvg-candidate-v1.md) · [LuxAlgo FVG](https://www.luxalgo.com/library/concept/fair-value-gap/) |
| **Inversion FVG** | El FVG original debe existir. La primera M5 posterior cerrada estrictamente por el extremo lejano del lado contrario confirma la inversión. Retiene la zona original y su origen. | La convención de borde completo es un comparador determinista. LuxAlgo también documenta usos del punto medio. Igualdad, toque de mecha y mitigación completa no bastan en este experimento. Expiración y retest quedan abiertos para la estrategia. [LuxAlgo Inversion FVG](https://www.luxalgo.com/library/concept/inversion-fvg/) |
| **Implied FVG** | Geometría proyectada entre puntos medios de mechas externas solapadas. El patrón necesita C3 cerrada antes de que sus mechas definitivas sean observables. | Longitud de mecha, fuerza y dirección del cuerpo central, y confluencia carecen de umbrales acordados. Queda `unresolved`. Se descarta como sustituto de Inversion FVG en esta comparación. [LuxAlgo Implied FVG](https://www.luxalgo.com/library/concept/implied-fvg/) |
| FVG posterior y ruta M1 | El V1 mide primer C3 direccional estrictamente posterior al trigger. `FormationAfterConfirmation` también exige que C1 abra en o después del trigger. V2 compara ese ancla con el primer trigger directamente. | Standard admite FVG de continuación opcional, incluso al mismo cierre con evidencia independiente. La zona origen de IFVG y un FVG posterior son hechos distintos. Un FVG ausente, rechazado o con evidencia incompleta tiene el tratamiento de su contrato existente. [Ruta opcional V2](nasdaq-m5-to-m1-optional-fvg-path-v2.md) |

### Observable en replay y disponibilidad histórica

`StrictConfirmedPivots` devuelve el cierre del último vecino derecho como `ConfirmedAtUtc` y exige contigüidad. Para R barras derechas contiguas en M5, el cierre de confirmación está R × 5 min después del cierre del extremo. A/B/C/D-V2 usan otra condición, continuación estricta respecto al ancla fija del episodio. En la muestra ascendente el extremo inclusivo abre a 13:30 y su selección se conoce a 13:35. En la descendente el extremo abre a 14:20 y la selección se conoce a 14:40. No se adelanta ninguna selección a la apertura histórica donde se dibuja el punto. TradingView ilustra el mismo riesgo con cinco barras derechas. [Comparación A/B/C/D](m5-inclusive-confirmation-v2.md) · [TradingView, Plotting in the past](https://www.tradingview.com/pine-script-docs/concepts/repainting/#plotting-in-the-past)

Las horas de junio son UTC **documental**, derivado en el paquete de la coordenada nativa menos tres horas. La primera disponibilidad reproducible en el replay es el cierre de la última vela requerida, junto con el ancla y los antecedentes visibles. La primera publicación por el proveedor y la disponibilidad contemporánea quedan indeterminadas. Cuatro combinaciones de episodio por política conservan `SameSweepClose`. El orden dentro de la vela se desconoce con esos OHLC. La especificación admite un caso de igualdad solo con evidencia explícita de toma antes de confirmación.

## Resultados del 23 de junio Development

OANDA Global Markets MT5 Demo / US100, corte nominal 16:30. Se usaron 10 H4, 41 H1, 498 M5 y 2.490 M1. El comparador contextual emitió 12 episodios, 24 configuraciones y 288 pares. La tabla fija `SameCloseDiagnostic / FormationAfterConfirmation / GeometryOnly` para comparar el primer SC e IFVG bajo la misma evidencia y cada política. Los conteos son episodios repetidos de un día. H3 se abstuvo sin referencia confirmada en las seis selecciones con toma. No hubo empate de tipos SC/IFVG en el primer cierre elegible de esta muestra.

| Política | Takes elegibles / 12 | IFVG primero | SC primero | Sin trigger | Completan FVG posterior en V1 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `PostSweepObservation` | 6 | 5 | 1 | 6 | 5 |
| `WindowEventOnly` | 4 | 4 | 0 | 8 | 4 |
| `WindowConfirmationWithPriorSweep` | 6 | 5 | 1 | 6 | 6 |

El informe V2 deduplica cuatro tomas de nivel en tres velas, dos SC y 19 IFVG observados verificados por el algoritmo. Dos SC y 12 IFVG son elegibles bajo alguna política, con cinco eventos distintos seleccionados como primeros. En el tramo M5 aislado 07:00–16:30, ambos perfiles produjeron 22 FVG (10 alcistas y 12 bajistas) y 19 inversiones (12 alcistas y 7 bajistas). La igualdad del cierre C2 separa los perfiles en el fixture. A/B/C/D inclusivo cambió el nivel ascendente de 29441.2 a 29381.7 y conservó 29811.6 en el descendente. Las comparaciones históricas de swing fueron 1/2 exactas para cada variante V1 y 2/2 para cada V2 en esas mismas etiquetas expuestas. El caso sintético produjo cambios de −10 o −13 según variante. Estas diferencias muestran sensibilidad a la selección del extremo. Las etiquetas independientes de Step 4 siguen pendientes.

### Contraejemplo reproducible London Low / E1

| Evidencia M5 (UTC nominal del paquete) | OHLC o coordenada | Primera observación del replay |
| --- | --- | --- |
| Take 13:05–13:10 contra LL 29472.7 | 29483.9 / 29494.4 / 29467.1 / 29477.4 | 13:10. Penetración por Low, antes de 08:30 Bogotá. |
| FVG bajista de origen, aperturas C1/C2/C3 12:25 / 12:30 / 12:35 | Zona 29508.0–29509.4 | 12:40, al cierre C3. |
| Inversión alcista 13:20–13:25 | 29495.6 / 29519.6 / 29490.9 / 29509.9 | 13:25, cierre superior a 29509.4. |
| Referencia SC H1/H2, vela 13:00–13:05 | 29499.4 / 29510.2 / 29473.2 / 29483.6 | 13:05, High 29510.2. |
| Ruptura SC 13:30–13:35 | 29484.1 / 29536.9 / 29381.7 / 29532.7 | 13:35, mismo evento y dos IDs de hipótesis. |
| FVG alcista posterior, aperturas C1/C2/C3 13:40 / 13:45 / 13:50 | Zona 29570.3–29582.1 | 13:55. |

Con `PostSweepObservation` la primera evidencia es IFVG, aunque invalide a las 13:30. `WindowEventOnly` excluye la toma. `WindowConfirmationWithPriorSweep` conserva el SC de 13:35 como primer evento en ventana y retiene ambas identidades. Una invalidación posterior se registra en su momento y no vuelve elegible un reemplazo retrospectivo. Los fixtures también conservan empates de SC e IFVG sin asignarles prioridad. Una ruta V2 con trigger sin FVG posterior aparece en ocho asociaciones y añade geometría M1. Eso no resuelve el requisito H4. El end-to-end devolvió `H4ContextUnavailable` y cero cadenas H4→M1 completas. `NQ-M5-001` sigue necesitando un Step 3 alineado, el swing revisado para strict-close o una observación IFVG revisada, y disponibilidad acreditada. `CanonicalAdmissionPermitted=false` y `OperationalSignal=false`.

## Reproducción y causalidad

Desde la raíz, construir `MoneyWay.sln`, usar la [guía de comandos y el CSV fijado](nasdaq-m5-fvg-ifvg-candidate-v1.md#run-the-exposed-june-23-development-slice) y ejecutar `nasdaq-session-liquidity` con el [contexto Development](../../src/frontend/moneyway-web/src/research/context/data/june23-context.json), una salida nueva por ejecución y `--cutoff-utc`. Su SHA-256 es `3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`. La [guía A/B/C/D](m5-inclusive-confirmation-v2.md) reproduce V1/V2 sobre ascendente, descendente y el fixture sintético.

| Modo a 16:30 UTC | SHA-256 de dos salidas idénticas |
| --- | --- |
| `--compare-context-sequence` | `43ad4a0c30fcfe927331c916162cebd2de8ee1fa3c51378e5c10e621752c6e91` |
| `--compare-m5-to-m1-optional-fvg-v2` | `2f9687c2dc812f5623b3085d5b012d32c71b5e621c534f12911379c7c4bea345` |
| `--compare-end-to-end` | `d19c506bbce9609a8d3237e9f21b527d4d0150a1ed0339dc3a86f04735b8cb61` |
| `m5-fvg-research`, ambos perfiles | `559938154cd48b1eec87b9859ebdc34adbffd1c22bf5685ea4b6e92933ac9572` |

Al repetir con corte 14:00, la proyección de la corrida larga conservó 39 identidades de confirmación con evidencia exacta, 128 primeros resultados de episodio/configuración incluyendo empates, y 16 zonas por perfil con su historial hasta el corte. Para reproducir el cotejo, indexar `confirmationEvidence` por ID filtrando `confirmedAtUtc <= corte`, y `results` por episodio/configuración, conservando `(firstEligibleUtc, firstEligibleIds)` hasta el mismo corte. En el informe FVG, comparar la formación y la lista de observaciones filtrada por `observedAtUtc <= corte`. Las secuencias del informe contextual y del integrado coincidieron en ambos cortes. Los estados posteriores al corte pertenecen a snapshots distintos.

Pasaron build, 200 pruebas focalizadas de Application y 42 de integración. El trabajo no usó Validation ni HeldOut.

**Siguiente paso.** Obtener un episodio Development independiente con H4 y take alineado, ancla M5 y orden revisados por un experto externo o material original accesible, y disponibilidad de las fuentes acreditada. Congelar esas referencias antes de comparar SC e IFVG con el mismo corte y la misma política de sesión.
