import { useState, useEffect } from "react";
import { obtenerEstadoMemoria, actualizarMemoria } from "../services/memoriaService";

export default function EstadoMemoria() {
  const [estado, setEstado] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");
  const [mensajeExito, setMensajeExito] = useState("");
  const [actualizando, setActualizando] = useState(false);

  const cargarEstado = async () => {
    setCargando(true);
    setError("");
    try {
      const data = await obtenerEstadoMemoria();
      setEstado(data);
    } catch (err) {
      setError(err.message); 
      setEstado(null);
    } finally {
      setCargando(false);
    }
  };

  useEffect(() => {
    cargarEstado();
  }, []);

  const handleActualizar = async () => {
    setActualizando(true);
    setError("");
    setMensajeExito("");
    try {
      const res = await actualizarMemoria();
      
      if (res.sinCambios) {
        setMensajeExito("Los datos ya estaban al día. No se requirieron cambios.");
      } else {
        setMensajeExito(`Memoria actualizada con éxito. Se cargaron ${res.cargadas} nuevos registros.`);
      }
      
      await cargarEstado();
    } catch (err) {
      setError(err.message);
    } finally {
      setActualizando(false);
    }
  };

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-8">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
              <h4 className="mb-0 fw-bold">Estado de la Memoria del Agente</h4>
              <span className="badge bg-light text-primary">Rol: Administrador</span>
            </div>
            
            <div className="card-body p-4">
              {error && (
                <div className="alert alert-warning text-center">
                  <strong>Atención:</strong> {error}
                </div>
              )}
              {mensajeExito && <div className="alert alert-success text-center">{mensajeExito}</div>}

              {cargando ? (
                <div className="text-center my-5">
                  <div className="spinner-border text-primary"></div>
                  <p className="mt-2 text-muted">Consultando memoria...</p>
                </div>
              ) : (
                estado && (
                  <div className="table-responsive">
                    <table className="table table-bordered align-middle">
                      <tbody>
                        <tr>
                          <th className="bg-light" style={{ width: "40%" }}>Huella de Memoria</th>
                          <td className="font-monospace text-muted">{estado.huella}</td>
                        </tr>
                        <tr>
                          <th className="bg-light">Última Carga</th>
                          <td>{new Date(estado.fechaCarga).toLocaleString()}</td>
                        </tr>
                        <tr>
                          <th className="bg-light">Total de Carreras</th>
                          <td><span className="badge bg-info text-dark fs-6">{estado.totalCarreras}</span></td>
                        </tr>
                        <tr>
                          <th className="bg-light">Total de Asignaturas</th>
                          <td><span className="badge bg-info text-dark fs-6">{estado.totalAsignaturas}</span></td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                )
              )}

              <div className="text-end mt-4 border-top pt-3">
                <button 
                  className="btn btn-primary fw-bold px-4" 
                  onClick={handleActualizar}
                  disabled={actualizando}
                >
                  {actualizando ? "Actualizando..." : "Actualizar Memoria"}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}