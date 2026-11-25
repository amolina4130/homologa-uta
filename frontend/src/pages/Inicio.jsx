import { useAuth } from "../context/AuthContext";
import InicioCoordinador from "./InicioCoordinador";
import SolicitudesDocente from "./SolicitudesDocente";

const NOMBRE_ROL = {
  COMITE: "Comité",
  COORDINADOR: "Coordinador",
  DOCENTE: "Docente",
  ADMINISTRADOR: "Administrador",
};

// Cada rol entra a su propia pantalla de trabajo. Los datos personales se editan desde
// "Mi perfil" en el menú de la cuenta.
export default function Inicio() {
  const { rol, usuario } = useAuth();

  if (rol === "COORDINADOR") return <InicioCoordinador />;
  if (rol === "DOCENTE") return <SolicitudesDocente esInicio />;

  const nombre = usuario?.nombre?.trim();
  return (
    <div className="container pb-5">
      <p className="text-muted small text-uppercase fw-semibold mb-1">{NOMBRE_ROL[rol] || rol}</p>
      <h1 className="h3 fw-bold mb-1">{nombre ? `Hola, ${nombre}` : "Bienvenido"}</h1>
      <p className="text-muted mb-4">{usuario?.correo}</p>
      <div className="card border-0 shadow-sm">
        <div className="card-body p-4">
          <p className="mb-0">Use el menú superior para acceder a las opciones de su rol.</p>
        </div>
      </div>
    </div>
  );
}
