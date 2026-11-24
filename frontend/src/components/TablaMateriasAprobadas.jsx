import { useEffect, useState } from "react";

const normalizar = (materias) =>
  (materias ?? []).map((m) => ({
    nombre: m.nombre ?? "",
    nota: m.nota === null || m.nota === undefined ? "" : String(m.nota),
    estadoSegunDocumento: m.estadoSegunDocumento ?? "",
    periodo: m.periodo ?? "",
  }));

const filaVacia = () => ({
  nombre: "",
  nota: "",
  estadoSegunDocumento: "",
  periodo: "",
});

// HU-20: tabla editable con las materias aprobadas extraídas del récord.
export default function TablaMateriasAprobadas({
  materias,
  onGuardar,
  guardando,
  soloLectura,
  mensaje,
  error,
}) {
  const [filas, setFilas] = useState(() => normalizar(materias));
  const [errorLocal, setErrorLocal] = useState("");

  useEffect(() => {
    const timer = setTimeout(() => {
      setFilas(normalizar(materias));
      setErrorLocal("");
    }, 0);
    return () => clearTimeout(timer);
  }, [materias]);

  const cambiar = (indice, campo, valor) => {
    setFilas((prev) =>
      prev.map((fila, i) => (i === indice ? { ...fila, [campo]: valor } : fila))
    );
  };

  const agregar = () => {
    setFilas((prev) => [...prev, filaVacia()]);
  };

  const eliminar = (indice) => {
    setFilas((prev) => prev.filter((_, i) => i !== indice));
  };

  const guardar = () => {
    setErrorLocal("");

    if (filas.length === 0) {
      setErrorLocal("Debe registrar al menos una materia aprobada.");
      return;
    }

    for (const fila of filas) {
      if (!fila.nombre.trim()) {
        setErrorLocal("Todas las materias deben tener un nombre.");
        return;
      }

      if (fila.nota !== "") {
        const nota = Number(fila.nota);
        if (Number.isNaN(nota) || nota < 0 || nota > 20) {
          setErrorLocal("La nota debe estar entre 0 y 20.");
          return;
        }
      }
    }

    onGuardar(
      filas.map((fila) => ({
        nombre: fila.nombre.trim(),
        nota: fila.nota === "" ? null : Number(fila.nota),
        estadoSegunDocumento: fila.estadoSegunDocumento || null,
        periodo: fila.periodo || null,
      }))
    );
  };

  return (
    <div className="card shadow-sm border-0 mb-4">
      <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
        <h5 className="mb-0 fw-bold">Materias aprobadas del récord</h5>
        {!soloLectura && (
          <button type="button" className="btn btn-sm btn-outline-light" onClick={agregar}>
            Agregar materia
          </button>
        )}
      </div>

      <div className="card-body">
        {mensaje && <div className="alert alert-success">{mensaje}</div>}
        {error && <div className="alert alert-danger">{error}</div>}
        {errorLocal && <div className="alert alert-danger">{errorLocal}</div>}

        {filas.length === 0 ? (
          <p className="text-muted mb-0">
            Todavía no hay materias aprobadas. Extraiga el récord o agregue una materia
            manualmente.
          </p>
        ) : (
          <div className="table-responsive">
            <table className="table table-bordered align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th style={{ minWidth: "260px" }}>Materia</th>
                  <th style={{ width: "130px" }}>Nota</th>
                  <th>Estado según el documento</th>
                  <th style={{ width: "130px" }}>Período</th>
                  {!soloLectura && <th style={{ width: "100px" }}>Acción</th>}
                </tr>
              </thead>
              <tbody>
                {filas.map((fila, indice) => (
                  <tr key={indice}>
                    <td>
                      {soloLectura ? (
                        fila.nombre
                      ) : (
                        <input
                          type="text"
                          className="form-control form-control-sm"
                          maxLength={200}
                          value={fila.nombre}
                          onChange={(e) => cambiar(indice, "nombre", e.target.value)}
                          disabled={guardando}
                        />
                      )}
                    </td>
                    <td>
                      {soloLectura ? (
                        fila.nota
                      ) : (
                        <input
                          type="number"
                          className="form-control form-control-sm"
                          min="0"
                          max="20"
                          step="0.01"
                          value={fila.nota}
                          onChange={(e) => cambiar(indice, "nota", e.target.value)}
                          disabled={guardando}
                        />
                      )}
                    </td>
                    <td>{fila.estadoSegunDocumento || "-"}</td>
                    <td>{fila.periodo || "-"}</td>
                    {!soloLectura && (
                      <td className="text-center">
                        <button
                          type="button"
                          className="btn btn-sm btn-outline-danger"
                          onClick={() => eliminar(indice)}
                          disabled={guardando}
                        >
                          Eliminar
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!soloLectura && (
          <div className="text-end mt-3">
            <button
              type="button"
              className="btn btn-primary fw-bold"
              onClick={guardar}
              disabled={guardando}
            >
              {guardando ? "Guardando..." : "Guardar materias aprobadas"}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
