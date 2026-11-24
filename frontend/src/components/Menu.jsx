import { useEffect, useState } from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import NotificacionesDocente from "./NotificacionesDocente";

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
    { tipo: "grupo", etiqueta: "Solicitudes", claves: ["nueva", "mis", "adjuntar", "url"] },
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
  const { rol, cerrarSesion } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuAbierto, setMenuAbierto] = useState(false);
  const [grupoAbierto, setGrupoAbierto] = useState(null);

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
            <button onClick={handleLogout} className="btn btn-outline-light btn-sm">
              Cerrar sesión
            </button>
          </div>
        </div>
      </div>
    </nav>
  );
}
