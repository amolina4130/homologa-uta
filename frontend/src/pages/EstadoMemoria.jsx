import { useEffect, useState } from "react";
import { obtenerCarreras } from "../services/solicitudesService";
import {
  obtenerEstadoMemoria,
  cargarMemoria,
  obtenerAsignaturasMemoria,
  cargarPlanesAnaliticos,
} from "../services/catalogoService";

/**
 * Administración del catálogo: memoria del agente (HU-19) y planes analíticos (HU-13).
 * Solo accesible para el rol Administrador.
 */
export default function EstadoMemoria() {
  const [estado, setEstado] = useState(null);
  const [sinMemoria, setSinMemoria] = useState(false);
  const [carreras, setCarreras] = useState([]);
  const [carreraId, setCarreraId] = useState("");
  const [nivelMaximo, setNivelMaximo] = useState("");
  const [asignaturas, setAsignaturas] = useState([]);
  const [archivos, setArchivos] = useState([]);
  const [resultadoPlanes, setResultadoPlanes] = useState(null);
  const [mensaje, setMensaje] = useState("");
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const cargarEstado = async () => {
    setError("");
    try {
      const data = await obtenerEstadoMemoria();
      setEstado(data);
      setSinMemoria(false);
    } catch (err) {
      if (err.status === 404) {
        setSinMemoria(true);
        setEstado(null);
      } else {
        setError(err.message || "No se pudo consultar la memoria del agente.");
      }
    }
  };

  useEffect(() => {
    obtenerCarreras()
      .then((data) => setCarreras(data.data || data))
      .catch(() => setError("No se pudieron cargar las carreras."));
    cargarEstado();
  }, []);

  const recargarMemoria = async () => {
    setCargando(true);
    setError("");
    setMensaje("");
    try {
      const data = await cargarMemoria();
      setMensaje(
        data.sinCambios
          ? "La memoria no cambió desde la última carga."
          : `Memoria cargada: ${data.cargadas} registros.`
      );
      await cargarEstado();
    } catch (err) {
      setError(err.message || "No se pudo cargar la memoria del agente.");
    } finally {
      setCargando(false);
    }
  };

  const consultarAsignaturas = async () => {
    if (!carreraId) return setError("Seleccione una carrera.");
    setCargando(true);
    setError("");
    try {
      const data = await obtenerAsignaturasMemoria(carreraId, nivelMaximo || undefined);
      setAsignaturas(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "No se pudieron consultar las asignaturas.");
    } finally {
      setCargando(false);
    }
  };

  const subirPlanes = async (ev) => {
    ev.preventDefault();
    setError("");
    setMensaje("");
    setResultadoPlanes(null);
    if (archivos.length === 0) return setError("Seleccione al menos un PDF.");

    setCargando(true);
    try {
      const data = await cargarPlanesAnaliticos(archivos);
      setResultadoPlanes(data);
      setArchivos([]);
      setMensaje(`Planes analíticos cargados: ${data.cargados}.`);
    } catch (err) {
      setError(err.message || "No se pudieron cargar los planes analíticos.");
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className="container mt-4">
      <h3 className="fw-bold mb-3">Memoria del agente y planes analíticos</h3>

      {error && <div className="alert alert-danger">{error}</div>}
      {mensaje && <div className="alert alert-success">{mensaje}</div>}

      <div className="card shadow-sm border-0 mb-4">
        <div className="card-header bg-primary text-white fw-bold">
          Memoria del agente (HU-19)
        </div>
        <div className="card-body">
          {sinMemoria ? (
            <p className="text-muted">La memoria del agente todavía no se ha cargado.</p>
          ) : estado ? (
            <div className="row mb-3">
              <div className="col-md-3"><strong>Huella:</strong><div className="text-break small">{estado.huella}</div></div>
              <div className="col-md-3"><strong>Fecha de carga:</strong><div>{estado.fechaCarga ? new Date(estado.fechaCarga).toLocaleString() : "—"}</div></div>
              <div className="col-md-3"><strong>Carreras:</strong><div>{estado.totalCarreras}</div></div>
              <div className="col-md-3"><strong>Asignaturas:</strong><div>{estado.totalAsignaturas}</div></div>
            </div>
          ) : (
            <p className="text-muted">Consultando...</p>
          )}

          <button className="btn btn-primary fw-bold" onClick={recargarMemoria} disabled={cargando}>
            {cargando ? "Procesando..." : "Cargar / actualizar memoria"}
          </button>

          <hr />

          <div className="row g-2 align-items-end">
            <div className="col-md-4">
              <label className="form-label fw-bold">Carrera</label>
              <select className="form-select" value={carreraId} onChange={(e) => setCarreraId(e.target.value)}>
                <option value="">Seleccione...</option>
                {carreras.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
              </select>
            </div>
            <div className="col-md-3">
              <label className="form-label fw-bold">Nivel máximo</label>
              <input
                type="number"
                min="1"
                max="10"
                className="form-control"
                value={nivelMaximo}
                onChange={(e) => setNivelMaximo(e.target.value)}
                placeholder="Todos"
              />
            </div>
            <div className="col-md-2">
              <button className="btn btn-outline-primary w-100" onClick={consultarAsignaturas} disabled={cargando}>
                Consultar
              </button>
            </div>
          </div>

          {asignaturas.length > 0 && (
            <div className="table-responsive mt-3">
              <table className="table table-sm table-bordered align-middle mb-0">
                <thead className="table-light">
                  <tr>
                    <th>Asignatura</th>
                    <th>Nivel</th>
                    <th>Área</th>
                    <th>Créditos</th>
                    <th>Horas</th>
                  </tr>
                </thead>
                <tbody>
                  {asignaturas.map((a) => (
                    <tr key={a.asignaturaId}>
                      <td>{a.nombre}</td>
                      <td>{a.nivel}</td>
                      <td>{a.area}</td>
                      <td>{a.creditos}</td>
                      <td>{a.horas}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <div className="card shadow-sm border-0 mb-4">
        <div className="card-header bg-primary text-white fw-bold">
          Carga de planes analíticos (HU-13)
        </div>
        <div className="card-body">
          <form onSubmit={subirPlanes}>
            <div className="mb-3">
              <label className="form-label fw-bold">Archivos PDF</label>
              <input
                type="file"
                className="form-control"
                accept=".pdf"
                multiple
                onChange={(e) => setArchivos(Array.from(e.target.files))}
              />
              <div className="form-text">
                El nombre del archivo debe empezar con el id de la asignatura (por ejemplo, <code>12_plan.pdf</code>).
              </div>
            </div>
            <button className="btn btn-primary fw-bold" disabled={cargando || archivos.length === 0}>
              {cargando ? "Cargando..." : "Cargar planes"}
            </button>
          </form>

          {resultadoPlanes && (
            <div className="mt-3">
              <p className="mb-1"><strong>Cargados:</strong> {resultadoPlanes.cargados}</p>
              {resultadoPlanes.omitidos?.length > 0 && (
                <div className="alert alert-warning mb-0">
                  <strong>Omitidos:</strong>
                  <ul className="mb-0">
                    {resultadoPlanes.omitidos.map((o, i) => (
                      <li key={i}>{o.archivo}: {o.motivo}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
