import { useState } from "react";
import { registrarResolucion } from "../services/catalogoService";

/**
 * Formulario para registrar la resolución del Consejo Directivo sobre una solicitud
 * con informe técnico emitido (HU-17). Lo usa el Panel del Coordinador.
 */
export default function FormularioResolucion({ solicitud, onRegistrada }) {
  const asignaturas = solicitud?.asignaturas ?? [];

  const [numeroResolucion, setNumeroResolucion] = useState("");
  const [fecha, setFecha] = useState(() => new Date().toISOString().slice(0, 10));
  const [observacion, setObservacion] = useState("");
  const [decisiones, setDecisiones] = useState(() =>
    Object.fromEntries(
      asignaturas.map((a) => [a.id, a.decisionCoordinador || "HOMOLOGABLE"])
    )
  );
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");
  const [cargando, setCargando] = useState(false);

  const cambiarDecision = (id, valor) =>
    setDecisiones((prev) => ({ ...prev, [id]: valor }));

  async function guardar(ev) {
    ev.preventDefault();
    setError("");
    setExito("");

    if (!numeroResolucion.trim()) return setError("Ingrese el número de resolución.");
    if (asignaturas.length === 0) return setError("La solicitud no tiene asignaturas analizadas.");

    setCargando(true);
    try {
      await registrarResolucion(solicitud.solicitudId, {
        numeroResolucion: numeroResolucion.trim(),
        fecha: fecha ? new Date(`${fecha}T00:00:00`).toISOString() : new Date().toISOString(),
        observacion: observacion.trim() || null,
        decisiones: asignaturas.map((a) => ({
          asignaturaId: a.id,
          decisionFinal: decisiones[a.id] || "HOMOLOGABLE",
        })),
      });
      setExito("Resolución registrada. La solicitud pasó a estado RESUELTA.");
      if (onRegistrada) onRegistrada();
    } catch (err) {
      setError(err.message || "No se pudo registrar la resolución.");
    } finally {
      setCargando(false);
    }
  }

  return (
    <div className="card shadow-sm border mt-4">
      <div className="card-header bg-dark text-white">
        <h5 className="mb-0">Resolución del Consejo Directivo</h5>
      </div>
      <div className="card-body">
        {error && <div className="alert alert-danger py-2">{error}</div>}
        {exito && <div className="alert alert-success py-2">{exito}</div>}

        <form onSubmit={guardar}>
          <div className="row mb-3">
            <div className="col-md-6">
              <label className="form-label fw-bold">Número de resolución</label>
              <input
                className="form-control"
                value={numeroResolucion}
                onChange={(e) => setNumeroResolucion(e.target.value)}
                placeholder="RES-2026-001"
                required
              />
            </div>
            <div className="col-md-6">
              <label className="form-label fw-bold">Fecha</label>
              <input
                type="date"
                className="form-control"
                value={fecha}
                onChange={(e) => setFecha(e.target.value)}
                required
              />
            </div>
          </div>

          <div className="table-responsive mb-3">
            <table className="table table-sm table-bordered align-middle">
              <thead className="table-light">
                <tr>
                  <th>Asignatura de destino</th>
                  <th>Propuesta del agente</th>
                  <th style={{ width: "200px" }}>Decisión final</th>
                </tr>
              </thead>
              <tbody>
                {asignaturas.map((a) => (
                  <tr key={a.id}>
                    <td>{a.asignaturaDestino || a.asignaturaOrigen}</td>
                    <td>
                      <span className="badge bg-info text-dark">
                        {a.propuesta?.decision || "Pendiente"}
                      </span>
                    </td>
                    <td>
                      <select
                        className="form-select form-select-sm"
                        value={decisiones[a.id] || "HOMOLOGABLE"}
                        onChange={(e) => cambiarDecision(a.id, e.target.value)}
                      >
                        <option value="HOMOLOGABLE">HOMOLOGABLE</option>
                        <option value="NO_HOMOLOGABLE">NO HOMOLOGABLE</option>
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mb-3">
            <label className="form-label fw-bold">Observación</label>
            <textarea
              className="form-control"
              rows={2}
              value={observacion}
              onChange={(e) => setObservacion(e.target.value)}
              placeholder="Observación del Consejo Directivo (opcional)"
            />
          </div>

          <div className="text-end">
            <button className="btn btn-success fw-bold" disabled={cargando}>
              {cargando ? "Registrando..." : "Registrar resolución"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
