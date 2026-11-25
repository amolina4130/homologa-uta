import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesDocente } from "../services/docenteService";
import { filtrarPendientes, numeroSolicitud } from "../utils/solicitudesDocente";
import InicioCoordinador from "./InicioCoordinador";

const CAMPOS_PERFIL = [
  { clave: "nombre", etiqueta: "Nombre completo", placeholder: "Ej.: Ing. María Pérez" },
  { clave: "tipo", etiqueta: "Tipo", placeholder: "Ej.: Docente, Comité" },
  { clave: "titulo", etiqueta: "Título", placeholder: "Ej.: Ing., Mg., Ph.D." },
  { clave: "cedula", etiqueta: "Cédula", placeholder: "Ej.: 1800000001" },
  { clave: "telefono", etiqueta: "Teléfono", placeholder: "Ej.: 0999999999" },
];

const perfilInicial = (usuario) => ({
  nombre: usuario?.nombre || "",
  tipo: usuario?.tipo || "",
  titulo: usuario?.titulo || "",
  cedula: usuario?.cedula || "",
  telefono: usuario?.telefono || "",
});

export default function Inicio() {
  const { rol, usuario, guardarPerfil } = useAuth();
  const [pendientes, setPendientes] = useState([]);
  const [cargandoPendientes, setCargandoPendientes] = useState(true);
  const [solicitudesDocente, setSolicitudesDocente] = useState([]);
  const [perfil, setPerfil] = useState(() => perfilInicial(usuario));
  const [guardandoPerfil, setGuardandoPerfil] = useState(false);
  const [mensajePerfil, setMensajePerfil] = useState("");
  const [errorPerfil, setErrorPerfil] = useState("");

  useEffect(() => {
    if (rol !== "DOCENTE") return undefined;

    let activo = true;
    setCargandoPendientes(true);
    obtenerSolicitudesDocente()
      .then((lista) => {
        const arr = Array.isArray(lista) ? lista : [];
        if (activo) {
          setSolicitudesDocente(arr);
          setPendientes(filtrarPendientes(arr));
        }
      })
      .catch(() => {
        if (activo) {
          setSolicitudesDocente([]);
          setPendientes([]);
        }
      })
      .finally(() => {
        if (activo) setCargandoPendientes(false);
      });

    return () => {
      activo = false;
    };
  }, [rol]);

  const cambiarPerfil = (clave, valor) => {
    setPerfil((prev) => ({ ...prev, [clave]: valor }));
    setMensajePerfil("");
    setErrorPerfil("");
  };

  const guardarDatos = async (evento) => {
    evento.preventDefault();
    setGuardandoPerfil(true);
    setMensajePerfil("");
    setErrorPerfil("");
    try {
      const actualizado = await guardarPerfil(perfil);
      setPerfil(perfilInicial(actualizado));
      setMensajePerfil("Sus datos se guardaron correctamente.");
    } catch (e) {
      setErrorPerfil(e.message || "No se pudieron guardar sus datos.");
    } finally {
      setGuardandoPerfil(false);
    }
  };

  if (rol === "COORDINADOR") return <InicioCoordinador />;

  if (rol === "DOCENTE") {
    const esDocente = rol === "DOCENTE";
    const primera = pendientes[0];
    const total = pendientes.length;
    const iniciales = (perfil.nombre || usuario?.correo || (esDocente ? "D" : "C"))
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((parte) => parte[0]?.toUpperCase() || "")
      .join("");

    const etiquetaSolicitud = (s) =>
      s?.numeroProceso || s?.numero || numeroSolicitud(s) || `SOL-${String(s?.solicitudId || "").padStart(4, "0")}`;
    const rutaSolicitud = (s) =>
      esDocente ? `/evaluar/${s.solicitudId}` : `/detalle-solicitud/${s.solicitudId}`;

    const tarjetaPerfil = (
      <form className={`card shadow-sm border-0 ${esDocente ? "h-100" : ""}`} onSubmit={guardarDatos}>
        <div className="card-header bg-primary text-white">
          <h2 className="mb-0 fw-bold">Mi perfil</h2>
          <small className="d-block fw-normal">
            Sus datos se usan para identificarlo en el sistema y en los informes.
          </small>
        </div>
        <div className="card-body p-4">
          <div className="d-flex align-items-center gap-3 mb-4">
            <div
              className="rounded-circle bg-primary text-white d-flex align-items-center justify-content-center fw-bold fs-4"
              style={{ width: "64px", height: "64px", flex: "0 0 auto" }}
              aria-hidden="true"
            >
              {iniciales || (esDocente ? "D" : "C")}
            </div>
            <div>
              <div className="fw-bold fs-5">
                {perfil.nombre || usuario?.correo || (esDocente ? "Docente" : "Coordinador")}
              </div>
              <div className="text-muted small">
                {[perfil.titulo, perfil.tipo].filter(Boolean).join(" · ") || `Datos del ${esDocente ? "docente" : "coordinador"}`}
              </div>
              <div className="text-muted small">{usuario?.correo || "-"}</div>
            </div>
          </div>

          <div className="row g-3">
            {CAMPOS_PERFIL.map((campo) => (
              <div className={esDocente ? "col-md-6" : "col-12"} key={campo.clave}>
                <label htmlFor={`perfil-${campo.clave}`} className="form-label small fw-bold mb-1">
                  {campo.etiqueta}
                </label>
                <input
                  id={`perfil-${campo.clave}`}
                  className="form-control form-control-sm"
                  value={perfil[campo.clave] || ""}
                  placeholder={campo.placeholder}
                  onChange={(e) => cambiarPerfil(campo.clave, e.target.value)}
                />
              </div>
            ))}
            <div className={esDocente ? "col-md-6" : "col-12"}>
              <label className="form-label small fw-bold mb-1">Correo de acceso</label>
              <input className="form-control form-control-sm" value={usuario?.correo || ""} readOnly disabled />
            </div>
            <div className={esDocente ? "col-md-6" : "col-12"}>
              <label className="form-label small fw-bold mb-1">Rol</label>
              <input className="form-control form-control-sm" value={rol || ""} readOnly disabled />
            </div>
          </div>

          {mensajePerfil && <div className="alert alert-success small mt-3 mb-0">{mensajePerfil}</div>}
          {errorPerfil && <div className="alert alert-danger small mt-3 mb-0">{errorPerfil}</div>}

          <div className="d-flex justify-content-end mt-4">
            <button type="submit" className="btn btn-primary fw-bold" disabled={guardandoPerfil}>
              {guardandoPerfil ? "Guardando..." : "Guardar mis datos"}
            </button>
          </div>
        </div>
      </form>
    );

    const tarjetaPendientes = (
      <div className={`card shadow-sm border-0 ${esDocente ? "h-100" : ""}`}>
        <div className="card-header bg-light fw-bold">
          {esDocente ? "Notificaciones" : "Solicitudes pendientes de revisión"}
        </div>
        <div className="card-body p-4">
          <div className="d-flex align-items-start gap-3">
            <div className="position-relative" style={{ fontSize: "2.5rem", lineHeight: 1 }}>
              <span aria-hidden="true">{esDocente ? "🔔" : "📋"}</span>
              {!cargandoPendientes && total > 0 && (
                <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">
                  {total > 9 ? "9+" : total}
                  <span className="visually-hidden">solicitudes pendientes de revisar</span>
                </span>
              )}
            </div>
            <div>
              <h5 className="fw-bold mb-1">
                {cargandoPendientes
                  ? "Consultando solicitudes..."
                  : total > 0
                    ? `${total} solicitud${total === 1 ? "" : "es"} pendiente${total === 1 ? "" : "s"}`
                    : "Sin solicitudes pendientes"}
              </h5>
              <p className="text-muted small mb-0">
                {esDocente
                  ? "La campanita muestra un número solo cuando el Coordinador le asigna una revisión pendiente."
                  : "Aquí ve las solicitudes que aún requieren su revisión, delegación o emisión de informe."}
              </p>
            </div>
          </div>

          {!cargandoPendientes && total > 0 && (
            <div className="mt-4">
              <p className="mb-1 fw-semibold small">
                {etiquetaSolicitud(primera)} · {primera.carreraOrigen || "Origen"} → {primera.carreraDestino || "Destino"}
              </p>
              <Link to={rutaSolicitud(primera)} className="btn btn-primary btn-sm w-100">
                {esDocente ? "Revisar ahora" : "Ver solicitud"}
              </Link>
              {total > 1 && (
                <ul className="mt-3 mb-0 small">
                  {pendientes.slice(1, 6).map((s) => (
                    <li key={s.solicitudId}>
                      <Link to={rutaSolicitud(s)}>{etiquetaSolicitud(s)}</Link>{" "}
                      · pendiente de revisión
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
      </div>
    );

    if (esDocente) {
      const formatoFecha = (valor) => (valor ? new Date(valor).toLocaleDateString("es-EC") : "-");
      const YA_REVISADO = ["YA_REVISADO", "REVISADO"];

      return (
        <div className="container mt-4">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center gap-2">
              <div>
                <h2 className="mb-0 fw-bold">Revisión docente</h2>
                <small className="d-block fw-normal">
                  Genere la sugerencia, revísela y envíe manualmente el informe al Coordinador.
                </small>
              </div>
              <Link to="/solicitudes-docente" className="btn btn-light btn-sm fw-bold">
                Ver todas
              </Link>
            </div>
            <div className="card-body p-4">
              {!cargandoPendientes && pendientes.length > 0 && (
                <div className="alert alert-warning">
                  Tienes {pendientes.length} solicitud{pendientes.length === 1 ? "" : "es"} pendiente{pendientes.length === 1 ? "" : "s"} de revisar. La primera es{" "}
                  <Link to={`/evaluar/${pendientes[0].solicitudId}`} className="fw-bold">
                    {numeroSolicitud(pendientes[0])}
                  </Link>.
                </div>
              )}

              <div className="table-responsive">
                <table className="table table-hover align-middle">
                  <thead className="table-light">
                    <tr>
                      <th>Solicitud</th>
                      <th>Coordinador</th>
                      <th>Titular (récord)</th>
                      <th>Modalidad</th>
                      <th>Carreras</th>
                      <th>Estado</th>
                      <th className="text-center">Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    {cargandoPendientes ? (
                      <tr>
                        <td colSpan="7" className="text-center py-4">Cargando solicitudes...</td>
                      </tr>
                    ) : solicitudesDocente.length === 0 ? (
                      <tr>
                        <td colSpan="7" className="text-center py-4">No hay solicitudes asignadas para revisión.</td>
                      </tr>
                    ) : (
                      solicitudesDocente.map((s) => {
                        const id = s.solicitudId;
                        const revisado = YA_REVISADO.includes(s.estadoDelegacion);
                        const borrador = !revisado && Boolean(s.tieneSugerencia || s.fechaSugerencia);
                        const puedeEvaluar = s.puedeEvaluar !== false && !revisado;
                        return (
                          <tr key={id}>
                            <td>
                              <div className="fw-bold">{s.numeroProceso || `SOL-${String(id).padStart(4, "0")}`}</div>
                              <div className="small text-muted">{formatoFecha(s.fecha)}</div>
                            </td>
                            <td>{s.coordinadorCorreo || s.comiteCorreo || "-"}</td>
                            <td>
                              <span className="fw-semibold">{s.estudiante || "-"}</span>
                              <div className="small text-muted">C.I.: {s.estudianteCedula || "-"}</div>
                            </td>
                            <td>
                              <span className={`badge ${s.modulo === "EXTERNO" ? "bg-info text-dark" : "bg-primary"}`}>
                                {s.modulo || "INTERNO"}
                              </span>
                            </td>
                            <td>
                              {s.carreraOrigen || "-"} → {s.carreraDestino || "-"}
                              <div className="small text-muted">{s.totalAsignaturas ?? 0} asignatura(s)</div>
                            </td>
                            <td>
                              {revisado ? (
                                <span className="badge bg-secondary">Ya revisado</span>
                              ) : borrador ? (
                                <span className="badge bg-info text-dark">Sugerencia generada · sin enviar</span>
                              ) : (
                                <span className="badge bg-warning text-dark">{s.estadoDelegacion || "PENDIENTE"}</span>
                              )}
                            </td>
                            <td className="text-center">
                              {puedeEvaluar ? (
                                <Link to={`/evaluar/${id}`} className="btn btn-sm btn-primary fw-bold">
                                  {borrador ? "Continuar" : "Revisar"}
                                </Link>
                              ) : (
                                <Link to={`/evaluar/${id}`} className="btn btn-sm btn-outline-secondary">
                                  Ver
                                </Link>
                              )}
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
        </div>
      );
    }
  }

  return (
    <div className="container mt-4">
      <div className="card shadow-sm p-4 border-0">
        <h2 className="mb-4">¡Bienvenido!</h2>

        <div className="mb-3">
          <label className="fw-bold">Nombre:</label>
          <p>{usuario?.nombre || "No registrado"}</p>
        </div>

        <div className="mb-3">
          <label className="fw-bold">Correo:</label>
          <p>{usuario?.correo || "No registrado"}</p>
        </div>

        <div className="mb-3">
          <label className="fw-bold">Rol:</label>
          <p>{rol || "No registrado"}</p>
        </div>
      </div>
    </div>
  );
}
