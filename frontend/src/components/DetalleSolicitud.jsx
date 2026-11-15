import { useState } from "react";
import FormularioResolucion from "./FormularioResolucion";

export default function DetalleSolicitud({
  solicitud,
  onVolver,
  onGuardar,
  onResolucionRegistrada,
  cargando
}) {
  // Guardamos las decisiones y observaciones en el estado
  const [decisiones, setDecisiones] = useState({});
  const [observaciones, setObservaciones] = useState({});

  if (!solicitud) return null;
  const asignaturas = solicitud.asignaturas || [];
  const informeEmitido = solicitud.estado === "INFORME_EMITIDO";

  const handleDecisionChange = (id, valor) => {
    setDecisiones(prev => ({ ...prev, [id]: valor }));
  };

  const handleObservacionChange = (id, valor) => {
    setObservaciones(prev => ({ ...prev, [id]: valor }));
  };

  const handleGuardar = () => {
    // Armamos el objeto EXACTO que espera el backend (con mayúsculas)
    const informe = {
      asignaturas: asignaturas.map(asig => ({
        asignaturaId: asig.id,
        decision: decisiones[asig.id] || asig.propuesta?.decision || "HOMOLOGABLE",
        observacion: observaciones[asig.id] || "Sin observaciones"
      }))
    };

    // Llamamos a la función del componente padre (PanelCoordinador)
    onGuardar(informe);
  };

  return (
    <div className="card shadow-sm border-0 mt-3">
      <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
        <h5 className="mb-0">Detalle de la Solicitud SOL-{String(solicitud.solicitudId).padStart(4, "0")}</h5>
        <button className="btn btn-sm btn-outline-light" onClick={onVolver}>
          Volver a la lista
        </button>
      </div>
      <div className="card-body">
        <p><strong>Estudiante:</strong> {solicitud.estudiante}</p>
        <p><strong>Carrera Origen:</strong> {solicitud.carreraOrigen}</p>
        <p><strong>Carrera Destino:</strong> {solicitud.carreraDestino}</p>
        <p>
          <strong>Estado del análisis:</strong>{" "}
          <span className={`badge ${informeEmitido ? "bg-success" : "bg-warning text-dark"}`}>
            {solicitud.estado}
          </span>
        </p>
        {informeEmitido && solicitud.fechaInforme && (
          <p className="small text-muted">
            Informe emitido por {solicitud.coordinadorCorreo} el{" "}
            {new Date(solicitud.fechaInforme).toLocaleString()}
          </p>
        )}

        <h6 className="mt-4 fw-bold">Asignaturas para análisis:</h6>
        {asignaturas.length === 0 ? (
          <p className="text-muted">No hay asignaturas registradas en este análisis.</p>
        ) : (
          <div className="table-responsive">
            <table className="table table-bordered align-middle">
              <thead className="table-light">
                <tr>
                  <th>Materia de origen</th>
                  <th>Asignatura de destino</th>
                  <th>Propuesta del agente</th>
                  <th>Decisión Coordinador</th>
                  <th>Observación</th>
                </tr>
              </thead>
              <tbody>
                {asignaturas.map((asig) => (
                  <tr key={asig.id}>
                    <td>
                      {asig.materiasOrigen && asig.materiasOrigen.length > 0 ? (
                        <ul className="list-unstyled mb-0">
                          {asig.materiasOrigen.map((m, i) => (
                            <li key={i}>
                              {m.nombre}
                              {m.nota != null && <span className="text-muted small"> (nota {m.nota})</span>}
                            </li>
                          ))}
                        </ul>
                      ) : (
                        asig.asignaturaOrigen
                      )}
                      {asig.combinada && (
                        <span className="badge bg-primary ms-2">Combinada</span>
                      )}
                      {asig.reglaAplicada && (
                        <div className="small text-muted">Regla: {asig.reglaAplicada}</div>
                      )}
                    </td>
                    <td>{asig.asignaturaDestino || "—"}</td>
                    <td>
                      <span className="badge bg-info text-dark">
                        {asig.propuesta?.decision || "Pendiente"}
                      </span>
                      {asig.propuesta && (
                        <div className="small text-muted mt-1">
                          C {asig.propuesta.porcentajeContenido}% · P {asig.propuesta.porcentajeProfundidad}% · H {asig.propuesta.porcentajeHoras}%
                        </div>
                      )}
                    </td>
                    <td>
                      {informeEmitido ? (
                        <span className="badge bg-secondary">
                          {asig.decisionCoordinador || "—"}
                        </span>
                      ) : (
                        <select
                          className="form-select form-select-sm"
                          value={decisiones[asig.id] || asig.propuesta?.decision || "HOMOLOGABLE"}
                          onChange={(e) => handleDecisionChange(asig.id, e.target.value)}
                        >
                          <option value="HOMOLOGABLE">HOMOLOGABLE</option>
                          <option value="NO_HOMOLOGABLE">NO HOMOLOGABLE</option>
                        </select>
                      )}
                    </td>
                    <td>
                      {informeEmitido ? (
                        <span className="small">{asig.observacionCoordinador || "—"}</span>
                      ) : (
                        <input
                          type="text"
                          className="form-control form-control-sm"
                          placeholder="Observación..."
                          value={observaciones[asig.id] || ""}
                          onChange={(e) => handleObservacionChange(asig.id, e.target.value)}
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!informeEmitido && (
          <div className="text-end mt-3">
            <button
              className="btn btn-success fw-bold"
              onClick={handleGuardar}
              disabled={cargando}
            >
              {cargando ? "Emitiendo..." : "Emitir Informe Técnico"}
            </button>
          </div>
        )}

        {informeEmitido && (
          <FormularioResolucion
            solicitud={solicitud}
            onRegistrada={onResolucionRegistrada}
          />
        )}
      </div>
    </div>
  );
}
