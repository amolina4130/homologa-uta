import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesDocente } from "../services/docenteService";
import { numeroSolicitud, yaRevisada } from "../utils/solicitudesDocente";
import { coincideBusqueda, diasDesde } from "../components/revision/etapas";
import "../components/revision/revision.css";
import "./InicioCoordinador.css";

// Bandeja del docente: las solicitudes que el Coordinador le asignó, separadas en pendientes
// y enviadas. Es también su página de inicio (con saludo).

const SECCIONES = [
  { valor: "pendientes", etiqueta: "Pendientes" },
  { valor: "enviadas", etiqueta: "Enviadas" },
];

const etapaDocente = (s) => {
  if (s.estadoDelegacion === "YA_REVISADO") return { texto: "Revisada por otro docente", clase: "secondary" };
  if (yaRevisada(s)) return { texto: "Informe enviado", clase: "success" };
  if (s.tieneSugerencia || s.fechaSugerencia) return { texto: "En revisión · borrador", clase: "info" };
  return { texto: "Por revisar", clase: "warning" };
};

const fecha = (v) => (v ? new Date(v).toLocaleDateString("es-EC", { day: "2-digit", month: "short", year: "numeric" }) : "-");

export default function SolicitudesDocente({ esInicio = false }) {
  const { usuario } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");
  const [seccion, setSeccion] = useState("pendientes");
  const [texto, setTexto] = useState("");

  const pedir = () =>
    obtenerSolicitudesDocente()
      .then((data) => {
        setSolicitudes(Array.isArray(data) ? data : []);
        setError("");
      })
      .catch((e) => setError(e.message || "No se pudieron cargar las solicitudes asignadas."))
      .finally(() => setCargando(false));

  const cargar = () => {
    setCargando(true);
    return pedir();
  };

  useEffect(() => {
    pedir();
    // Solo al entrar.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const pendientes = useMemo(() => solicitudes.filter((s) => !yaRevisada(s) && s.puedeEvaluar !== false), [solicitudes]);
  const enviadas = useMemo(() => solicitudes.filter((s) => yaRevisada(s) || s.puedeEvaluar === false), [solicitudes]);
  const lista = (seccion === "pendientes" ? pendientes : enviadas).filter((s) => coincideBusqueda(s, texto));
  const nombre = usuario?.nombre?.trim();

  return (
    <div className="container pb-5">
      <div className="d-flex flex-wrap justify-content-between align-items-end gap-3 mb-4">
        <div>
          <p className="text-muted small text-uppercase fw-semibold mb-1 dd-sobretitulo">Revisión docente</p>
          <h1 className="h3 fw-bold mb-1">{esInicio ? (nombre ? `Hola, ${nombre}` : "Bienvenido") : "Solicitudes asignadas"}</h1>
          <p className="text-muted mb-0">
            Revise la sugerencia de la IA, decida cada asignatura con su justificación y envíe su informe técnico al Coordinador.
          </p>
        </div>
        <button type="button" className="btn btn-outline-secondary" onClick={cargar} disabled={cargando}>
          ↻ Actualizar
        </button>
      </div>

      {!cargando && pendientes.length > 0 && (
        <div className="ic-pendientes d-flex flex-wrap align-items-center justify-content-between gap-3 p-3 mb-4 rounded-3">
          <div className="d-flex align-items-center gap-3">
            <span className="ic-pendientes-numero" aria-hidden="true">{pendientes.length}</span>
            <div>
              <div className="fw-bold">
                {pendientes.length === 1 ? "1 solicitud por revisar" : `${pendientes.length} solicitudes por revisar`}
              </div>
              <div className="small text-muted">
                La más antigua: {numeroSolicitud(pendientes[0])} · {pendientes[0].carreraOrigen} → {pendientes[0].carreraDestino}
              </div>
            </div>
          </div>
          <Link to={`/evaluar/${pendientes[0].solicitudId}`} className="btn btn-warning fw-semibold">
            Revisar ahora →
          </Link>
        </div>
      )}

      {error && <div className="alert alert-danger">{error}</div>}

      <div className="card border-0 shadow-sm">
        <div className="card-header bg-white border-bottom-0 pt-3 px-3 d-flex flex-wrap justify-content-between gap-2">
          <div className="ic-secciones" role="tablist" aria-label="Sección de solicitudes">
            {SECCIONES.map((x) => (
              <button
                key={x.valor}
                type="button"
                role="tab"
                aria-selected={seccion === x.valor}
                className={`ic-seccion ${seccion === x.valor ? "activa" : ""}`}
                onClick={() => setSeccion(x.valor)}
              >
                {x.etiqueta}
                <span className="ic-seccion-cuenta">
                  {cargando ? "…" : x.valor === "pendientes" ? pendientes.length : enviadas.length}
                </span>
              </button>
            ))}
          </div>
          <input
            type="search"
            className="form-control"
            style={{ maxWidth: 320 }}
            placeholder="Buscar por número, estudiante o carrera"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            aria-label="Buscar solicitudes"
          />
        </div>

        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0 ic-tabla">
            <thead>
              <tr>
                <th>Solicitud</th>
                <th>Estudiante</th>
                <th>Carreras</th>
                <th>Estado</th>
                <th className="text-end">Acción</th>
              </tr>
            </thead>
            <tbody>
              {cargando ? (
                <tr>
                  <td colSpan="5" className="text-center text-muted py-5">
                    <span className="spinner-border spinner-border-sm text-primary me-2" role="status" />
                    Cargando solicitudes…
                  </td>
                </tr>
              ) : lista.length === 0 ? (
                <tr>
                  <td colSpan="5" className="text-center text-muted py-5">
                    {seccion === "pendientes" ? "No tiene solicitudes por revisar." : "Todavía no ha enviado informes."}
                  </td>
                </tr>
              ) : (
                lista.map((s) => {
                  const etapa = etapaDocente(s);
                  const dias = diasDesde(s.fecha);
                  const pendiente = seccion === "pendientes";
                  return (
                    <tr key={s.solicitudId}>
                      <td>
                        <div className="fw-bold text-primary">{numeroSolicitud(s)}</div>
                        <div className="small text-muted">
                          {fecha(s.fecha)}
                          {pendiente && dias !== null ? ` · ${dias === 0 ? "hoy" : `hace ${dias} día${dias === 1 ? "" : "s"}`}` : ""}
                        </div>
                      </td>
                      <td>
                        <div className="fw-semibold">{s.estudiante || "—"}</div>
                        <div className="small text-muted">C.I. {s.estudianteCedula || "—"}</div>
                      </td>
                      <td className="ic-carreras">
                        <span className={`badge ${s.modulo === "EXTERNO" ? "bg-info text-dark" : "bg-primary"} me-1`}>
                          {s.modulo === "EXTERNO" ? "Externo" : "Interno"}
                        </span>
                        {s.carreraOrigen || "—"} → {s.carreraDestino || "—"}
                        <div className="small text-muted">{s.totalAsignaturas ?? 0} sugerencia(s) de la IA</div>
                      </td>
                      <td>
                        <span className={`badge rounded-pill bg-${etapa.clase}-subtle text-${etapa.clase}-emphasis border border-${etapa.clase}-subtle`}>
                          {etapa.texto}
                        </span>
                      </td>
                      <td className="text-end">
                        <Link
                          to={`/evaluar/${s.solicitudId}`}
                          className={`btn btn-sm ${pendiente ? "btn-primary fw-semibold" : "btn-outline-secondary"}`}
                        >
                          {pendiente ? (etapa.clase === "info" ? "Continuar" : "Revisar") : "Ver informe"}
                        </Link>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
