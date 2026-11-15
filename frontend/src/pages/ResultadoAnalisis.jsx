import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { obtenerResultadoAnalisis, validarReglasObjetivas } from "../services/homologacionService";
import { descargarInforme } from "../services/catalogoService";

const ETIQUETA_DECISION = {
  HOMOLOGABLE: { texto: "● Homologable", clase: "text-success" },
  NO_HOMOLOGABLE: { texto: "○ No homologable", clase: "text-danger" },
  REQUIERE_REVISION: { texto: "△ Requiere revisión", clase: "text-warning" },
  SIN_EQUIVALENTE: { texto: "✕ Sin equivalente", clase: "text-secondary" },
};

export default function ResultadoAnalisis() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [resultado, setResultado] = useState(null);
  const [validacion, setValidacion] = useState(null);
  const [asignaturaSeleccionada, setAsignaturaSeleccionada] = useState(null);

  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");
  const [validando, setValidando] = useState(false);
  const [errorValidacion, setErrorValidacion] = useState("");
  const [descargando, setDescargando] = useState(false);

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
      setError("No se pudo cargar el resultado del análisis. La solicitud todavía no tiene un análisis guardado.");
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
    setValidando(true);
    setErrorValidacion("");
    try {
      const data = await validarReglasObjetivas(id);
      setValidacion(data);
    } catch {
      setErrorValidacion("Error al validar las reglas. Intente nuevamente.");
    } finally {
      setValidando(false);
    }
  };

  const handleInforme = async () => {
    setDescargando(true);
    setErrorValidacion("");
    try {
      await descargarInforme(id);
    } catch (err) {
      setErrorValidacion(err.message || "No se pudo descargar el informe.");
    } finally {
      setDescargando(false);
    }
  };

  if (cargando) return <div className="container mt-4"><div className="alert alert-info">Cargando...</div></div>;
  if (error) return <div className="container mt-4"><div className="alert alert-danger">{error}</div></div>;

  const puedeDescargar = ["INFORME_EMITIDO", "RESUELTA", "APROBADA", "RECHAZADA"].includes(resultado?.estado);

  return (
    <div className="container mt-4">
      <h2 className="mb-4 text-center">SISTEMA DE HOMOLOGACIÓN - UTA</h2>

      <div className="mb-4">
        <div className="row mb-2">
          <div className="col-md-2 fw-bold">Solicitud</div>
          <div className="col-md-10">SOL-{String(resultado?.solicitudId ?? id).padStart(4, "0")}</div>
        </div>
        <div className="row mb-2">
          <div className="col-md-2 fw-bold">Estado</div>
          <div className="col-md-4">
            <span className="badge bg-warning text-dark border">{resultado?.estado}</span>
          </div>
          <div className="col-md-2 fw-bold">Fecha del análisis</div>
          <div className="col-md-4">
            {resultado?.fechaAnalisis ? new Date(resultado.fechaAnalisis).toLocaleString() : "—"}
          </div>
        </div>
        {resultado?.memoriaHuella && (
          <div className="row">
            <div className="col-md-2 fw-bold">Memoria</div>
            <div className="col-md-10 small text-muted text-break">{resultado.memoriaHuella}</div>
          </div>
        )}
      </div>

      <div className="table-responsive mb-4">
        <table className="table table-bordered align-middle">
          <thead className="table-light">
            <tr>
              <th>Materia de origen</th>
              <th>Asignatura de destino</th>
              <th>Contenido</th>
              <th>Profundidad</th>
              <th>Carga horaria</th>
              <th>Decisión</th>
              <th>Detalle</th>
            </tr>
          </thead>
          <tbody>
            {(resultado?.asignaturas || []).map((asig, index) => {
              const info = ETIQUETA_DECISION[asig.decision] || { texto: asig.decision, clase: "" };
              return (
                <tr key={index}>
                  <td>
                    {asig.materiasOrigen && asig.materiasOrigen.length > 0 ? (
                      <ul className="list-unstyled mb-0">
                        {asig.materiasOrigen.map((m, i) => (
                          <li key={i}>
                            {m.nombre}
                            {m.nota != null && <span className="text-muted small"> (nota {m.nota})</span>}
                          </li>
                        ))}
                      </ul>
                    ) : (
                      asig.asignaturaOrigen
                    )}
                    {asig.combinada && <span className="badge bg-primary ms-2">Combinada</span>}
                    {asig.reglaAplicada && <div className="small text-muted">Regla: {asig.reglaAplicada}</div>}
                  </td>
                  <td>{asig.asignaturaDestino || "—"}</td>
                  <td>{asig.porcentajeContenido != null ? `${asig.porcentajeContenido} %` : "—"}</td>
                  <td>{asig.porcentajeProfundidad != null ? `${asig.porcentajeProfundidad} %` : "—"}</td>
                  <td>{asig.porcentajeHoras != null ? `${asig.porcentajeHoras} %` : "—"}</td>
                  <td><span className={info.clase}>{info.texto}</span></td>
                  <td>
                    <button className="btn btn-sm btn-outline-dark" onClick={() => setAsignaturaSeleccionada(asig)}>
                      Ver
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {asignaturaSeleccionada && (
        <div className="card shadow-sm border mb-4">
          <div className="card-body">
            <h5 className="card-title fw-bold">Justificación — {asignaturaSeleccionada.asignaturaDestino || asignaturaSeleccionada.asignaturaOrigen}</h5>
            <p className="card-text">{asignaturaSeleccionada.justificacion}</p>
            {asignaturaSeleccionada.requiereRevision && (
              <p className="text-warning mb-0">Esta asignatura requiere revisión manual del Coordinador.</p>
            )}
          </div>
        </div>
      )}

      <p className="text-muted small">
        El sistema calcula la decisión en base a las reglas configuradas. El agente de IA solo entrega los porcentajes de similitud.<br />
        "Requiere revisión": el agente no devolvió una respuesta válida. "Sin equivalente": no hay una materia de destino candidata.
      </p>

      <div className="d-flex justify-content-between align-items-center mt-4">
        <button className="btn btn-outline-secondary" onClick={() => navigate("/mis-solicitudes")}>
          Volver a Mis solicitudes
        </button>

        <div className="text-end">
          {puedeDescargar && (
            <button className="btn btn-success me-2" onClick={handleInforme} disabled={descargando}>
              {descargando ? "Generando..." : "Descargar informe PDF"}
            </button>
          )}
          <button className="btn btn-primary" onClick={handleValidar} disabled={validando}>
            {validando ? "Validando..." : "Validar Reglas Objetivas"}
          </button>
          {errorValidacion && <div className="text-danger small mt-2">{errorValidacion}</div>}
        </div>
      </div>

      {validacion && validacion.reglas && (
        <div className="card shadow-sm border mt-4">
          <div className="card-header bg-light">
            <h5 className="mb-0">Validación de Reglas Objetivas</h5>
          </div>
          <div className="card-body p-0">
            <table className="table table-sm table-bordered mb-0">
              <thead>
                <tr>
                  <th>Regla</th>
                  <th>Resultado</th>
                  <th>Detalle</th>
                </tr>
              </thead>
              <tbody>
                {validacion.reglas.map((regla, i) => (
                  <tr key={i}>
                    <td>{regla.nombre || regla.codigo}</td>
                    <td>
                      {regla.cumple ? (
                        <span className="text-success">✓ Cumple</span>
                      ) : (
                        <span className="text-danger">✗ No cumple</span>
                      )}
                    </td>
                    <td>{regla.detalle || "Sin detalle adicional"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
