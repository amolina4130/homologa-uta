import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  obtenerResultadoAnalisis,
  analizarSolicitud,
  analizarSolicitudExterna,
} from "../services/homologacionService";
import { obtenerSolicitud } from "../services/solicitudesService";

const claseDecision = (decision) => {
  if (decision === "HOMOLOGABLE") return "bg-success";
  if (decision === "NO_HOMOLOGABLE") return "bg-danger";
  return "bg-warning text-dark";
};

const textoDecision = (decision) => {
  if (decision === "HOMOLOGABLE") return "Homologable";
  if (decision === "NO_HOMOLOGABLE") return "No homologable";
  if (decision === "REQUIERE_REVISION") return "Requiere revisión";
  return decision || "Pendiente";
};

export default function ResultadoAnalisis() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [resultado, setResultado] = useState(null);
  const [solicitud, setSolicitud] = useState(null);
  const [asignaturaSeleccionada, setAsignaturaSeleccionada] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");
  const [sinAnalisis, setSinAnalisis] = useState(false);
  const [generando, setGenerando] = useState(false);
  const [errorGenerando, setErrorGenerando] = useState("");
  const [reintentable, setReintentable] = useState(false);

  const esExterno =
    (resultado?.moduloSolicitud || solicitud?.modulo) === "EXTERNO";

  const cargarResultado = useCallback(async () => {
    setCargando(true);
    setError("");
    setSinAnalisis(false);
    setErrorGenerando("");
    setReintentable(false);
    try {
      const data = await obtenerResultadoAnalisis(id);
      setResultado(data);
      setAsignaturaSeleccionada(data.asignaturas?.[0] ?? null);
    } catch (e) {
      if (e.status === 404) {
        // El resultado no existe todavía: se consulta la solicitud para saber
        // si debe analizarse con el agente interno o con el externo.
        try {
          setSolicitud(await obtenerSolicitud(id));
        } catch {
          // Si tampoco se puede consultar la solicitud, se muestra el flujo interno.
        }
        setSinAnalisis(true);
      } else {
        setError(e.message || "No se pudo cargar el resultado del análisis.");
      }
    } finally {
      setCargando(false);
    }
  }, [id]);

  useEffect(() => {
    const timer = setTimeout(() => cargarResultado(), 0);
    return () => clearTimeout(timer);
  }, [cargarResultado]);

  const generarAnalisis = async () => {
    setGenerando(true);
    setErrorGenerando("");
    setReintentable(false);
    try {
      if (esExterno) {
        await analizarSolicitudExterna(id);
      } else {
        await analizarSolicitud(id);
      }
      await cargarResultado();
    } catch (e) {
      setErrorGenerando(
        e.message || "No se pudo generar el análisis. Intente nuevamente."
      );
      setReintentable(Boolean(e.reintentable));
    } finally {
      setGenerando(false);
    }
  };

  if (cargando) {
    return (
      <div className="container mt-4">
        <div className="alert alert-info">Cargando análisis...</div>
      </div>
    );
  }

  if (sinAnalisis) {
    return (
      <div className="container mt-4">
        <div className="card shadow-sm border-0">
          <div className="card-header bg-warning text-dark">
            <h4 className="mb-0">Solicitud SOL-{id}</h4>
          </div>
          <div className="card-body">
            {esExterno ? (
              <>
                <p className="mb-3">
                  Esta solicitud usa el <strong>agente externo</strong>. Primero adjunte la
                  malla, el historial y los sílabos (documentos procesados); después genere
                  el análisis externo.
                </p>
                <div className="d-flex flex-wrap gap-2">
                  <button
                    className="btn btn-outline-primary"
                    onClick={() =>
                      navigate(`/adjuntar-documentos?solicitudId=${id}&volver=resultado`)
                    }
                    disabled={generando}
                  >
                    Adjuntar documentos
                  </button>
                  <button
                    className="btn btn-primary"
                    onClick={generarAnalisis}
                    disabled={generando}
                  >
                    {generando ? "Analizando documentos..." : "Generar análisis externo"}
                  </button>
                </div>
              </>
            ) : (
              <>
                <p className="mb-3">
                  Esta solicitud todavía no tiene un análisis del agente. Puedes generarlo
                  ahora con las asignaturas que registraste.
                </p>
                <button
                  className="btn btn-primary"
                  onClick={generarAnalisis}
                  disabled={generando}
                >
                  {generando ? "Analizando asignaturas..." : "Generar análisis"}
                </button>
              </>
            )}

            {errorGenerando && (
              <div className="alert alert-danger mt-3">
                <div>{errorGenerando}</div>
                {reintentable && (
                  <button
                    className="btn btn-sm btn-outline-dark mt-2"
                    onClick={generarAnalisis}
                    disabled={generando}
                  >
                    Reintentar
                  </button>
                )}
              </div>
            )}

            <button
              className="btn btn-outline-secondary ms-2 mt-3 mt-sm-0"
              onClick={() => navigate("/mis-solicitudes")}
              disabled={generando}
            >
              Volver a Mis solicitudes
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="container mt-4">
        <div className="alert alert-danger">{error}</div>
        <button className="btn btn-outline-primary me-2" onClick={cargarResultado}>
          Reintentar
        </button>
        <button className="btn btn-outline-secondary" onClick={() => navigate("/mis-solicitudes")}>
          Volver a Mis solicitudes
        </button>
      </div>
    );
  }

  const informeEmitido = resultado?.estado === "INFORME_EMITIDO";

  return (
    <div className="container mt-4">
      <h2 className="mb-4 text-center">SISTEMA DE HOMOLOGACIÓN - UTA</h2>

      <div className="card shadow-sm border-0 mb-4">
        <div className="card-body">
          <div className="row mb-2">
            <div className="col-md-2 fw-bold">Solicitud</div>
            <div className="col-md-10">
              {resultado?.numero || `SOL-${id}`} · {resultado?.carreraOrigen} → {resultado?.carreraDestino}
            </div>
          </div>
          <div className="row mb-2">
            <div className="col-md-2 fw-bold">Estudiante</div>
            <div className="col-md-10">{resultado?.estudiante}</div>
          </div>
          {esExterno && (resultado?.universidadOrigen || resultado?.universidadDestino) && (
            <div className="row mb-2">
              <div className="col-md-2 fw-bold">Universidades</div>
              <div className="col-md-10">
                {resultado?.universidadOrigen || "Origen"} →{" "}
                {resultado?.universidadDestino || "Destino"}
              </div>
            </div>
          )}
          <div className="row mb-2">
            <div className="col-md-2 fw-bold">Estado</div>
            <div className="col-md-4">
              <span className={`badge ${informeEmitido ? "bg-success" : "bg-warning text-dark"}`}>
                {resultado?.estado}
              </span>
            </div>
            <div className="col-md-2 fw-bold">Módulo</div>
            <div className="col-md-4">
              {resultado?.modulo}
              {esExterno && (
                <span className="badge bg-info text-dark ms-2">Sin reglas fijas</span>
              )}
            </div>
          </div>
          {informeEmitido && (
            <div className="row mb-2">
              <div className="col-md-2 fw-bold">Informe</div>
              <div className="col-md-10">
                Emitido por {resultado?.coordinadorCorreo}
                {resultado?.fechaInforme
                  ? ` el ${new Date(resultado.fechaInforme).toLocaleString("es-EC")}`
                  : ""}
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="table-responsive mb-4">
        <table className="table table-bordered align-middle">
          <thead className="table-light">
            <tr>
              <th>Asignatura origen</th>
              <th>Asignatura destino</th>
              <th>Contenido</th>
              <th>Profundidad</th>
              <th>Carga horaria</th>
              <th>Propuesta del agente</th>
              {informeEmitido && <th>Decisión final</th>}
              <th></th>
            </tr>
          </thead>
          <tbody>
            {resultado?.asignaturas?.map((asig) => (
              <tr key={asig.id}>
                <td>{asig.asignaturaOrigen}</td>
                <td>{asig.asignaturaDestino}</td>
                <td>{asig.porcentajeContenido} %</td>
                <td>{asig.porcentajeProfundidad} %</td>
                <td>{asig.porcentajeHoras} %</td>
                <td>
                  <span className={`badge ${claseDecision(asig.decision)}`}>
                    {textoDecision(asig.decision)}
                  </span>
                  {asig.requiereRevision && (
                    <div className="small text-muted mt-1">El agente no respondió; revisión manual.</div>
                  )}
                </td>
                {informeEmitido && (
                  <td>
                    {asig.decisionCoordinador ? (
                      <span className={`badge ${claseDecision(asig.decisionCoordinador)}`}>
                        {textoDecision(asig.decisionCoordinador)}
                      </span>
                    ) : (
                      <span className="text-muted">Sin decisión</span>
                    )}
                  </td>
                )}
                <td>
                  <button
                    className="btn btn-sm btn-outline-dark"
                    onClick={() => setAsignaturaSeleccionada(asig)}
                  >
                    Ver justificación
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {asignaturaSeleccionada && (
        <div className="card shadow-sm border mb-4">
          <div className="card-body">
            <h5 className="card-title fw-bold">
              Justificación — {asignaturaSeleccionada.asignaturaOrigen}
            </h5>
            <p className="card-text">
              {asignaturaSeleccionada.justificacion || "Sin justificación registrada."}
            </p>

            {asignaturaSeleccionada.observacionCoordinador && (
              <>
                <hr />
                <h6 className="fw-bold">Observación del Coordinador</h6>
                <p className="card-text mb-0">{asignaturaSeleccionada.observacionCoordinador}</p>
              </>
            )}
          </div>
        </div>
      )}

      <p className="text-muted small">
        {esExterno
          ? "El agente externo decide por su cuenta a partir de la documentación procesada: el sistema no le impone reglas ni umbrales."
          : 'El sistema calcula la decisión con las reglas configuradas. El agente de IA entrega los porcentajes de similitud y su justificación. "Requiere revisión" indica que el agente no devolvió una respuesta válida y la decisión queda en manos del Coordinador.'}
      </p>

      <div className="d-flex justify-content-between align-items-center mt-4">
        <button className="btn btn-outline-secondary" onClick={() => navigate("/mis-solicitudes")}>
          Volver a Mis solicitudes
        </button>
        <button className="btn btn-outline-primary" onClick={cargarResultado}>
          Actualizar
        </button>
      </div>
    </div>
  );
}
