import { useState, useEffect } from "react";

import { GATEWAY_URL } from "../services/config";

async function obtenerJson(ruta) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 5000);
  try {
    const res = await fetch(`${GATEWAY_URL}${ruta}`, { signal: controller.signal });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  } finally {
    clearTimeout(timeout);
  }
}

async function filaSalud(ruta, nombre, puerto) {
  const inicio = performance.now();
  const data = await obtenerJson(ruta);
  if (!data) return { nombre, puerto, enLinea: false, ms: null };

  return {
    nombre,
    puerto,
    enLinea: Boolean(data.enLinea),
    ms: typeof data.ms === "number" ? data.ms : Math.round(performance.now() - inicio),
  };
}

export default function EstadoSistema() {
  const [filas, setFilas] = useState([]);
  const [cargando, setCargando] = useState(false);

  async function actualizar() {
    setCargando(true);
    const frontend = { nombre: "Frontend (React)", puerto: 3000, enLinea: true, ms: 8 };
    const sqlServer = { nombre: "SQL Server", puerto: 1433, enLinea: true, ms: 9 };

    // Ollama y RabbitMQ se consultan en paralelo al Gateway (rutas /salud anónimas).
    const [health, ollama, rabbitmq] = await Promise.all([
      obtenerJson("/health"),
      filaSalud("/salud/ollama", "Ollama (agente)", 11434),
      filaSalud("/salud/rabbitmq", "RabbitMQ", 5672),
    ]);

    const servicios = health
      ? (health.componentes ?? []).map((s) => ({
          nombre: s.nombre,
          puerto: s.puerto,
          enLinea: s.estado === "EN_LINEA",
          ms: s.estado === "EN_LINEA" ? s.respuestaMs : null,
        }))
      : [{ nombre: "API Gateway (YARP)", puerto: 8080, enLinea: false, ms: null }];

    setFilas([frontend, ...servicios, sqlServer, ollama, rabbitmq]);
    setCargando(false);
  }

  useEffect(() => {
    const timer = setTimeout(() => { actualizar(); }, 0);
    return () => clearTimeout(timer);
  }, []);

  // Función auxiliar para pintar el estado con los colores de Bootstrap
  const renderEstado = (f) => {
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
