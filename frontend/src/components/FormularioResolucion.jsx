import { useState } from "react";

const DECISIONES_VALIDAS = ["HOMOLOGABLE", "NO_HOMOLOGABLE"];

const hoy = () => new Date().toISOString().slice(0, 10);

const decisionesIniciales = (solicitud) => {
  const resultado = {};
  for (const asig of solicitud?.asignaturas ?? []) {
    resultado[asig.id] = DECISIONES_VALIDAS.includes(asig.decisionFinal)
      ? asig.decisionFinal
      : "";
  }
  return resultado;
};

const textoMaterias = (asig) => {
  if (asig.combinada && (asig.materiasOrigen?.length ?? 0) > 0) {
    return asig.materiasOrigen.map((m) => m.nombre).join(" + ");
  }
  return asig.asignaturaOrigen || "-";
};

// HU-17 / HU-22: formulario para que el Coordinador registre la resolución del
// Consejo Directivo, con la decisión final de cada asignatura y las materias de
// origen cuando la asignatura se cubre de forma combinada.
export default function FormularioResolucion({ solicitud, onRegistrar, cargando }) {
  const [numeroResolucion, setNumeroResolucion] = useState("");
  const [fecha, setFecha] = useState(hoy);
  const [observacion, setObservacion] = useState("");
  const [decisiones, setDecisiones] = useState(() => decisionesIniciales(solicitud));
  const [errorLocal, setErrorLocal] = useState("");

  if (!solicitud) return null;

  const asignaturas = solicitud.asignaturas || [];

  const cambiarDecision = (id, valor) => {
    setDecisiones((prev) => ({ ...prev, [id]: valor }));
  };

  const registrar = (ev) => {
    ev.preventDefault();
    setErrorLocal("");

    if (!numeroResolucion.trim()) {
      setErrorLocal("Ingrese el número de la resolución.");
      return;
    }
    if (!fecha) {
      setErrorLocal("Ingrese la fecha de la resolución.");
      return;
    }
    if (asignaturas.length === 0) {
      setErrorLocal("La solicitud no tiene asignaturas para resolver.");
      return;
    }

    const faltantes = asignaturas.filter(
      (asig) => !DECISIONES_VALIDAS.includes(decisiones[asig.id])
    );
    if (faltantes.length > 0) {
      setErrorLocal("Debe seleccionar la decisión final de cada asignatura.");
      return;
    }

    onRegistrar({
      numeroResolucion: numeroResolucion.trim(),
      fecha: new Date(`${fecha}T00:00:00`).toISOString(),
      decisiones: asignaturas.map((asig) => ({
        asignaturaId: asig.id,
        decisionFinal: decisiones[asig.id],
      })),
      observacion: observacion.trim() || null,
    });
  };

  return (
    <div className="card shadow-sm border-0 mt-4">
      <div className="card-header bg-dark text-white">
        <h5 className="mb-0 fw-bold">Resolución del Consejo Directivo</h5>
      </div>
      <div className="card-body">
        <div className="alert alert-info">
          La resolución deja constancia en el sistema. No reemplaza el trámite oficial
          del Consejo Directivo.
        </div>

        <form onSubmit={registrar} noValidate>
          <div className="row mb-3">
            <div className="col-md-6">
              <label htmlFor="numeroResolucion" className="form-label fw-bold">
                Número de resolución *
              </label>
              <input
                id="numeroResolucion"
                type="text"
                className="form-control"
                maxLength={100}
                placeholder="Ej. 045-2026-CONSEJO"
                value={numeroResolucion}
                onChange={(e) => setNumeroResolucion(e.target.value)}
                disabled={cargando}
              />
            </div>
            <div className="col-md-6">
              <label htmlFor="fechaResolucion" className="form-label fw-bold">
                Fecha *
              </label>
              <input
                id="fechaResolucion"
                type="date"
                className="form-control"
                value={fecha}
                onChange={(e) => setFecha(e.target.value)}
                disabled={cargando}
              />
            </div>
          </div>

          <h6 className="fw-bold mt-4">Decisión final por asignatura *</h6>

          {asignaturas.length === 0 ? (
            <p className="text-muted">No hay asignaturas en este informe.</p>
          ) : (
            <div className="table-responsive">
              <table className="table table-bordered align-middle">
                <thead className="table-light">
                  <tr>
                    <th>Asignatura de destino</th>
                    <th>Materias de origen</th>
                    <th style={{ minWidth: "180px" }}>Decisión final</th>
                  </tr>
                </thead>
                <tbody>
                  {asignaturas.map((asig) => (
                    <tr key={asig.id}>
                      <td>
                        <div className="fw-bold">{asig.asignaturaDestino}</div>
                        {asig.combinada && (
                          <span className="badge bg-info text-dark mt-1">Combinada</span>
                        )}
                      </td>
                      <td>
                        <div>{textoMaterias(asig)}</div>
                        {(asig.materiasOrigen?.length ?? 0) > 0 && (
                          <ul className="list-unstyled small text-muted mb-0 mt-1">
                            {asig.materiasOrigen.map((m, indice) => (
                              <li key={`${asig.id}-${indice}`}>
                                {m.nombre}
                                {m.nota !== null && m.nota !== undefined
                                  ? ` · Nota: ${m.nota}`
                                  : ""}
                              </li>
                            ))}
                          </ul>
                        )}
                      </td>
                      <td>
                        <select
                          className="form-select form-select-sm"
                          value={decisiones[asig.id] || ""}
                          onChange={(e) => cambiarDecision(asig.id, e.target.value)}
                          disabled={cargando}
                        >
                          <option value="">Seleccione...</option>
                          <option value="HOMOLOGABLE">HOMOLOGABLE</option>
                          <option value="NO_HOMOLOGABLE">NO HOMOLOGABLE</option>
                        </select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="mb-3">
            <label htmlFor="observacionResolucion" className="form-label fw-bold">
              Observación
            </label>
            <textarea
              id="observacionResolucion"
              className="form-control"
              rows={3}
              maxLength={2000}
              placeholder="Observaciones del Consejo (opcional)"
              value={observacion}
              onChange={(e) => setObservacion(e.target.value)}
              disabled={cargando}
            />
          </div>

          {errorLocal && <div className="alert alert-danger">{errorLocal}</div>}

          <div className="text-end">
            <button
              type="submit"
              className="btn btn-success fw-bold"
              disabled={cargando || asignaturas.length === 0}
            >
              {cargando ? "Registrando..." : "Registrar resolución"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
