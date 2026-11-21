import { useState } from "react";

export default function FormularioResolucion({ solicitud, onGuardar, cargando }) {
  const [numeroResolucion, setNumeroResolucion] = useState("");
  const [fecha, setFecha] = useState("");
  const [observacion, setObservacion] = useState("");
  
  // Inicializamos las decisiones con los datos de la solicitud
  const [decisiones, setDecisiones] = useState(
    solicitud.asignaturas?.map((a) => ({
      asignaturaId: a.asignaturaId,
      nombre: a.nombre,
      decisionFinal: a.decisionFinal || "HOMOLOGABLE",
    })) || []
  );
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  const handleDecisionChange = (id, valor) => {
    setDecisiones((prev) =>
      prev.map((d) => (d.asignaturaId === id ? { ...d, decisionFinal: valor } : d))
    );
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(""); setExito("");

    if (!numeroResolucion || !fecha) {
      setError("Debe ingresar el número de resolución y la fecha.");
      return;
    }

    const payload = {
      numeroResolucion,
      fecha,
      decisiones: decisiones.map(({ asignaturaId, decisionFinal }) => ({ asignaturaId, decisionFinal })),
      observacion,
    };

    try {
      await onGuardar(payload);
      setExito("Resolución registrada correctamente.");
    } catch (err) {
      setError(err.message || "Error al registrar la resolución.");
    }
  };

  return (
    <div className="card shadow-sm mt-4 border-primary">
      <div className="card-header bg-primary text-white">
        <h5 className="mb-0">Registrar Resolución del Consejo</h5>
      </div>
      <div className="card-body">
        {error && <div className="alert alert-danger py-2">{error}</div>}
        {exito && <div className="alert alert-success py-2">{exito}</div>}

        <form onSubmit={handleSubmit}>
          <div className="row mb-3">
            <div className="col-md-6">
              <label className="form-label fw-bold">Número de Resolución</label>
              <input
                type="text"
                className="form-control"
                placeholder="Ej. RES-2026-001"
                value={numeroResolucion}
                onChange={(e) => setNumeroResolucion(e.target.value)}
              />
            </div>
            <div className="col-md-6">
              <label className="form-label fw-bold">Fecha</label>
              <input
                type="date"
                className="form-control"
                value={fecha}
                onChange={(e) => setFecha(e.target.value)}
              />
            </div>
          </div>

          <h6 className="fw-bold mt-4 mb-2">Decisión Final por Asignatura</h6>
          <div className="table-responsive mb-3">
            <table className="table table-sm table-bordered">
              <thead className="table-light">
                <tr>
                  <th>Asignatura</th>
                  <th style={{ width: "200px" }}>Decisión Final</th>
                </tr>
              </thead>
              <tbody>
                {decisiones.map((d) => {
                  // LÓGICA DE COMBINACIÓN
                  const infoAsignatura = solicitud.asignaturas?.find(a => a.asignaturaId === d.asignaturaId);
                  const esCombinada = infoAsignatura?.combinada;
                  const materiasOrigen = infoAsignatura?.materiasOrigen || [];

                  return (
                    <tr key={d.asignaturaId}>
                      <td>
                        {esCombinada ? (
                          <div>
                            <span className="badge bg-info text-dark mb-1">Combinada</span>
                            <ul className="mb-0 ps-3">
                              {materiasOrigen.map((m, i) => (
                                <li key={i}><small>{m.nombre} (Nota: {m.nota})</small></li>
                              ))}
                            </ul>
                          </div>
                        ) : (
                          d.nombre
                        )}
                      </td>
                      <td>
                        <select
                          className="form-select form-select-sm"
                          value={d.decisionFinal}
                          onChange={(e) => handleDecisionChange(d.asignaturaId, e.target.value)}
                        >
                          <option value="HOMOLOGABLE">Homologable</option>
                          <option value="NO_HOMOLOGABLE">No Homologable</option>
                        </select>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="mb-3">
            <label className="form-label fw-bold">Observación</label>
            <textarea
              className="form-control"
              rows="2"
              placeholder="Observaciones adicionales..."
              value={observacion}
              onChange={(e) => setObservacion(e.target.value)}
            ></textarea>
          </div>

          <div className="text-end">
            <button type="submit" className="btn btn-primary px-4" disabled={cargando}>
              {cargando ? "Guardando..." : "Registrar Resolución"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}