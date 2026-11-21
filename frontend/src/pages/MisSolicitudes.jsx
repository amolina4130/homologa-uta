import { useState, useEffect } from "react";
import { TablaSolicitudes } from '../components/TablaSolicitudes';
import { obtenerMisSolicitudes } from '../services/solicitudesService';

const MisSolicitudes = () => {
  const [solicitudes, setSolicitudes] = useState([]);
  const [filtradas, setFiltradas] = useState([]); 
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);

  const [filtroEstado, setFiltroEstado] = useState("");
  const [filtroCarrera, setFiltroCarrera] = useState("");
  const [filtroFecha, setFiltroFecha] = useState("");

  useEffect(() => {
    const cargarDatos = async () => {
      try {
        const data = await obtenerMisSolicitudes();
        const lista = Array.isArray(data) ? data : data.data || [];
        setSolicitudes(lista);
        setFiltradas(lista);
      } catch (err) {
        setError(err.tipo === "no_autorizado" ? "Debes iniciar sesión." : err.message);
      } finally {
        setCargando(false);
      }
    };
    cargarDatos();
  }, []);

  useEffect(() => {
    let resultado = solicitudes;

    if (filtroEstado) resultado = resultado.filter(s => s.estado === filtroEstado);
    if (filtroCarrera) {
      resultado = resultado.filter(s => 
        (s.carreraOrigen && s.carreraOrigen.toLowerCase().includes(filtroCarrera.toLowerCase())) ||
        (s.carreraDestino && s.carreraDestino.toLowerCase().includes(filtroCarrera.toLowerCase()))
      );
    }
    if (filtroFecha) resultado = resultado.filter(s => s.fecha && s.fecha.startsWith(filtroFecha));

    setFiltradas(resultado);
  }, [filtroEstado, filtroCarrera, filtroFecha, solicitudes]);

  const limpiarFiltros = () => {
    setFiltroEstado("");
    setFiltroCarrera("");
    setFiltroFecha("");
  };

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-10">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Mis Solicitudes</h4>
            </div>
            <div className="card-body p-4">
              
              {/* SECCIÓN DE FILTROS (Nuevo) */}
              <div className="row mb-4 bg-light p-3 rounded border">
                <div className="col-md-3">
                  <label className="form-label fw-bold small">Estado</label>
                  <select className="form-select form-select-sm" value={filtroEstado} onChange={(e) => setFiltroEstado(e.target.value)}>
                    <option value="">Todos</option>
                    <option value="REGISTRADA">Registrada</option>
                    <option value="PENDIENTE_REVISION">Pendiente Revisión</option>
                    <option value="RESUELTA">Resuelta</option>
                  </select>
                </div>
                <div className="col-md-3">
                  <label className="form-label fw-bold small">Carrera</label>
                  <input type="text" className="form-control form-control-sm" placeholder="Ej. Software" value={filtroCarrera} onChange={(e) => setFiltroCarrera(e.target.value)} />
                </div>
                <div className="col-md-3">
                  <label className="form-label fw-bold small">Fecha</label>
                  <input type="date" className="form-control form-control-sm" value={filtroFecha} onChange={(e) => setFiltroFecha(e.target.value)} />
                </div>
                <div className="col-md-3 d-flex align-items-end">
                  <button className="btn btn-secondary btn-sm w-100" onClick={limpiarFiltros}>Limpiar Filtros</button>
                </div>
              </div>

              {cargando && <div className="text-center my-5"><div className="spinner-border text-primary"></div></div>}
              {error && <div className="alert alert-danger">{error}</div>}
              
              {!cargando && !error && (
                <TablaSolicitudes solicitudes={filtradas} />
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default MisSolicitudes;