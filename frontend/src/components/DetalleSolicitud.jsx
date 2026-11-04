export default function DetalleSolicitudComponente({
  solicitud,
  decisiones,
  nuevosResultados,
  observaciones,
  onDecisionChange,
  onResultadoChange,
  onObservacionChange,
  onVolver,
  onGuardar,
  cargando,
  todasDecididas,
}) {
  return (
    <div className="card shadow-sm border-0">
      <div className="card-header bg-secondary text-white d-flex justify-content-between align-items-center">
        <h5 className="mb-0">
          Detalle SOL-{solicitud.numero || solicitud.id} — {solicitud.estudiante}
        </h5>
        <button className="btn btn-sm btn-light" onClick={onVolver}>Volver</button>
      </div>
      <div className="card-body">
        <table className="table table-bordered align-middle">
          <thead className="table-light">
            <tr>
              <th>Asignatura</th>
              <th>Propuesta del agente</th>
              <th>Justificación</th>
              <th>Decisión final</th>
              <th>Nuevo Resultado</th>
              <th>Observación</th>
            </tr>
          </thead>
          <tbody>
            {solicitud.asignaturas.map((asig) => (
              <tr key={asig.id}>
                <td>{asig.nombre}</td>
                <td>
                  <span className={`badge ${asig.propuestaAgente ? "bg-success" : "bg-danger"}`}>
                    {asig.propuestaAgente ? "Homologable" : "No homologable"}
                  </span>
                </td>
                <td><small>{asig.justificacion}</small></td>
                <td>
                  <select 
                    className="form-select form-select-sm"
                    value={decisiones[asig.id] || ""}
                    onChange={(e) => onDecisionChange(asig.id, e.target.value)}
                    aria-label={`Decisión para ${asig.nombre}`}
                  >
                    <option value="">Seleccione...</option>
                    <option value="ACEPTAR">Aceptar</option>
                    <option value="MODIFICAR">Modificar</option>
                  </select>
                </td>
                <td>
                  <select 
                    className="form-select form-select-sm"
                    value={nuevosResultados[asig.id] || ""}
                    onChange={(e) => onResultadoChange(asig.id, e.target.value)}
                    disabled={decisiones[asig.id] !== "MODIFICAR"}
                    aria-label={`Nuevo resultado para ${asig.nombre}`}
                  >
                    <option value="">N/A</option>
                    <option value="HOMOLOGABLE">Homologable</option>
                    <option value="NO_HOMOLOGABLE">No homologable</option>
                  </select>
                </td>
                <td>
                  <input 
                    type="text" 
                    className="form-control form-control-sm"
                    placeholder={decisiones[asig.id] === "MODIFICAR" ? "Obligatoria..." : "Opcional..."}
                    value={observaciones[asig.id] || ""}
                    onChange={(e) => onObservacionChange(asig.id, e.target.value)}
                    aria-label={`Observación para ${asig.nombre}`}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="text-end mt-3">
          <button className="btn btn-secondary me-2" onClick={onVolver}>Cancelar</button>
          <button 
            className="btn btn-primary" 
            onClick={onGuardar} 
            disabled={cargando || !todasDecididas}
          >
            {cargando ? "Emitiendo..." : "Emitir informe técnico"}
          </button>
        </div>
        {!todasDecididas && (
          <div className="text-end text-muted small mt-2">
            * Debe completar la decisión, el nuevo resultado y la observación (si modifica).
          </div>
        )}
      </div>
    </div>
  );
}