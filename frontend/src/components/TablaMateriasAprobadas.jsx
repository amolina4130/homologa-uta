/**
 * Tabla editable de las materias aprobadas extraídas del récord académico (HU-20).
 * El estudiante puede corregir nombre, nota, estado y periodo antes de guardar.
 */
export default function TablaMateriasAprobadas({ materias, onChange, editable = true }) {
  const filas = materias ?? [];

  const actualizar = (indice, campo, valor) => {
    if (!onChange) return;
    const copia = filas.map((m, i) =>
      i === indice ? { ...m, [campo]: valor } : m
    );
    onChange(copia);
  };

  const agregar = () => {
    if (!onChange) return;
    onChange([...filas, { nombre: "", nota: "", estadoSegunDocumento: "", periodo: "" }]);
  };

  const eliminar = (indice) => {
    if (!onChange) return;
    onChange(filas.filter((_, i) => i !== indice));
  };

  if (filas.length === 0) {
    return (
      <div className="alert alert-info mb-0">
        Todavía no hay materias aprobadas. Extraiga el récord académico o agréguelas manualmente.
        {editable && (
          <button type="button" className="btn btn-sm btn-outline-primary ms-3" onClick={agregar}>
            Agregar materia
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="table-responsive">
      <table className="table table-sm table-bordered align-middle mb-0">
        <thead className="table-light">
          <tr>
            <th style={{ minWidth: "220px" }}>Materia</th>
            <th style={{ width: "100px" }}>Nota</th>
            <th style={{ width: "140px" }}>Estado</th>
            <th style={{ width: "130px" }}>Periodo</th>
            {editable && <th style={{ width: "80px" }} className="text-center">Acciones</th>}
          </tr>
        </thead>
        <tbody>
          {filas.map((m, i) => (
            <tr key={m.id ?? `nueva-${i}`}>
              <td>
                {editable ? (
                  <input
                    className="form-control form-control-sm"
                    value={m.nombre ?? ""}
                    onChange={(e) => actualizar(i, "nombre", e.target.value)}
                    placeholder="Nombre de la materia"
                  />
                ) : (
                  m.nombre
                )}
              </td>
              <td>
                {editable ? (
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    max="20"
                    className="form-control form-control-sm"
                    value={m.nota ?? ""}
                    onChange={(e) => actualizar(i, "nota", e.target.value)}
                  />
                ) : (
                  m.nota ?? "—"
                )}
              </td>
              <td>
                {editable ? (
                  <input
                    className="form-control form-control-sm"
                    value={m.estadoSegunDocumento ?? ""}
                    onChange={(e) => actualizar(i, "estadoSegunDocumento", e.target.value)}
                    placeholder="APROBADO"
                  />
                ) : (
                  m.estadoSegunDocumento ?? "—"
                )}
              </td>
              <td>
                {editable ? (
                  <input
                    className="form-control form-control-sm"
                    value={m.periodo ?? ""}
                    onChange={(e) => actualizar(i, "periodo", e.target.value)}
                    placeholder="2024-1"
                  />
                ) : (
                  m.periodo ?? "—"
                )}
              </td>
              {editable && (
                <td className="text-center">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-danger"
                    onClick={() => eliminar(i)}
                  >
                    Quitar
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
      {editable && (
        <div className="text-end mt-2">
          <button type="button" className="btn btn-sm btn-outline-primary" onClick={agregar}>
            Agregar materia
          </button>
        </div>
      )}
    </div>
  );
}
