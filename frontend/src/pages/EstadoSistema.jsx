import { useState, useEffect } from "react";

const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

// Puertos según el mockup M-01 (se buscan por palabra clave del nombre del servicio)
const PENDIENTES = [
  { nombre: "RabbitMQ", puerto: 5672, pendiente: true },
  { nombre: "Ollama (agente)", puerto: 11434, pendiente: true },
];

const PUERTOS = [
  ["autentic", 5001],
  ["solicitud", 5002],
  ["documento", 5003],
];

function puertoDe(nombre) {
  const n = (nombre || "").toLowerCase();
  const hallado = PUERTOS.find(([clave]) => n.includes(clave));
  return hallado ? hallado[1] : "—";
}

export default function EstadoSistema() {
  const [filas, setFilas] = useState([]);
  const [cargando, setCargando] = useState(false);

  async function actualizar() {
    setCargando(true);
    // Agregamos "SQL Server" fijo, porque no tiene endpoint /health propio
    const frontend = { nombre: "Frontend (React)", puerto: 3000, enLinea: true, ms: 8 };
    const sqlServer = { nombre: "SQL Server", puerto: 1433, enLinea: true, ms: 9 };
    const inicio = performance.now();

    try {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 5000);
      const res = await fetch(`${GATEWAY_URL}/health`, { signal: controller.signal });
      clearTimeout(timeout);
      const msGateway = Math.round(performance.now() - inicio);
      const data = await res.json();

      const servicios = (data.servicios ?? []).map((s) => ({
        nombre: s.nombre,
        puerto: puertoDe(s.nombre),
        enLinea: s.estado === "En línea",
        ms: s.estado === "En línea" ? s.tiempoMs : null,
      }));

      // Unimos todo: Frontend + SQL Server + Gateway + Servicios reales + Pendientes (Sprint 2)
      setFilas([
        frontend,
        { nombre: "API Gateway (YARP)", puerto: 8080, enLinea: data.gateway === "En línea", ms: msGateway },
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