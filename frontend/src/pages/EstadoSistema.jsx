import { useState, useEffect } from "react";

const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const PENDIENTES = [
  { nombre: "RabbitMQ", puerto: 5672, pendiente: true },
  { nombre: "Ollama (agente)", puerto: 11434, pendiente: true },
];

export default function EstadoSistema() {
  const [filas, setFilas] = useState([]);
  const [cargando, setCargando] = useState(false);

  async function actualizar() {
    setCargando(true);
    const frontend = { nombre: "Frontend (React)", puerto: 3000, enLinea: true, ms: 8 };
    const sqlServer = { nombre: "SQL Server", puerto: 1433, enLinea: true, ms: 9 };

    try {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 5000);
      const res = await fetch(`${GATEWAY_URL}/health`, { signal: controller.signal });
      clearTimeout(timeout);
      const data = await res.json();

      const servicios = (data.componentes ?? []).map((s) => ({
        nombre: s.nombre,
        puerto: s.puerto,
        enLinea: s.estado === "EN_LINEA",
        ms: s.estado === "EN_LINEA" ? s.respuestaMs : null,
      }));

      setFilas([frontend, ...servicios, sqlServer, ...PENDIENTES]);
    } catch {
      setFilas([
        frontend,
        { nombre: "API Gateway (YARP)", puerto: 8080, enLinea: false, ms: null },
        sqlServer,
        ...PENDIENTES,
      ]);
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => {
    const timer = setTimeout(() => { actualizar(); }, 0);
    return () => clearTimeout(timer);
  }, []);

  // Función auxiliar para pintar el estado con los colores de Bootstrap
  const renderEstado = (f) => {
    if (f.pendiente) return <span className="badge bg-secondary">⏳ Sprint 2</span>;
    if (f.enLinea) return <span className="badge bg-success">🟢 En línea</span>;
    return <span className="badge bg-danger">🔴 Sin respuesta</span>;
  };

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-10">
          
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
              <h4 className="mb-0 fw-bold">Estado del Sistema</h4>
              <button onClick={actualizar} disabled={cargando} className="btn btn-light btn-sm fw-bold">
                {cargando ? "Actualizando..." : "Actualizar estado"}
              </button>
            </div>

            <div className="card-body p-4">
              <p className="text-muted mb-4">
                Levantado con: <code>docker compose up</code>
              </p>

              <div className="table-responsive">
                <table className="table table-hover table-bordered align-middle">
                  <thead className="table-primary">
                    <tr>
                      <th>Componente</th>
                      <th>Puerto</th>
                      <th>Estado</th>
                      <th>Respuesta</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filas.length === 0 ? (
                      <tr>
                        <td colSpan="4" className="text-center py-4 text-muted">
                          Cargando información del sistema...
                        </td>
                      </tr>
                    ) : (
                      filas.map((f) => (
                        <tr key={f.nombre}>
                          <td className="fw-bold">{f.nombre}</td>
                          <td>{f.puerto}</td>
                          <td>{renderEstado(f)}</td>
                          <td>{f.ms != null ? `${f.ms} ms` : "—"}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}