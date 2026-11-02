import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { obtenerResultadoAnalisis, validarReglasObjetivas } from "../services/homologacionService";

export default function ResultadoAnalisis() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [resultado, setResultado] = useState(null);
  const [validacion, setValidacion] = useState(null);
  const [asignaturaSeleccionada, setAsignaturaSeleccionada] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");

  // Envolvemos la función en useCallback para que no se recree en cada render
  const cargarResultado = useCallback(async () => {
    setCargando(true);
    setError("");
    try {
      const data = await obtenerResultadoAnalisis(id);
      setResultado(data);
      if (data.asignaturas && data.asignaturas.length > 0) {
        setAsignaturaSeleccionada(data.asignaturas[0]);
      }
    } catch {
      setError("No se pudo cargar el resultado del análisis.");
    } finally {
      setCargando(false);
    }
  }, [id]);

  useEffect(() => {
    const timer = setTimeout(() => {
      cargarResultado();
    }, 0);

    return () => clearTimeout(timer);
  }, [cargarResultado]);

  const handleValidar = async () => {
    try {
      const data = await validarReglasObjetivas(id);
      setValidacion(data);
    } catch {
      setError("Error al validar las reglas.");
    }
  };

  if (cargando) return <div className="container mt-4"><div className="alert alert-info">Cargando...</div></div>;
  if (error) return <div className="container mt-4"><div className="alert alert-danger">{error}</div></div>;

  return (
    <div className="container mt-4">
      <h2 className="mb-4 text-center">SISTEMA DE HOMOLOGACIÓN - UTA</h2>

      {/* Cabecera */}
      <div className="mb-4">
        <div className="row mb-2">
          <div className="col-md-2 fw-bold">Solicitud</div>
          <div className="col-md-10">SOL-{resultado?.numero || id} · {resultado?.carreraOrigen} → {resultado?.carreraDestino}</div>
        </div>
        <div className="row">
          <div className="col-md-2 fw-bold">Estado</div>
          <div className="col-md-4">
            <span className="badge bg-warning text-dark border">{resultado?.estado}</span>
          </div>
          <div className="col-md-2 fw-bold">Módulo</div>
          <div className="col-md-4">{resultado?.modulo || "Agente interno"}</div>
        </div>
      </div>

      {/* Tabla de asignaturas */}
      <div className="table-responsive mb-4">
        <table className="table table-bordered align-middle">
          <thead className="table-light">
            <tr>
              <th>Asignatura</th>
              <th>Contenido</th>
              <th>Profundidad</th>
              <th>Carga horaria</th>
              <th>Decisión</th>
              <th>Justificación</th>
            </tr>
          </thead>
          <tbody>
            {resultado?.asignaturas?.map((asig, index) => (
              <tr key={index}>
                <td>{asig.nombre}</td>
                <td>{asig.contenido != null ? `${asig.contenido} %` : "—"}</td>
                <td>{asig.profundidad != null ? `${asig.profundidad} %` : "—"}</td>
                <td>{asig.cargaHoraria != null ? `${asig.cargaHoraria} %` : "—"}</td>
                <td>
                  {asig.decision === "HOMOLOGABLE" && <span className="text-success">● Homologable</span>}
                  {asig.decision === "NO_HOMOLOGABLE" && <span className="text-danger">○ No homologable</span>}
                  {asig.decision === "REQUIERE_REVISION" && <span className="text-warning">△ Requiere revisión</span>}
                </td>
                <td>
                  <button className="btn btn-sm btn-outline-dark" onClick={() => setAsignaturaSeleccionada(asig)}>
                    Ver
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Cuadro de Justificación */}
      {asignaturaSeleccionada && (
        <div className="card shadow-sm border mb-4">
          <div className="card-body">
            <h5 className="card-title fw-bold">Justificación — {asignaturaSeleccionada.nombre}</h5>
            <p className="card-text">{asignaturaSeleccionada.justificacion}</p>
          </div>
        </div>
      )}

      {/* Textos informativos */}
      <p className="text-muted small">
        Umbral: 80 % en cada dimensión (RN-03). El sistema calcula la decisión; el agente solo entrega los porcentajes.<br/>
        Tiempo de análisis por asignatura: 34 s (máximo 60 s). "Requiere revisión": el agente no devolvió una respuesta válida.
      </p>

      {/* Botones de acción */}
      <div className="d-flex justify-content-between mt-4">
        <button className="btn btn-outline-secondary" onClick={() => navigate("/mis-solicitudes")}>
          Volver a Mis solicitudes
        </button>

        {/* Sección de validación de reglas (HU-07) */}
        <button className="btn btn-primary" onClick={handleValidar}>
          Validar Reglas Objetivas
        </button>
      </div>

      {validacion && (
        <div className="alert alert-info mt-3">
          <pre className="mb-0">{JSON.stringify(validacion, null, 2)}</pre>
        </div>
      )}
    </div>
  );
}