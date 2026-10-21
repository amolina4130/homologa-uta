import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function Menu() {
  const { rol, cerrarSesion } = useAuth();
  const navigate = useNavigate();

  const handleCerrarSesion = () => {
    cerrarSesion();
    navigate("/login");
  };

  // Mapeo de opciones a sus rutas reales
  const enlaces = {
    "Inicio": "/inicio",
    "Nueva solicitud": "/nueva-solicitud",
    "Estado del sistema": "/estado",
    // Las demás rutas se agregarán en próximas tareas
  };

  // Opciones del menú según el rol (basado en la nota del M-02)
  const opciones = {
    ESTUDIANTE: ["Inicio", "Nueva solicitud", "Mis solicitudes", "Carreras y mallas"],
    COORDINADOR: ["Inicio", "Solicitudes pendientes", "Carreras y mallas"],
    SECRETARIA: ["Inicio", "Solicitudes", "Carreras y mallas"],
    CONSEJO: ["Inicio", "Resoluciones", "Carreras y mallas"],
    ADMINISTRADOR: ["Inicio", "Estado del sistema", "Carreras y mallas"],
  };

  const misOpciones = opciones[rol] || [];

  return (
    <nav style={{ padding: "10px", backgroundColor: "#eee", borderBottom: "2px solid #000", display: "flex", justifyContent: "space-between" }}>
      <div>
        <strong>Menú:</strong>
        {misOpciones.map((opcion) => {
          const ruta = enlaces[opcion] || "#"; // Si no tiene ruta, se queda en "#"
          return (
            <span key={opcion} style={{ marginLeft: "15px" }}>
              <Link to={ruta} style={{ textDecoration: "none", color: "black" }}>{opcion}</Link>
            </span>
          );
        })}
      </div>
      <button onClick={handleCerrarSesion} style={{ cursor: "pointer" }}>
        Cerrar sesión
      </button>
    </nav>
  );
}