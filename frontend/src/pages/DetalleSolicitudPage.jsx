import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  descargarInformeFinal,
  obtenerDelegaciones,
  obtenerDetalleSolicitud,
} from "../services/coordinadorService";
import {
  cedulaTitular,
  esRevisada,
  fechaCorta,
  nombreTitular,
  numeroDe,
  siguientePaso,
} from "../components/revision/etapas";
import "../components/revision/revision.css";
import "./DetalleSolicitudPage.css";

const HOMOLOGABLE = "HOMOLOGABLE";

// El trámite termina cuando el Coordinador envía el informe final al Comité.
const ETAPAS = ["Registrada", "Delegada", "Sugerencia del docente", "Informe enviado al Comité"];

// Cuántas etapas están completas según el estado y las delegaciones.
const etapasCompletas = (estado, delegaciones) => {
  if (estado === "INFORME_EMITIDO" || estado === "RESUELTA") return 4;
  if (delegaciones.some(esRevisada)) return 3;
  if (delegaciones.length > 0) return 2;
  return 1;
};

const claveEtapa = (completas) => ["sin", "sin", "esperando", "con", "emitido"][completas];

const nombreOrigen = (a) =>
  a.combinada && a.materiasOrigen?.length ? a.materiasOrigen.map((m) => m.nombre).join(" + ") : a.asignaturaOrigen;

function Decision({ valor }) {
  if (!valor) return <span className="text-muted">—</span>;
  const si = valor === HOMOLOGABLE;
  return (
    <span className={`badge rounded-pill ${si ? "bg-success-subtle text-success-emphasis" : "bg-danger-subtle text-danger-emphasis"}`}>
      {si ? "Homologa" : valor === "REQUIERE_REVISION" ? "Revisar" : "No"}
    </span>
  );
}

/** Detalle de solo lectura: dónde está el trámite y cuál es el siguiente paso. */
export default function DetalleSolicitudPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [solicitud, setSolicitud] = useState(null);
  const [delegaciones, setDelegaciones] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");
  const [descargando, setDescargando] = useState(false);

  useEffect(() => {
    let activo = true;
    Promise.all([obtenerDetalleSolicitud(id), obtenerDelegaciones(id).catch(() => [])])
      .then(([detalle, dels]) => {
        if (!activo) return;
        setSolicitud(detalle);
        setDelegaciones(Array.isArray(dels) ? dels : []);
      })
      .catch((e) => activo && setError(e.message || "No se pudo cargar el detalle de la solicitud."))
      .finally(() => activo && setCargando(false));
    return () => {
      activo = false;
    };
  }, [id]);

  const descargar = async () => {
    setDescargando(true);
    setError("");
    try {
      await descargarInformeFinal(id);
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe final.");
    } finally {
      setDescargando(false);
    }
  };

  if (cargando) {
    return (
      <div className="container d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
        <div className="spinner-border spinner-border-sm text-primary" role="status" />
        Cargando solicitud…
      </div>
    );
  }

  if (!solicitud) {
    return (
      <div className="container">
        <div className="alert alert-danger">{error || "Solicitud no encontrada."}</div>
        <button type="button" className="btn btn-outline-secondary" onClick={() => navigate(-1)}>← Volver</button>
      </div>
    );
  }

  const completas = etapasCompletas(solicitud.estado, delegaciones);
  const siguiente = siguientePaso(solicitud.estado, claveEtapa(completas), id);
  const asignaturas = solicitud.asignaturas || [];
  const finalizada = completas >= ETAPAS.length;
  // Solicitudes antiguas pueden tener resolución del Consejo: solo entonces se muestra.
  const resolucion = solicitud.resolucion;
  const conConsejo = asignaturas.some((a) => a.decisionFinal);

  return (
    <div className="container pb-5">
      <button type="button" className="btn btn-link px-0 mb-2 text-decoration-none" onClick={() => navigate(-1)}>
        ← Volver
      </button>

      {/* Encabezado y siguiente paso */}
      <div className="d-flex flex-wrap justify-content-between align-items-end gap-3 mb-3">
        <div>
          <p className="text-muted small text-uppercase fw-semibold mb-1 dd-sobretitulo">Solicitud de homologación</p>
          <h1 className="h3 fw-bold mb-1">{numeroDe(solicitud)}</h1>
          <p className="text-muted mb-0">
            {nombreTitular(solicitud) || "Titular sin identificar"} · {solicitud.carreraOrigen} → {solicitud.carreraDestino}
          </p>
        </div>
        <div className="d-flex flex-wrap gap-2">
          {finalizada && (
            <button type="button" className="btn btn-outline-secondary" onClick={descargar} disabled={descargando}>
              {descargando ? "Descargando…" : "⬇ Informe final (PDF)"}
            </button>
          )}
          {siguiente && (
            <Link to={siguiente.ruta} className="btn btn-primary fw-semibold">
              {siguiente.texto} →
            </Link>
          )}
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {finalizada && (
        <div className="alert alert-success py-2">
          <strong>Trámite finalizado.</strong> El informe final se envió al Comité
          {solicitud.fechaInforme ? ` el ${fechaCorta(solicitud.fechaInforme)}` : ""}.
        </div>
      )}

      {/* Línea de tiempo del trámite */}
      <div className="card border-0 shadow-sm mb-4">
        <div className="card-body">
          <ol className="ds-etapas" aria-label="Avance del trámite">
            {ETAPAS.map((nombre, i) => {
              const estado = i < completas ? "hecha" : i === completas ? "actual" : "";
              return (
                <li key={nombre} className={estado} aria-current={estado === "actual" ? "step" : undefined}>
                  <span className="ds-punto" aria-hidden="true">{i < completas ? "✓" : i + 1}</span>
                  <span className="ds-nombre">{nombre}</span>
                </li>
              );
            })}
          </ol>
        </div>
      </div>

      <div className="row g-4">
        {/* Datos */}
        <div className="col-lg-4">
          <div className="card border-0 shadow-sm h-100">
            <div className="card-body">
              <h2 className="h6 fw-bold mb-3">Datos del trámite</h2>
              <dl className="ds-datos mb-0">
                <dt>Titular</dt>
                <dd>{nombreTitular(solicitud) || <span className="text-muted fst-italic">Sin identificar</span>}</dd>
                <dt>Cédula</dt>
                <dd>{cedulaTitular(solicitud) || "—"}</dd>
                <dt>Modalidad</dt>
                <dd>
                  {solicitud.modulo === "EXTERNO"
                    ? `Externo · ${solicitud.universidadOrigen || "Origen"} → ${solicitud.universidadDestino || "Destino"}`
                    : "Interno"}
                </dd>
                <dt>Registrada</dt>
                <dd>
                  {fechaCorta(solicitud.fecha)} · {solicitud.comiteCorreo || "—"}
                </dd>
                <dt>Docentes</dt>
                <dd>
                  {delegaciones.length === 0
                    ? "Sin delegar"
                    : delegaciones.map((d) => (
                        <div key={d.id ?? d.docenteCorreo}>
                          {d.docenteCorreo}{" "}
                          <span className="text-muted small">· {esRevisada(d) ? "revisó" : "pendiente"}</span>
                        </div>
                      ))}
                </dd>
                {solicitud.fechaInforme && (
                  <>
                    <dt>Informe final</dt>
                    <dd>
                      {fechaCorta(solicitud.fechaInforme)} · {solicitud.coordinadorCorreo || "—"}
                    </dd>
                  </>
                )}
              </dl>
            </div>
          </div>
        </div>

        {/* Asignaturas */}
        <div className="col-lg-8">
          <div className="card border-0 shadow-sm">
            <div className="card-body">
              <h2 className="h6 fw-bold mb-3">Asignaturas ({asignaturas.length})</h2>
              {asignaturas.length === 0 ? (
                <p className="text-muted mb-0">Esta solicitud todavía no tiene asignaturas analizadas.</p>
              ) : (
                <div className="table-responsive">
                  <table className="table align-middle mb-0 ds-tabla">
                    <thead>
                      <tr>
                        <th>Asignatura</th>
                        <th className="text-center">Agente</th>
                        <th className="text-center">Informe final</th>
                        {conConsejo && <th className="text-center">Consejo</th>}
                      </tr>
                    </thead>
                    <tbody>
                      {asignaturas.map((a) => (
                        <tr key={a.id}>
                          <td>
                            <div className="fw-semibold">{nombreOrigen(a)}</div>
                            <div className="small text-muted">→ {a.asignaturaDestino}</div>
                          </td>
                          <td className="text-center">
                            <Decision valor={a.propuesta?.decision} />
                            {a.propuesta?.porcentajeContenido != null && (
                              <div className="small text-muted">{a.propuesta.porcentajeContenido} %</div>
                            )}
                          </td>
                          <td className="text-center"><Decision valor={a.decisionCoordinador} /></td>
                          {conConsejo && <td className="text-center"><Decision valor={a.decisionFinal} /></td>}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>

          {resolucion && (
            <div className="card border-0 shadow-sm mt-4">
              <div className="card-body">
                <h2 className="h6 fw-bold mb-2">Resolución del Consejo Directivo</h2>
                <p className="mb-1">
                  <strong>{resolucion.numeroResolucion}</strong> · {fechaCorta(resolucion.fecha)}
                </p>
                {resolucion.observacion && <p className="small mb-1">{resolucion.observacion}</p>}
                <p className="small text-muted mb-0">
                  Registrada por {resolucion.registradoPor} el {fechaCorta(resolucion.fechaRegistro)}.
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
