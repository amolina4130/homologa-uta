import { useState } from "react";
import { useNavigate } from "react-router-dom";
import FormularioSolicitud from "../components/FormularioSolicitud";
import { crearSolicitud } from "../services/solicitudesService";

export default function NuevaSolicitud() {
  const [cargando, setCargando] = useState(false);
  const [errorServer, setErrorServer] = useState("");
  const navigate = useNavigate();

  const handleRegistrar = async (datosFormulario) => {
    setCargando(true);
    setErrorServer("");
    try {
      await crearSolicitud(datosFormulario);
      // Si todo sale bien, redirigimos a Mis Solicitudes
      navigate("/mis-solicitudes");
    } catch {
      setErrorServer("No se pudo registrar la solicitud. Verifique la conexión o intente más tarde.");
    } finally {
      setCargando(false);
    }
  };

  return (
    <div style={{ maxWidth: 900, margin: "40px auto", fontFamily: "sans-serif" }}>
      <h2>Registrar nueva solicitud</h2>
      {errorServer && <p style={{ color: "red", fontWeight: "bold" }}>{errorServer}</p>}
      <FormularioSolicitud onSubmit={handleRegistrar} cargando={cargando} />
    </div>
  );
}