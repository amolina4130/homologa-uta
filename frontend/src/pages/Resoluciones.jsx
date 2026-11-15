import { useState } from "react";
import { obtenerResultado, obtenerResolucion } from "../services/catalogoService";

/**
 * Consulta de la resolución registrada por el Consejo Directivo (HU-17, vista de lectura).
 */
export default function Resoluciones() {
  const [solicitudId, setSolicitudId] = useState("");
  const [resultado, setResultado] = useState(null);
  const [resolucion, setResolucion] = useState(null);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const consultar = async (ev) => {
    ev.preventDefault();
    setError("");
    setResultado(null);
    setResolucion(null);

    if (!solicitudId) return setError("Ingrese el número de la solicitud.");

    setCargando(true);
    try {
      const res = await obtenerResultado(solicitudId);
      setResultado(res);
      try {
        setResolucion(await obtenerResolucion(solicitudId));
      } catch {
        setResolucion(null);
      }
    } catch (err) {
      setError(err.message || "No se pudo consultar la solicitud.");
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className="container mt-4">
      <h3 className="fw-bold mb-3">Resoluciones del Consejo Directivo</h3>

      <div className="card shadow-sm border-0 mb-4">
        <div className="card-body">
          <form onSubmit={consultar} className="row g-2 align-items-end">
            <div className="col-md-4">
              <label className="form-label fw-bold">Número de solicitud</label>
              <input
                type="number"
                min="1"
                className="form-control"
                value={solicitudId}
                onChange={(e) => setSolicitudId(e.target.value)}
                placeholder="Ej. 3"
                required
              />
            </div>
            <div className="col-md-3">
              <button className="btn btn-primary fw-bold" disabled={cargando}>
                {cargando ? "Consultando..." : "Consultar"}
              </button>
            </div>
          </form>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {resultado && (
        <div className="card shadow-sm border-0 mb-4">
          <div className="card-header bg-primary text-white fw-bold">
            Solicitud SOL-{String(resultado.solicitudId).padStart(4, "0")}
          </div>
          <div className="card-body">
            <p><strong>Estado del análisis:</strong> {resultado.estado}</p>
            <p><strong>Fecha del análisis:</strong> {resultado.fechaAnalisis ? new Date(resultado.fechaAnalisis).toLocaleString() : "—"}</p>

            <div className="table-responsive">
              <table className="table table-sm table-bordered align-middle mb-0">
                <thead className="table-light">
                  <tr>
                    <th>Materia de origen</th>
                    <th>Asignatura de destino</th>
                    <th>Decisión del agente</th>
                  </tr>
                </thead>
                <tbody>
                  {(resultado.asignaturas || []).map((a, i) => (
                    <tr key={i}>
                      <td>
                        {a.materiasOrigen && a.materiasOrigen.length > 0
                          ? a.materiasOrigen.map((m) => m.nombre).join(" + ")
                          : a.asignaturaOrigen}
                      </td>
                      <td>{a.asignaturaDestino || "—"}</td>
                      <td><span className="badge bg-info text-dark">{a.decision}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {resultado && (
        resolucion ? (
          <div className="card shadow-sm border-0 mb-4">
            <div className="card-header bg-dark text-white fw-bold">Resolución registrada</div>
            <div className="card-body">
              <p><strong>Número:</strong> {resolucion.numeroResolucion}</p>
              <p><strong>Fecha:</strong> {resolucion.fecha ? new Date(resolucion.fecha).toLocaleDateString() : "—"}</p>
              {resolucion.observacion && <p><strong>Observación:</strong> {resolucion.observacion}</p>}
              <ul className="mb-0">
                {(resolucion.decisiones || []).map((d) => (
                  <li key={d.asignaturaId}>
                    Asignatura {d.asignaturaId}: <strong>{d.decisionFinal}</strong>
                  </li>
                ))}
              </ul>
            </div>
          </div>
        ) : (
          <div className="alert alert-info">
            Esta solicitud todavía no tiene una resolución registrada.
          </div>
        )
      )}
    </div>
  );
}
