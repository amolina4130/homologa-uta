import { useEffect, useState } from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import NotificacionesDocente from "./NotificacionesDocente";
import NotificacionesCoordinador from "./NotificacionesCoordinador";
import "./Menu.css";

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

const RUTAS = {
  inicio: "/inicio",
  nueva: "/nueva-solicitud",
  mis: "/mis-solicitudes",
  evaluar: "/solicitudes-docente",
  delegar: "/delegar-docentes",
  sugeridos: "/informes-sugeridos",
  final: "/informe-final",
  carreras: "/carreras",
  estado: "/estado",
  reglas: "/reglas",
  equivalencias: "/equivalencias",
  memoria: "/memoria",
};

// `desc` aparece debajo del nombre en los menús desplegables; `paso` numera las
// etapas del proceso de revisión en el orden en que se hacen.
const ITEMS = {
  inicio: { etiqueta: "Inicio", ruta: RUTAS.inicio },
  nueva: { etiqueta: "Nueva solicitud", desc: "Datos, récord y envío a revisión, paso a paso", ruta: RUTAS.nueva },
  mis: { etiqueta: "Mis solicitudes", desc: "Ver el avance de las solicitudes registradas", ruta: RUTAS.mis },
  evaluar: { etiqueta: "Revisión por el Docente", ruta: RUTAS.evaluar },
  delegar: { etiqueta: "Delegar a docentes", desc: "Asignar las solicitudes pendientes de revisión", ruta: RUTAS.delegar, paso: 1 },
  sugeridos: { etiqueta: "Sugerencias de docentes", desc: "Revisar lo que propusieron los docentes", ruta: RUTAS.sugeridos, paso: 2 },
  final: { etiqueta: "Informe final", desc: "Emitirlo y enviarlo al Comité (cierra el trámite)", ruta: RUTAS.final, paso: 3 },
  carreras: { etiqueta: "Carreras y mallas", ruta: RUTAS.carreras },
  estado: { etiqueta: "Estado del sistema", desc: "Disponibilidad de los servicios", ruta: RUTAS.estado },
  reglas: { etiqueta: "Reglas", desc: "Reglas de negocio de la homologación", ruta: RUTAS.reglas },
  equivalencias: { etiqueta: "Equivalencias", desc: "Asignaturas equivalentes entre carreras", ruta: RUTAS.equivalencias },
  memoria: { etiqueta: "Memoria del agente", desc: "Estado del conocimiento del agente de IA", ruta: RUTAS.memoria },
};

const MENUS = {
  COMITE: [
    { tipo: "link", clave: "inicio" },
    // Los documentos se cargan dentro del asistente de "Nueva solicitud".
    { tipo: "grupo", etiqueta: "Solicitudes", claves: ["nueva", "mis"] },
    { tipo: "link", clave: "carreras" },
  ],
  // Ordenado como trabaja el coordinador: registra la solicitud (el asistente incluye los
  // documentos) y luego la lleva por la revisión (delegar → sugerencias → informe final).
  COORDINADOR: [
    { tipo: "link", clave: "inicio" },
    { tipo: "link", clave: "nueva" },
    {
      tipo: "grupo",
      etiqueta: "Revisión",
      claves: [{ encabezado: "Pasos de la revisión" }, "delegar", "sugeridos", "final"],
    },
    { tipo: "link", clave: "carreras" },
  ],
  DOCENTE: [
    { tipo: "link", clave: "inicio" },
    { tipo: "link", clave: "evaluar" },
  ],
  ADMINISTRADOR: [
    { tipo: "link", clave: "inicio" },
    {
      tipo: "grupo",
      etiqueta: "Gestión",
      claves: ["estado", "reglas", "equivalencias", "memoria"],
    },
    { tipo: "link", clave: "carreras" },
  ],
};

const NOMBRE_ROL = {
  COMITE: "Comité",
  COORDINADOR: "Coordinador",
  DOCENTE: "Docente",
  ADMINISTRADOR: "Administrador",
};

// Iniciales para el avatar, sin contar títulos como "Ing." o "Mg.".
const inicialesDe = (usuario) => {
  const palabras = (usuario?.nombre || "")
    .trim()
    .split(/\s+/)
    .filter((p) => p && !p.endsWith("."));
  if (palabras.length) return palabras.slice(0, 2).map((p) => p[0].toUpperCase()).join("");
  return (usuario?.correo || "?")[0].toUpperCase();
};

// En los grupos, una cadena es una opción del menú y un objeto { encabezado } un subtítulo.
const esClave = (c) => typeof c === "string";

// Clave del menú de la cuenta (avatar); comparte el estado de "grupo abierto".
const CLAVE_CUENTA = "__cuenta";

export default function Menu() {
  const { rol, usuario, guardarPerfil, cerrarSesion } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuAbierto, setMenuAbierto] = useState(false);
  const [grupoAbierto, setGrupoAbierto] = useState(null);
  const [perfilAbierto, setPerfilAbierto] = useState(false);
  const [perfil, setPerfil] = useState(() => perfilInicial(usuario));
  const [guardandoPerfil, setGuardandoPerfil] = useState(false);
  const [mensajePerfil, setMensajePerfil] = useState("");
  const [errorPerfil, setErrorPerfil] = useState("");

  useEffect(() => {
    const cerrarAlClicExterior = (evento) => {
      const dentroDelMenu =
        evento.target instanceof Element && evento.target.closest(".navbar");
      if (!dentroDelMenu) {
        setMenuAbierto(false);
        setGrupoAbierto(null);
      }
    };

    const cerrarConEscape = (evento) => {
      if (evento.key === "Escape") setGrupoAbierto(null);
    };

    document.addEventListener("click", cerrarAlClicExterior);
    document.addEventListener("keydown", cerrarConEscape);
    return () => {
      document.removeEventListener("click", cerrarAlClicExterior);
      document.removeEventListener("keydown", cerrarConEscape);
    };
  }, []);

  const handleLogout = () => {
    cerrarSesion();
    navigate("/login");
  };

  const cerrarAlNavegar = () => {
    setMenuAbierto(false);
    setGrupoAbierto(null);
  };

  const abrirPerfil = () => {
    setPerfil(perfilInicial(usuario));
    setMensajePerfil("");
    setErrorPerfil("");
    setPerfilAbierto(true);
    setMenuAbierto(false);
    setGrupoAbierto(null);
  };

  const cambiarPerfil = (clave, valor) => {
    setPerfil((prev) => ({ ...prev, [clave]: valor }));
    setMensajePerfil("");
    setErrorPerfil("");
  };

  const guardarDatosPerfil = async (evento) => {
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

  const renderEnlace = (clave, enDropdown = false) => {
    const item = ITEMS[clave];
    const activo = location.pathname === item.ruta;
    if (enDropdown) {
      return (
        <Link
          key={clave}
          to={item.ruta}
          className={`dropdown-item menu-opcion ${activo ? "active" : ""}`}
          aria-current={activo ? "page" : undefined}
          onClick={cerrarAlNavegar}
        >
          {item.paso && <span className="menu-paso" aria-hidden="true">{item.paso}</span>}
          <span>
            <span className="menu-opcion-titulo d-block">{item.etiqueta}</span>
            {item.desc && <span className="menu-opcion-desc d-block">{item.desc}</span>}
          </span>
        </Link>
      );
    }

    return (
      <Link
        key={clave}
        to={item.ruta}
        className={`nav-link ${activo ? "active fw-bold text-white" : "text-white-50"}`}
        aria-current={activo ? "page" : undefined}
        onClick={cerrarAlNavegar}
      >
        {item.etiqueta}
      </Link>
    );
  };

  const entradas = MENUS[rol] || MENUS.COMITE;
  const puedeEditarPerfil = rol === "COORDINADOR" || rol === "DOCENTE";

  return (
    <nav className="navbar navbar-expand-lg navbar-dark bg-primary px-4 shadow-sm mb-4">
      <div className="container-fluid">
        <Link
          to="/inicio"
          className="navbar-brand fw-bold text-white mb-0 h1"
          onClick={cerrarAlNavegar}
        >
          HomologaUTA
        </Link>

        <button
          className="navbar-toggler"
          type="button"
          aria-controls="menuPrincipal"
          aria-expanded={menuAbierto}
          aria-label="Alternar navegación"
          onClick={() => setMenuAbierto((abierto) => !abierto)}
        >
          <span className="navbar-toggler-icon" />
        </button>

        <div
          id="menuPrincipal"
          className={`collapse navbar-collapse ${menuAbierto ? "show" : ""}`}
        >
          <ul className="navbar-nav me-auto mb-2 mb-lg-0">
            {entradas.map((entrada) => {
              if (entrada.tipo === "link") {
                return (
                  <li className="nav-item" key={entrada.clave}>
                    {renderEnlace(entrada.clave)}
                  </li>
                );
              }

              const abierto = grupoAbierto === entrada.etiqueta;
              const grupoActivo = entrada.claves.some(
                (clave) => esClave(clave) && location.pathname === ITEMS[clave].ruta
              );

              return (
                <li
                  className={`nav-item dropdown ${abierto ? "show" : ""}`}
                  key={entrada.etiqueta}
                >
                  <button
                    type="button"
                    className={`nav-link dropdown-toggle bg-transparent border-0 ${
                      grupoActivo ? "text-white fw-bold" : "text-white-50"
                    }`}
                    aria-haspopup="true"
                    aria-expanded={abierto}
                    onClick={() =>
                      setGrupoAbierto(abierto ? null : entrada.etiqueta)
                    }
                  >
                    {entrada.etiqueta}
                  </button>
                  <ul className={`dropdown-menu menu-desplegable ${abierto ? "show" : ""}`}>
                    {entrada.claves.map((clave) =>
                      esClave(clave) ? (
                        <li key={clave}>{renderEnlace(clave, true)}</li>
                      ) : (
                        <li key={clave.encabezado}>
                          <h6 className="dropdown-header">{clave.encabezado}</h6>
                        </li>
                      )
                    )}
                  </ul>
                </li>
              );
            })}
          </ul>

          <div className="barra-derecha d-flex align-items-center gap-2 pb-2 pb-lg-0">
            {rol === "DOCENTE" && <NotificacionesDocente />}
            {rol === "COORDINADOR" && <NotificacionesCoordinador />}

            <div className="dropdown">
              <button
                type="button"
                className="boton-circulo avatar"
                aria-haspopup="true"
                aria-expanded={grupoAbierto === CLAVE_CUENTA}
                aria-label="Mi cuenta"
                title={usuario?.nombre || usuario?.correo || "Mi cuenta"}
                onClick={() => setGrupoAbierto(grupoAbierto === CLAVE_CUENTA ? null : CLAVE_CUENTA)}
              >
                {inicialesDe(usuario)}
              </button>

              {grupoAbierto === CLAVE_CUENTA && (
                <div className="dropdown-menu menu-desplegable menu-derecha show" style={{ width: 300 }}>
                  <div className="d-flex align-items-center gap-3 px-2 py-2">
                    <span className="boton-circulo avatar-grande" aria-hidden="true">{inicialesDe(usuario)}</span>
                    <div className="text-truncate">
                      <div className="fw-bold text-truncate">{usuario?.nombre || "Sin nombre registrado"}</div>
                      <div className="small text-muted text-truncate">{usuario?.correo}</div>
                      <span className="badge rounded-pill bg-primary-subtle text-primary-emphasis mt-1">
                        {NOMBRE_ROL[rol] || rol}
                      </span>
                    </div>
                  </div>
                  <hr className="dropdown-divider" />
                  {puedeEditarPerfil && (
                    <button type="button" className="dropdown-item menu-opcion" onClick={abrirPerfil}>
                      <span>
                        <span className="menu-opcion-titulo d-block">Mi perfil</span>
                        <span className="menu-opcion-desc d-block">Nombre, título y datos para los informes</span>
                      </span>
                    </button>
                  )}
                  <button type="button" className="dropdown-item menu-opcion text-danger" onClick={handleLogout}>
                    <span className="menu-opcion-titulo">Cerrar sesión</span>
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {perfilAbierto && (
        <div
          className="modal d-block"
          tabIndex="-1"
          role="dialog"
          aria-modal="true"
          aria-label="Mi perfil"
          style={{ backgroundColor: "rgba(0,0,0,0.55)" }}
          onClick={() => setPerfilAbierto(false)}
        >
          <div
            className="modal-dialog modal-lg modal-dialog-centered"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-content">
              <div className="modal-header bg-primary text-white">
                <div>
                  <h5 className="modal-title mb-0">Mi perfil</h5>
                  <small>Sus datos se usan para identificarlo en el sistema y en los informes.</small>
                </div>
                <button
                  type="button"
                  className="btn-close btn-close-white"
                  aria-label="Cerrar"
                  onClick={() => setPerfilAbierto(false)}
                />
              </div>
              <form onSubmit={guardarDatosPerfil}>
                <div className="modal-body">
                  <div className="row g-3">
                    {CAMPOS_PERFIL.map((campo) => (
                      <div className="col-md-6" key={campo.clave}>
                        <label htmlFor={`menu-perfil-${campo.clave}`} className="form-label small fw-bold mb-1">
                          {campo.etiqueta}
                        </label>
                        <input
                          id={`menu-perfil-${campo.clave}`}
                          className="form-control form-control-sm"
                          value={perfil[campo.clave] || ""}
                          placeholder={campo.placeholder}
                          onChange={(e) => cambiarPerfil(campo.clave, e.target.value)}
                        />
                      </div>
                    ))}
                    <div className="col-md-6">
                      <label className="form-label small fw-bold mb-1">Correo de acceso</label>
                      <input className="form-control form-control-sm" value={usuario?.correo || ""} readOnly disabled />
                    </div>
                    <div className="col-md-6">
                      <label className="form-label small fw-bold mb-1">Rol</label>
                      <input className="form-control form-control-sm" value={rol || ""} readOnly disabled />
                    </div>
                  </div>

                  {mensajePerfil && <div className="alert alert-success small mt-3 mb-0">{mensajePerfil}</div>}
                  {errorPerfil && <div className="alert alert-danger small mt-3 mb-0">{errorPerfil}</div>}
                </div>
                <div className="modal-footer">
                  <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => setPerfilAbierto(false)}>
                    Cerrar
                  </button>
                  <button type="submit" className="btn btn-primary btn-sm fw-bold" disabled={guardandoPerfil}>
                    {guardandoPerfil ? "Guardando..." : "Guardar mis datos"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </nav>
  );
}
