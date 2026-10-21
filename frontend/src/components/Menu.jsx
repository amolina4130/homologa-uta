import { useAuth } from "../context/AuthContext";

export default function Menu() {
  const { rol, cerrarSesion } = useAuth();

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
        {misOpciones.map((opcion) => (
          <span key={opcion} style={{ marginLeft: "15px" }}>
            {/* Por ahora los enlaces no van a ningún lado porque esas pantallas no existen */}
            <a href="#" style={{ textDecoration: "none", color: "black" }}>{opcion}</a>
          </span>
        ))}
      </div>
      <button onClick={cerrarSesion} style={{ cursor: "pointer" }}>
        Cerrar sesión
      </button>
    </nav>
  );
}