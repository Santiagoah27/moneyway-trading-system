export interface ReportedCost {
    category: string;
    sourceReference: string;
    amount: string | null;
    currency: string | null;
    originalSignMeaning: string | null;
}
export interface ReportedMonetaryOutcome {
    executionRecordId: string;
    executionEvidenceRevision: number;
    executionEvidenceRevisionHash: string;
    sourceReference: string;
    reportedBy: string;
    provenance: string;
    sourceSymbol: string | null;
    amount: string | null;
    currency: string | null;
    amountMeaning: string;
    basis: string;
    originalMeaningStatement: string | null;
    narrative: string | null;
    sourceEventDescription: string | null;
    association: string;
    associationSourceReference: string | null;
    dealReference: string | null;
    positionReference: string | null;
    originalTime: string | null;
    clockDomain: string | null;
    timePrecision: string;
    costs: ReportedCost[] | null;
}
export function ReportedMonetaryOutcomeFields({
    value,
    onChange,
}: {
    value: ReportedMonetaryOutcome;
    onChange: (v: ReportedMonetaryOutcome) => void;
}) {
    const optional = (s: string) => s.trim() || null;
    function input(
        label: string,
        field: keyof ReportedMonetaryOutcome,
        required = false,
    ) {
        return (
            <label>
                Resultado: {label}
                <input
                    required={required}
                    maxLength={2048}
                    value={String(value[field] ?? "")}
                    onChange={(e) =>
                        onChange({
                            ...value,
                            [field]: required
                                ? e.target.value
                                : optional(e.target.value),
                        })
                    }
                />
            </label>
        );
    }
    return (
        <fieldset>
            <legend>Resultado monetario reportado</legend>
            <p>
                Vinculado al registro {value.executionRecordId}, revisión de
                evidencia {value.executionEvidenceRevision}, hash{" "}
                {value.executionEvidenceRevisionHash}.
            </p>
            <p>
                Resultado reportado ≠ P&amp;L calculado por MoneyWay. Resultado
                reportado ≠ resultado verificado por el broker. Un importe no
                completa la operación ni cierra la sesión.
            </p>
            <div className="capture-grid">
                {input("Fuente (note: o https://)", "sourceReference", true)}
                {input("Reportado por", "reportedBy", true)}
                <label>
                    Resultado: procedencia
                    <select
                        value={value.provenance}
                        onChange={(e) =>
                            onChange({ ...value, provenance: e.target.value })
                        }
                    >
                        <option value="HumanReported">
                            Reportado por humano
                        </option>
                        <option value="DocumentReported">
                            Reportado según documento; sin inspección
                            independiente
                        </option>
                    </select>
                </label>
                <label>
                    Resultado: instrumento original
                    <select
                        value={value.sourceSymbol ?? ""}
                        onChange={(e) =>
                            onChange({
                                ...value,
                                sourceSymbol: optional(e.target.value),
                            })
                        }
                    >
                        <option value="">No establecido</option>
                        <option>USTEC</option>
                        <option>US100</option>
                    </select>
                </label>
                {input("Importe original (opcional, decimal exacto)", "amount")}
                {input("Moneda original (opcional)", "currency")}
                <label>
                    Resultado: significado del importe
                    <select
                        value={value.amountMeaning}
                        onChange={(e) =>
                            onChange({
                                ...value,
                                amountMeaning: e.target.value,
                            })
                        }
                    >
                        <option value="Unspecified">
                            No establecido; no inferir ganancia/pérdida
                        </option>
                        <option value="SignedResult">
                            Resultado con signo según la fuente
                        </option>
                        <option value="ProfitMagnitude">
                            Magnitud de ganancia declarada
                        </option>
                        <option value="LossMagnitude">
                            Magnitud de pérdida declarada
                        </option>
                    </select>
                </label>
                <label>
                    Resultado: bruto/neto
                    <select
                        value={value.basis}
                        onChange={(e) =>
                            onChange({ ...value, basis: e.target.value })
                        }
                    >
                        <option value="Unspecified">No establecido</option>
                        <option value="Gross">Bruto declarado</option>
                        <option value="Net">Neto declarado</option>
                    </select>
                </label>
                {input(
                    "Significado literal de la fuente",
                    "originalMeaningStatement",
                )}
                {input(
                    "Narrativa original, sin inferencia numérica",
                    "narrative",
                )}
                {input("Evento según fuente", "sourceEventDescription")}
                <label>
                    Resultado: vínculo con la operación
                    <select
                        value={value.association}
                        onChange={(e) =>
                            onChange({ ...value, association: e.target.value })
                        }
                    >
                        <option value="RecordOnly">
                            Solo asociación al registro MoneyWay
                        </option>
                        <option value="HumanReported">
                            Relación afirmada por humano
                        </option>
                        <option value="DocumentarySupported">
                            Relación afirmada con fuente documental
                        </option>
                    </select>
                </label>
                {input(
                    "Fuente del vínculo (note: o https://)",
                    "associationSourceReference",
                    value.association !== "RecordOnly",
                )}
                {input("Deal original, si consta", "dealReference")}
                {input("Posición original, si consta", "positionReference")}
                {input("Hora original", "originalTime")}
                {input("Dominio de reloj original", "clockDomain")}
                <label>
                    Resultado: precisión temporal
                    <select
                        value={value.timePrecision}
                        onChange={(e) =>
                            onChange({
                                ...value,
                                timePrecision: e.target.value,
                            })
                        }
                    >
                        <option value="Unknown">Desconocida</option>
                        <option value="Approximate">Aproximada</option>
                        <option value="SourceReportedSecond">
                            Segundos según fuente
                        </option>
                        <option value="ExactNativeRecord">
                            Exacta según registro declarado; sin verificar
                        </option>
                    </select>
                </label>
            </div>
            <p>
                Costos ausentes: desconocidos, nunca cero. Se conservan en sus
                monedas originales; no se suman ni convierten, y no se calcula
                un neto.
            </p>
            {(value.costs ?? []).map((cost, index) => (
                <fieldset key={index}>
                    <legend>Costo reportado {index + 1}</legend>
                    <div className="capture-grid">
                        {(
                            [
                                ["Categoría original", "category", true],
                                [
                                    "Fuente (note: o https://)",
                                    "sourceReference",
                                    true,
                                ],
                                ["Importe original", "amount", false],
                                ["Moneda original", "currency", false],
                                [
                                    "Significado original del signo",
                                    "originalSignMeaning",
                                    false,
                                ],
                            ] as const
                        ).map(([label, field, required]) => (
                            <label key={field}>
                                Costo {index + 1}: {label}
                                <input
                                    required={required}
                                    maxLength={2048}
                                    value={cost[field] ?? ""}
                                    onChange={(e) =>
                                        onChange({
                                            ...value,
                                            costs: value.costs!.map((c, i) =>
                                                i === index
                                                    ? {
                                                          ...c,
                                                          [field]: required
                                                              ? e.target.value
                                                              : optional(
                                                                    e.target
                                                                        .value,
                                                                ),
                                                      }
                                                    : c,
                                            ),
                                        })
                                    }
                                />
                            </label>
                        ))}
                    </div>
                    <button
                        type="button"
                        onClick={() =>
                            onChange({
                                ...value,
                                costs: value.costs!.filter(
                                    (_, i) => i !== index,
                                ),
                            })
                        }
                    >
                        Quitar costo {index + 1} de esta revisión
                    </button>
                </fieldset>
            ))}
            <button
                type="button"
                disabled={(value.costs?.length ?? 0) >= 16}
                onClick={() =>
                    onChange({
                        ...value,
                        costs: [
                            ...(value.costs ?? []),
                            {
                                category: "",
                                sourceReference: "",
                                amount: null,
                                currency: null,
                                originalSignMeaning: null,
                            },
                        ],
                    })
                }
            >
                Añadir costo reportado
            </button>
        </fieldset>
    );
}
