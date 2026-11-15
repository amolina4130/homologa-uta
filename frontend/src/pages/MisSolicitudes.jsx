import { useCallback, useEffect, useState } from "react";
import { TablaSolicitudes } from "../components/TablaSolicitudes";
import { obtenerMisSolicitudes, obtenerCarreras } from "../services/solicitudesService";

const ESTADOS = [
  { valor: "", etiqueta: "Todos los estados" },
  { valor: "REGISTRADA", etiqueta: "REGISTRADA" },
  { valor: "PENDIENTE_REVISION", etiqueta: "PENDIENTE_REVISION" },
  { valor: "INFORME_EMITIDO", etiqueta: "INFORME_EMITIDO" },
  { valor: "RESUELTA", etiqueta: "RESUELTA" },
  { valor: "APROBADA", etiqueta: "APROBADA" },
  { valor: "RECHAZADA", etiqueta: "RECHAZADA" },
];

const MisSolicitudes = () => {
  const [solicitudes, setSolicitudes] = useState([]);
  const [carreras, setCarreras] = useState([]);
  const [filtros, setFiltros] = useState({ estado: "", carreraId: "", desde: "", hasta: "" });
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);

  const cargarDatos = useCallback(async () => {
    setCargando(true);
    setError(null);
    try {
      const data = await obtenerMisSolicitudes(filtros);
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
  }, [filtros]);

  useEffect(() => {
    obtenerCarreras()
      .then((data) => setCarreras(data.data || data))
      .catch(() => setCarreras([]));
  }, []);

  useEffect(() => {
    cargarDatos();
  }, [cargarDatos]);

  const cambiar = (campo, valor) => setFiltros((prev) => ({ ...prev, [campo]: valor }));

  const limpiar = () => setFiltros({ estado: "", carreraId: "", desde: "", hasta: "" });

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-11">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Mis Solicitudes</h4>
            </div>
            <div className="card-body p-4">
              <form className="row g-2 align-items-end mb-3" onSubmit={(e) => { e.preventDefault(); cargarDatos(); }}>
                <div className="col-md-3">
                  <label className="form-label fw-bold small">Estado</label>
                  <select className="form-select form-select-sm" value={filtros.estado} onChange={(e) => cambiar("estado", e.target.value)}>
                    {ESTADOS.map((e) => <option key={e.valor} value={e.valor}>{e.etiqueta}</option>)}
                  </select>
                </div>
                <div className="col-md-3">
                  <label className="form-label fw-bold small">Carrera</label>
                  <select className="form-select form-select-sm" value={filtros.carreraId} onChange={(e) => cambiar("carreraId", e.target.value)}>
                    <option value="">Todas las carreras</option>
                    {carreras.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                  </select>
                </div>
                <div className="col-md-2">
                  <label className="form-label fw-bold small">Desde</label>
                  <input type="date" className="form-control form-control-sm" value={filtros.desde} onChange={(e) => cambiar("desde", e.target.value)} />
                </div>
                <div className="col-md-2">
                  <label className="form-label fw-bold small">Hasta</label>
                  <input type="date" className="form-control form-control-sm" value={filtros.hasta} onChange={(e) => cambiar("hasta", e.target.value)} />
                </div>
                <div className="col-md-2 d-flex gap-2">
                  <button type="submit" className="btn btn-sm btn-primary flex-grow-1">Filtrar</button>
                  <button type="button" className="btn btn-sm btn-outline-secondary" onClick={limpiar}>Limpiar</button>
                </div>
              </form>

              {cargando && (
                <div className="d-flex justify-content-center my-5">
                  <div className="spinner-border text-primary" role="status">
                    <span className="visually-hidden">Cargando...</span>
                  </div>
                </div>
              )}

              {error && (
                <div className="alert alert-danger shadow-sm" role="alert">
                  {error}
                </div>
              )}

              {!cargando && !error && (
                <TablaSolicitudes solicitudes={solicitudes} />
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default MisSolicitudes;
