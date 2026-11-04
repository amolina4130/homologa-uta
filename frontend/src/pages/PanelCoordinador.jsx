import { useState, useEffect } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesPendientes, obtenerDetalleSolicitud, emitirInforme } from "../services/coordinadorService";
import ListaSolicitudes from "../components/ListaSolicitudes";
import DetalleSolicitud from "../components/DetalleSolicitud";

export default function PanelCoordinador() {
  const { rol } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudSeleccionada, setSolicitudSeleccionada] = useState(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  useEffect(() => {
    if (rol !== "COORDINADOR") return;

    const timer = setTimeout(() => {
      setCargando(true);
    }, 0);

    obtenerSolicitudesPendientes()
      .then((data) => setSolicitudes(data))
      .catch(() => setError("No se pudieron cargar las solicitudes pendientes."))
      .finally(() => setCargando(false));

    return () => clearTimeout(timer);
  }, [rol]);

  const abrirDetalle = async (id) => {
    setCargando(true);
    setError("");
    setExito("");
    try {
      const detalle = await obtenerDetalleSolicitud(id);
      setSolicitudSeleccionada(detalle);
    } catch {
      setError("Error al cargar el detalle.");
    } finally {
      setCargando(false);
    }
  };

  // Esta función ahora recibe el informe ya armado desde el componente hijo
  const guardarInforme = async (informeArmado) => {
    if (!solicitudSeleccionada) return;

    const confirmar = window.confirm("¿Está seguro de emitir el informe técnico?");
    if (!confirmar) return;

    setCargando(true);
    setError("");
    setExito("");

    try {
      // Llamamos al servicio enviando el ID de la solicitud y el arreglo
      await emitirInforme(solicitudSeleccionada.solicitudId, informeArmado);
      setExito("Informe técnico emitido correctamente.");
      setSolicitudSeleccionada(null);
      
      // Recargamos la lista
      try {
        const data = await obtenerSolicitudesPendientes();
        setSolicitudes(data);
      } catch {
        console.error("No se pudo recargar la lista.");
      }

    } catch (e) {
      setError("Error al emitir el informe: " + e.message);
    } finally {
      setCargando(false);
    }
  };

  if (rol !== "COORDINADOR") {
    return (
      <div className="container mt-4">
        <div className="alert alert-danger">Acceso denegado. Solo el Coordinador puede ver esta pantalla.</div>
      </div>
    );
  }

  return (
    <div className="container mt-4">
      <h2 className="mb-4">PANEL DEL COORDINADOR</h2>

      {error && <div className="alert alert-danger">{error}</div>}
      {exito && <div className="alert alert-success">{exito}</div>}

      {!solicitudSeleccionada ? (
        <ListaSolicitudes
          solicitudes={solicitudes}
          cargando={cargando}
          onAbrir={abrirDetalle}
        />
      ) : (
        <DetalleSolicitud
          solicitud={solicitudSeleccionada}
          onVolver={() => setSolicitudSeleccionada(null)}
          onGuardar={guardarInforme}
          cargando={cargando}
        />
      )}
    </div>
  );
}