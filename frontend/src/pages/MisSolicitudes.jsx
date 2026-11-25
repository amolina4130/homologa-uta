import { useState, useEffect, useCallback } from "react";
import { TablaSolicitudes } from "../components/TablaSolicitudes";
import {
  obtenerCarreras,
  obtenerMisSolicitudes,
} from "../services/solicitudesService";
import { descargarInforme } from "../services/homologacionService";
import { useAuth } from "../context/AuthContext";

const FILTROS_VACIOS = { estado: "", carreraId: "", desde: "", hasta: "" };

const ESTADOS = [
  { valor: "", etiqueta: "Todos los estados" },
  { valor: "REGISTRADA", etiqueta: "Registrada" },
  { valor: "PENDIENTE_REVISION", etiqueta: "En revisión" },
  { valor: "INFORME_EMITIDO", etiqueta: "Informe recibido" },
];

const MisSolicitudes = () => {
  const { rol } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [carreras, setCarreras] = useState([]);
  const [filtros, setFiltros] = useState(FILTROS_VACIOS);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);
  const [mensaje, setMensaje] = useState("");

  const cargarDatos = useCallback(async (criterios = {}) => {
    setCargando(true);
    setError(null);
    try {
      const data = await obtenerMisSolicitudes(criterios);
      setSolicitudes(Array.isArray(data) ? data : data.data || []);
    } catch (err) {
      if (err.tipo === "no_autorizado") {
        setError("Debes iniciar sesión para ver tus solicitudes.");
      } else {
        setError(err.message);
      }
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => cargarDatos(), 0);
    obtenerCarreras()
      .then((data) => setCarreras(Array.isArray(data) ? data : []))
      .catch(() => setCarreras([]));
    return () => clearTimeout(timer);
  }, [cargarDatos]);

  const cambiarFiltro = (campo, valor) => {
    setFiltros((prev) => ({ ...prev, [campo]: valor }));
  };

  const aplicarFiltros = (ev) => {
    ev.preventDefault();
    setMensaje("");
    cargarDatos(filtros);
  };

  const limpiarFiltros = () => {
    setFiltros(FILTROS_VACIOS);
    setMensaje("");
    cargarDatos({});
  };

  const descargar = async (id) => {
    setError(null);
    setMensaje("");
    try {
      await descargarInforme(id);
      setMensaje(`Informe de SOL-${String(id).padStart(4, "0")} descargado.`);
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe.");
    }
  };

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-11">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Mis Solicitudes</h4>
              <small className="d-block fw-normal">
                Solicitudes registradas por usted como {rol === "COORDINADOR" ? "Coordinador" : "Comité"}.
                No es lo mismo que “Solicitudes pendientes de revisión”, que son las que aún requieren su gestión o decisión.
              </small>
            </div>
            <div className="card-body p-4">
              <form className="row g-3 align-items-end mb-4" onSubmit={aplicarFiltros}>
                <div className="col-md-3">
                  <label htmlFor="filtroEstado" className="form-label fw-bold">
                    Estado
                  </label>
                  <select
                    id="filtroEstado"
                    className="form-select"
                    value={filtros.estado}
                    onChange={(e) => cambiarFiltro("estado", e.target.value)}
                  >
                    {ESTADOS.map((e) => (
                      <option key={e.valor} value={e.valor}>
                        {e.etiqueta}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="col-md-3">
                  <label htmlFor="filtroCarrera" className="form-label fw-bold">
                    Carrera
                  </label>
                  <select
                    id="filtroCarrera"
                    className="form-select"
                    value={filtros.carreraId}
                    onChange={(e) => cambiarFiltro("carreraId", e.target.value)}
                  >
                    <option value="">Todas las carreras</option>
                    {carreras.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.nombre}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="col-md-2">
                  <label htmlFor="filtroDesde" className="form-label fw-bold">
                    Desde
                  </label>
                  <input
                    id="filtroDesde"
                    type="date"
                    className="form-control"
                    value={filtros.desde}
                    onChange={(e) => cambiarFiltro("desde", e.target.value)}
                  />
                </div>

                <div className="col-md-2">
                  <label htmlFor="filtroHasta" className="form-label fw-bold">
                    Hasta
                  </label>
                  <input
                    id="filtroHasta"
                    type="date"
                    className="form-control"
                    value={filtros.hasta}
                    onChange={(e) => cambiarFiltro("hasta", e.target.value)}
                  />
                </div>

                <div className="col-md-2 d-flex gap-2">
                  <button type="submit" className="btn btn-primary fw-bold" disabled={cargando}>
                    Filtrar
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline-secondary"
                    onClick={limpiarFiltros}
                    disabled={cargando}
                  >
                    Limpiar
                  </button>
                </div>
              </form>

              {mensaje && <div className="alert alert-success">{mensaje}</div>}

              {error && (
                <div className="alert alert-danger shadow-sm" role="alert">
                  {error}
                </div>
              )}

              {cargando ? (
                <div className="d-flex justify-content-center my-5">
                  <div className="spinner-border text-primary" role="status">
                    <span className="visually-hidden">Cargando...</span>
                  </div>
                </div>
              ) : (
                <TablaSolicitudes
                  solicitudes={solicitudes}
                  onDescargarInforme={descargar}
                />
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default MisSolicitudes;
