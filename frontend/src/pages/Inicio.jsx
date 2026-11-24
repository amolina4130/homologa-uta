import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesDocente } from "../services/docenteService";
import { filtrarPendientes, numeroSolicitud } from "../utils/solicitudesDocente";

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
  const [perfil, setPerfil] = useState(() => perfilInicial(usuario));
  const [guardandoPerfil, setGuardandoPerfil] = useState(false);
  const [mensajePerfil, setMensajePerfil] = useState("");
  const [errorPerfil, setErrorPerfil] = useState("");

  useEffect(() => {
    if (rol !== "DOCENTE") return undefined;

    let activo = true;
    obtenerSolicitudesDocente()
      .then((lista) => {
        if (activo) setPendientes(filtrarPendientes(lista));
      })
      .catch(() => {
        if (activo) setPendientes([]);
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

  if (rol === "DOCENTE") {
    const primera = pendientes[0];
    const total = pendientes.length;
    const iniciales = (perfil.nombre || usuario?.correo || "D")
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((parte) => parte[0]?.toUpperCase() || "")
      .join("");

    return (
      <div className="container mt-4">
        <div className="row g-4">
          <div className="col-lg-8">
            <form className="card shadow-sm border-0 h-100" onSubmit={guardarDatos}>
              <div className="card-header bg-primary text-white">
                <h2 className="mb-0 fw-bold">Mi perfil</h2>
                <small className="d-block fw-normal">
                  Sus datos se usan para identificarlo en el sistema y en sus informes.
                </small>
              </div>
              <div className="card-body p-4">
                <div className="d-flex align-items-center gap-3 mb-4">
                  <div
                    className="rounded-circle bg-primary text-white d-flex align-items-center justify-content-center fw-bold fs-4"
                    style={{ width: "64px", height: "64px", flex: "0 0 auto" }}
                    aria-hidden="true"
                  >
                    {iniciales || "D"}
                  </div>
                  <div>
                    <div className="fw-bold fs-5">
                      {perfil.nombre || usuario?.correo || "Docente"}
                    </div>
                    <div className="text-muted small">
                      {[perfil.titulo, perfil.tipo].filter(Boolean).join(" · ") || "Datos del docente"}
                    </div>
                    <div className="text-muted small">{usuario?.correo || "-"}</div>
                  </div>
                </div>

                <div className="row g-3">
                  {CAMPOS_PERFIL.map((campo) => (
                    <div className="col-md-6" key={campo.clave}>
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
                  <div className="col-md-6">
                    <label className="form-label small fw-bold mb-1">Correo de acceso</label>
                    <input
                      className="form-control form-control-sm"
                      value={usuario?.correo || ""}
                      readOnly
                      disabled
                    />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label small fw-bold mb-1">Rol</label>
                    <input
                      className="form-control form-control-sm"
                      value={rol || ""}
                      readOnly
                      disabled
                    />
                  </div>
                </div>

                {mensajePerfil && (
                  <div className="alert alert-success small mt-3 mb-0">{mensajePerfil}</div>
                )}
                {errorPerfil && (
                  <div className="alert alert-danger small mt-3 mb-0">{errorPerfil}</div>
                )}

                <div className="d-flex justify-content-end mt-4">
                  <button
                    type="submit"
                    className="btn btn-primary fw-bold"
                    disabled={guardandoPerfil}
                  >
                    {guardandoPerfil ? "Guardando..." : "Guardar mis datos"}
                  </button>
                </div>
              </div>
            </form>
          </div>

          <div className="col-lg-4">
            <div className="card shadow-sm border-0 h-100">
              <div className="card-header bg-light fw-bold">Notificaciones</div>
              <div className="card-body p-4">
                <div className="d-flex align-items-start gap-3">
                  <div className="position-relative" style={{ fontSize: "2.5rem", lineHeight: 1 }}>
                    <span aria-hidden="true">🔔</span>
                    {!cargandoPendientes && total > 0 && (
                      <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">
                        {total > 9 ? "9+" : total}
                        <span className="visually-hidden">
                          solicitudes pendientes de revisar
                        </span>
                      </span>
                    )}
                  </div>
                  <div>
                    <h5 className="fw-bold mb-1">
                      {cargandoPendientes
                        ? "Consultando avisos..."
                        : total > 0
                          ? `Tienes ${total} solicitud${total === 1 ? "" : "es"} por revisar`
                          : "Sin solicitudes por revisar"}
                    </h5>
                    <p className="text-muted small mb-0">
                      La campanita muestra un número solo cuando el Coordinador le asigna una
                      revisión pendiente.
                    </p>
                  </div>
                </div>

                {!cargandoPendientes && total > 0 && (
                  <div className="mt-4">
                    <p className="mb-1 fw-semibold small">
                      {numeroSolicitud(primera)} ·{" "}
                      {primera.carreraOrigen || "Origen"} →{" "}
                      {primera.carreraDestino || "Destino"}
                    </p>
                    <Link to={`/evaluar/${primera.solicitudId}`} className="btn btn-primary btn-sm w-100">
                      Revisar ahora
                    </Link>
                    {total > 1 && (
                      <ul className="mt-3 mb-0 small">
                        {pendientes.slice(1).map((s) => (
                          <li key={s.solicitudId}>
                            <Link to={`/evaluar/${s.solicitudId}`}>
                              {numeroSolicitud(s)}
                            </Link>{" "}
                            · pendiente de revisar
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="card shadow-sm border-0 mt-4">
          <div className="card-body d-flex flex-wrap justify-content-between align-items-center gap-3">
            <div>
              <h5 className="fw-bold mb-1">Revisión por el Docente</h5>
              <p className="text-muted small mb-0">
                Genere la sugerencia con apoyo de IA, ponga sus observaciones, marque
                <strong> REVISADO</strong> y envíe manualmente su informe al Coordinador. La URL y
                los documentos ya están guardados en la solicitud.
              </p>
            </div>
            <Link to="/solicitudes-docente" className="btn btn-outline-primary">
              Abrir mis revisiones
            </Link>
          </div>
        </div>
      </div>
    );
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
