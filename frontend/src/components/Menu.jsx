import { Link, useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function Menu() {
  const { rol, cerrarSesion } = useAuth();
  const navigate = useNavigate();
  const location = useLocation(); 

  const rutas = {
    "Inicio": "/inicio",
    "Nueva solicitud": "/nueva-solicitud",
    "Mis solicitudes": "/mis-solicitudes",
    "Adjuntar documentos": "/adjuntar-documentos",
    "Carreras y mallas": "/carreras",
    "Estado del sistema": "/estado",
    "Reglas": "/reglas",
    "Solicitudes pendientes": "/panel-coordinador"
  };

  const opciones = {
    ESTUDIANTE: ["Inicio", "Nueva solicitud", "Mis solicitudes", "Adjuntar documentos", "Carreras y mallas"],
    COORDINADOR: ["Inicio", "Solicitudes pendientes", "Carreras y mallas"],
    SECRETARIA: ["Inicio", "Solicitudes", "Carreras y mallas"],
    CONSEJO: ["Inicio", "Resoluciones", "Carreras y mallas"],
    ADMINISTRADOR: ["Inicio", "Estado del sistema", "Reglas", "Carreras y mallas"],
  };

  const misOpciones = opciones[rol] || [];

  const handleLogout = () => {
    cerrarSesion();
    navigate("/login");
  };  

  return (
    <nav className="navbar navbar-expand-lg navbar-dark bg-primary px-4 shadow-sm mb-4">
      <div className="container-fluid">
        <span className="navbar-brand fw-bold text-white">HomologaUTA</span>
        
        <div className="navbar-nav me-auto">
          {misOpciones.map((opcion) => {
            const ruta = rutas[opcion] || "/inicio";
            const isActive = location.pathname === ruta;
            
            return (
              <Link 
                key={opcion} 
                to={ruta} 
                className={`nav-link ${isActive ? 'active fw-bold text-white border-bottom border-2 border-white' : 'text-white-50'}`}
              >
                {opcion}
              </Link>
            );
          })}
        </div>

        <div className="d-flex align-items-center gap-3">
          <span className="text-white-50 small d-none d-md-inline">Rol: {rol}</span>
          <button onClick={handleLogout} className="btn btn-outline-light btn-sm">
            Cerrar sesión
          </button>
        </div>
      </div>
    </nav>
  );
}