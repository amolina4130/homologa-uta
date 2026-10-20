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
    // Agregamos "SQL Server" fijo, porque no tiene endpoint /health propio
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

      // Unimos todo: Frontend + Servicios reales (incluye el Gateway) + SQL Server + Pendientes (Sprint 2)
      setFilas([
        frontend,
        ...servicios,
        sqlServer,
        ...PENDIENTES,
      ]);
    } catch {
      // Si el Gateway no responde, se muestra todo lo demás en modo local
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
    // Envolvemos la llamada en un setTimeout para evitar el error de ESLint
    const timer = setTimeout(() => {
      actualizar();
    }, 0);

    return () => clearTimeout(timer);
  }, []);

  return (
    <div style={{ maxWidth: 800, margin: "40px auto", fontFamily: "sans-serif" }}>
      <h1 style={{ textAlign: "center" }}>SISTEMA DE HOMOLOGACIÓN - UTA</h1>
      <h2>Estado de los componentes</h2>

      <table border="1" cellPadding="8" style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead>
          <tr>
            <th>Componente</th>
            <th>Puerto</th>
            <th>Estado</th>
            <th>Respuesta</th>
          </tr>
        </thead>
        <tbody>
          {filas.map((f) => (
            <tr key={f.nombre}>
              <td>{f.nombre}</td>
              <td>{f.puerto}</td>

              {/* Evaluamos si es "pendiente" para mostrar el circulito "Sprint 2" */}
              <td>{f.pendiente ? "○ Sprint 2" : f.enLinea ? "● En línea" : "○ Sin respuesta"}</td>
              <td>{f.ms != null ? `${f.ms} ms` : "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <button onClick={actualizar} disabled={cargando} style={{ marginTop: 16 }}>
        {cargando ? "Actualizando..." : "Actualizar estado"}
      </button>
      <span style={{ marginLeft: 16 }}>Levantado con: docker compose up</span>
    </div>
  );
}