import { useState } from "react";

export default function DetalleSolicitud({ 
  solicitud, 
  onVolver, 
  onGuardar, 
  cargando 
}) {
  // Guardamos las decisiones y observaciones en el estado
  const [decisiones, setDecisiones] = useState({});
  const [observaciones, setObservaciones] = useState({});

  if (!solicitud) return null;
  const asignaturas = solicitud.asignaturas || [];

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
        decision: decisiones[asig.id] || "HOMOLOGABLE",
        observacion: observaciones[asig.id] || "Sin observaciones"
      }))
    };

    // Llamamos a la función del componente padre (PanelCoordinador)
    onGuardar(informe);
  };

  return (
    <div className="card shadow-sm border-0 mt-3">
      <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
        <h5 className="mb-0">Detalle de la Solicitud SOL-{solicitud.solicitudId}</h5>
        <button className="btn btn-sm btn-outline-light" onClick={onVolver}>
          Volver a la lista
        </button>
      </div>
      <div className="card-body">
        <p><strong>Estudiante:</strong> {solicitud.estudiante}</p>
        <p><strong>Carrera Origen:</strong> {solicitud.carreraOrigen}</p>
        <p><strong>Carrera Destino:</strong> {solicitud.carreraDestino}</p>
        
        <h6 className="mt-4 fw-bold">Asignaturas para análisis:</h6>
        {asignaturas.length === 0 ? (
          <p className="text-muted">No hay asignaturas registradas en este análisis.</p>
        ) : (
          <div className="table-responsive">
            <table className="table table-bordered">
              <thead className="table-light">
                <tr>
                  <th>Asignatura</th>
                  <th>Decisión del Agente</th>
                  <th>Decisión Coordinador</th>
                  <th>Observación</th>
                </tr>
              </thead>
              <tbody>
                {asignaturas.map((asig) => (
                  <tr key={asig.id}>
                    <td>
                      <strong>{asig.asignaturaOrigen}</strong> → {asig.asignaturaDestino}
                    </td>
                    <td>
                      <span className="badge bg-info text-dark">
                        {asig.propuesta?.decision || "Pendiente"}
                      </span>
                    </td>
                    <td>
                      <select 
                        className="form-select form-select-sm"
                        value={decisiones[asig.id] || "HOMOLOGABLE"}
                        onChange={(e) => handleDecisionChange(asig.id, e.target.value)}
                      >
                        <option value="HOMOLOGABLE">HOMOLOGABLE</option>
                        <option value="NO_HOMOLOGABLE">NO HOMOLOGABLE</option>
                      </select>
                    </td>
                    <td>
                      <input 
                        type="text" 
                        className="form-control form-control-sm" 
                        placeholder="Observación..."
                        value={observaciones[asig.id] || ""}
                        onChange={(e) => handleObservacionChange(asig.id, e.target.value)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="text-end mt-3">
          <button 
            className="btn btn-success fw-bold" 
            onClick={handleGuardar}
            disabled={cargando}
          >
            {cargando ? "Emitiendo..." : "Emitir Informe Técnico"}
          </button>
        </div>
      </div>
    </div>
  );
}