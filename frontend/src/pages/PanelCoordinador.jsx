import { useState, useEffect } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesPendientes, obtenerDetalleSolicitud, emitirInforme } from "../services/coordinadorService";

export default function PanelCoordinador() {
  const { rol } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudSeleccionada, setSolicitudSeleccionada] = useState(null);
  const [decisiones, setDecisiones] = useState({});
  const [observaciones, setObservaciones] = useState({});
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  useEffect(() => {
    obtenerSolicitudesPendientes()
      .then((data) => setSolicitudes(data.data || data))
      .catch(() => setError("No se pudieron cargar las solicitudes pendientes."));
  }, []);

  const abrirDetalle = async (id) => {
    setCargando(true);
    setError("");
    try {
      const detalle = await obtenerDetalleSolicitud(id);
      setSolicitudSeleccionada(detalle);
      
      const decIniciales = {};
      const obsIniciales = {};
      detalle.asignaturas.forEach((a) => {
        decIniciales[a.id] = a.propuestaAgente ? "ACEPTAR" : "MODIFICAR";
        obsIniciales[a.id] = "";
      });
      setDecisiones(decIniciales);
      setObservaciones(obsIniciales);
    } catch {
      setError("Error al cargar el detalle.");
    } finally {
      setCargando(false);
    }
  };

  const handleDecisionChange = (id, valor) => {
    setDecisiones((prev) => ({ ...prev, [id]: valor }));
  };

  const handleObservacionChange = (id, valor) => {
    setObservaciones((prev) => ({ ...prev, [id]: valor }));
  };

  const guardarInforme = async () => {
    if (!solicitudSeleccionada) return;
    setCargando(true);
    setError("");
    setExito("");

    const decisionesFormateadas = Object.keys(decisiones).map((id) => ({
      asignaturaId: parseInt(id),
      decision: decisiones[id],
      observacion: observaciones[id],
    }));

    try {
      await emitirInforme(solicitudSeleccionada.id, decisionesFormateadas);
      setExito("Informe técnico emitido correctamente.");
      setSolicitudSeleccionada(null);
      obtenerSolicitudesPendientes().then((data) => setSolicitudes(data.data || data));
    } catch {
      setError("Error al emitir el informe. Verifique la conexión.");
    } finally {
      setCargando(false);
    }
  };

  if (rol !== "COORDINADOR") {
    return (
      <div className="container mt-4">
        <div className="alert alert-danger">Acceso denegado. Solo el Coordinador puede ver esta pantalla.</div>
      </div>
    );
  }

  return (
    <div className="container mt-4">
      <h2 className="mb-4">PANEL DEL COORDINADOR</h2>

      {error && <div className="alert alert-danger">{error}</div>}
      {exito && <div className="alert alert-success">{exito}</div>}

      {!solicitudSeleccionada ? (
        <div className="card shadow-sm border-0">
          <div className="card-header bg-primary text-white">
            <h5 className="mb-0">Solicitudes pendientes de revisión</h5>
          </div>
          <div className="card-body p-0">
            <table className="table table-hover mb-0">
              <thead className="table-light">
                <tr>
                  <th>Fecha</th>
                  <th>Estudiante</th>
                  <th>Tipo</th>
                  <th>Carreras</th>
                  <th>Plazo</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {solicitudes.length === 0 ? (
                  <tr><td colSpan="6" className="text-center py-4">No hay solicitudes pendientes.</td></tr>
                ) : (
                  solicitudes.map((sol) => (
                    <tr key={sol.id}>
                      <td>{new Date(sol.fecha).toLocaleDateString()}</td>
                      <td>{sol.estudiante}</td>
                      <td>{sol.tipo}</td>
                      <td>{sol.origen} → {sol.destino}</td>
                      <td><span className="badge bg-warning text-dark">5 días</span></td>
                      <td>
                        <button className="btn btn-sm btn-outline-primary" onClick={() => abrirDetalle(sol.id)}>
                          Abrir
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <div className="card shadow-sm border-0">
          <div className="card-header bg-secondary text-white d-flex justify-content-between align-items-center">
            <h5 className="mb-0">Detalle SOL-{solicitudSeleccionada.numero} — {solicitudSeleccionada.estudiante}</h5>
            <button className="btn btn-sm btn-light" onClick={() => setSolicitudSeleccionada(null)}>Volver</button>
          </div>
          <div className="card-body">
            <table className="table table-bordered align-middle">
              <thead className="table-light">
                <tr>
                  <th>Asignatura</th>
                  <th>Propuesta del agente</th>
                  <th>Justificación</th>
                  <th>Decisión final</th>
                  <th>Observación</th>
                </tr>
              </thead>
              <tbody>
                {solicitudSeleccionada.asignaturas.map((asig) => (
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
                        value={decisiones[asig.id]}
                        onChange={(e) => handleDecisionChange(asig.id, e.target.value)}
                      >
                        <option value="ACEPTAR">Aceptar</option>
                        <option value="MODIFICAR">Modificar</option>
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

            <div className="text-end mt-3">
              <button className="btn btn-secondary me-2" onClick={() => setSolicitudSeleccionada(null)}>Cancelar</button>
              <button className="btn btn-primary" onClick={guardarInforme} disabled={cargando}>
                {cargando ? "Emitiendo..." : "Emitir informe técnico"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}