import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../context/AuthContext";
import { cargarMemoria, obtenerEstadoMemoria } from "../services/memoriaService";

const formatoFecha = (valor) =>
  valor ? new Date(valor).toLocaleString("es-EC") : "-";

export default function EstadoMemoria() {
  const { rol } = useAuth();
  const [estado, setEstado] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [actualizando, setActualizando] = useState(false);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  const consultar = useCallback(async () => {
    setCargando(true);
    setError("");
    try {
      setEstado(await obtenerEstadoMemoria());
    } catch (e) {
      setError(e.message || "No se pudo consultar el estado de la memoria.");
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    if (rol !== "ADMINISTRADOR") return;
    const timer = setTimeout(() => consultar(), 0);
    return () => clearTimeout(timer);
  }, [rol, consultar]);

  const actualizar = async () => {
    setActualizando(true);
    setError("");
    setMensaje("");
    try {
      const resultado = await cargarMemoria();
      setMensaje(
        resultado?.sinCambios
          ? "Los datos ya estaban al día: la huella no cambió y no se reescribió la memoria."
          : `Memoria actualizada. Se cargaron ${resultado?.cargadas ?? 0} registros con una nueva huella.`
      );
      await consultar();
    } catch (e) {
      setError(e.message || "No se pudo actualizar la memoria del agente.");
    } finally {
      setActualizando(false);
    }
  };

  if (rol !== "ADMINISTRADOR") {
    return (
      <div className="container mt-4">
        <div className="alert alert-danger">
          Acceso denegado. Solo el Administrador puede ver el estado de la memoria.
        </div>
      </div>
    );
  }

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-9">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
              <h5 className="mb-0 fw-bold">Estado de la memoria del agente</h5>
              <button
                className="btn btn-sm btn-outline-light"
                onClick={consultar}
                disabled={cargando || actualizando}
              >
                Refrescar
              </button>
            </div>

            <div className="card-body p-4">
              {error && <div className="alert alert-danger">{error}</div>}
              {mensaje && <div className="alert alert-success">{mensaje}</div>}

              {cargando ? (
                <div className="d-flex justify-content-center my-5">
                  <div className="spinner-border text-primary" role="status">
                    <span className="visually-hidden">Cargando...</span>
                  </div>
                </div>
              ) : !estado ? (
                <div className="alert alert-warning mb-0">
                  Todavía no se ha cargado la memoria del agente. Pulse
                  «Actualizar memoria» para generarla con las carreras, asignaturas y
                  contenidos mínimos del catálogo.
                </div>
              ) : (
                <div className="table-responsive">
                  <table className="table table-bordered align-middle mb-0">
                    <tbody>
                      <tr>
                        <th className="table-light" style={{ width: "40%" }}>
                          Huella
                        </th>
                        <td>
                          <code className="text-break">{estado.huella}</code>
                        </td>
                      </tr>
                      <tr>
                        <th className="table-light">Fecha de la última carga</th>
                        <td>{formatoFecha(estado.fechaCarga)}</td>
                      </tr>
                      <tr>
                        <th className="table-light">Total de carreras</th>
                        <td>{estado.totalCarreras}</td>
                      </tr>
                      <tr>
                        <th className="table-light">Total de asignaturas</th>
                        <td>{estado.totalAsignaturas}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              )}

              <div className="d-flex justify-content-end mt-4">
                <button
                  className="btn btn-primary fw-bold"
                  onClick={actualizar}
                  disabled={actualizando || cargando}
                >
                  {actualizando ? "Actualizando memoria..." : "Actualizar memoria"}
                </button>
              </div>

              <div className="alert alert-secondary small mb-0 mt-4">
                La memoria se llena con las mallas y los planes analíticos del catálogo.
                Si los datos de origen no cambiaron, la huella se mantiene y no se
                reescribe nada.
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
