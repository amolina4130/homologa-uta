import { useEffect, useState } from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import NotificacionesDocente from "./NotificacionesDocente";
import NotificacionesCoordinador from "./NotificacionesCoordinador";

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
  adjuntar: "/adjuntar-documentos",
  url: "/ruta-carpeta",
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

const ITEMS = {
  inicio: { etiqueta: "Inicio", ruta: RUTAS.inicio },
  nueva: { etiqueta: "Nueva solicitud", ruta: RUTAS.nueva },
  mis: { etiqueta: "Mis solicitudes", ruta: RUTAS.mis },
  adjuntar: { etiqueta: "Adjuntar documentos", ruta: RUTAS.adjuntar },
  url: { etiqueta: "Ruta / URL externa", ruta: RUTAS.url },
  evaluar: {
    etiqueta: "Revisión por el Docente",
    ruta: RUTAS.evaluar,
  },
  delegar: { etiqueta: "Delegar docentes", ruta: RUTAS.delegar },
  sugeridos: { etiqueta: "Informes sugeridos", ruta: RUTAS.sugeridos },
  final: { etiqueta: "Generar informe final", ruta: RUTAS.final },
  carreras: { etiqueta: "Carreras y mallas", ruta: RUTAS.carreras },
  estado: { etiqueta: "Estado del sistema", ruta: RUTAS.estado },
  reglas: { etiqueta: "Reglas", ruta: RUTAS.reglas },
  equivalencias: { etiqueta: "Equivalencias", ruta: RUTAS.equivalencias },
  memoria: { etiqueta: "Memoria del agente", ruta: RUTAS.memoria },
};

const MENUS = {
  COMITE: [
    { tipo: "link", clave: "inicio" },
    { tipo: "grupo", etiqueta: "Solicitudes", claves: ["nueva", "mis", "adjuntar", "url"] },
    { tipo: "link", clave: "carreras" },
  ],
  COORDINADOR: [
    { tipo: "link", clave: "inicio" },
    { tipo: "grupo", etiqueta: "Solicitudes", claves: ["nueva", "adjuntar", "url"] },
    {
      tipo: "grupo",
      etiqueta: "Gestión",
      claves: ["delegar", "sugeridos", "final"],
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
    setPerfil(perfilInicial(usuario));
  }, [usuario]);

  useEffect(() => {
    const cerrarAlClicExterior = (evento) => {
      const dentroDelMenu =
        evento.target instanceof Element && evento.target.closest(".navbar");
      if (!dentroDelMenu) {
        setMenuAbierto(false);
        setGrupoAbierto(null);
      }
    };

    document.addEventListener("click", cerrarAlClicExterior);
    return () => document.removeEventListener("click", cerrarAlClicExterior);
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
    const clases = enDropdown
      ? `dropdown-item ${activo ? "active" : ""}`
      : `nav-link ${activo ? "active fw-bold text-white" : "text-white-50"}`;

    return (
      <Link
        key={clave}
        to={item.ruta}
        className={clases}
        aria-current={activo ? "page" : undefined}
        onClick={cerrarAlNavegar}
      >
        {item.etiqueta}
      </Link>
    );
  };

  const entradas = MENUS[rol] || MENUS.COMITE;

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
                (clave) => location.pathname === ITEMS[clave].ruta
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
                  <ul className={`dropdown-menu ${abierto ? "show" : ""}`}>
                    {entrada.claves.map((clave) => (
                      <li key={clave}>{renderEnlace(clave, true)}</li>
                    ))}
                  </ul>
                </li>
              );
            })}
          </ul>

          <div className="d-flex align-items-center gap-3">
            <span className="text-white-50 small d-none d-md-inline">
              Rol: {rol}
            </span>
            {rol === "DOCENTE" && <NotificacionesDocente />}
            {rol === "COORDINADOR" && <NotificacionesCoordinador />}
            {(rol === "COORDINADOR" || rol === "DOCENTE") && (
              <button
                type="button"
                className="btn btn-outline-light btn-sm"
                onClick={abrirPerfil}
                title="Ver mi perfil"
              >
                Mi perfil
              </button>
            )}
            <button onClick={handleLogout} className="btn btn-outline-light btn-sm">
              Cerrar sesión
            </button>
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
