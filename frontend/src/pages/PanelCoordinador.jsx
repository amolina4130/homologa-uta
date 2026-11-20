import { useState, useEffect, useCallback } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesPendientes, obtenerDetalleSolicitud, emitirInforme } from "../services/coordinadorService";
import { descargarInformePDF } from "../services/homologacionService"; 
import ListaSolicitudes from "../components/ListaSolicitudes";
import DetalleSolicitud from "../components/DetalleSolicitud";

export default function PanelCoordinador() {
  const { rol } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudSeleccionada, setSolicitudSeleccionada] = useState(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  const cargarPendientes = useCallback(async () => {
    setCargando(true);
    setError("");
    try {
      const data = await obtenerSolicitudesPendientes();
      setSolicitudes(data);
    } catch (e) {
      setError(e.message || "No se pudieron cargar las solicitudes pendientes.");
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    if (rol !== "COORDINADOR") return;
    const timer = setTimeout(() => cargarPendientes(), 0);
    return () => clearTimeout(timer);
  }, [rol, cargarPendientes]);

  const abrirDetalle = async (id) => {
    setCargando(true); setError(""); setExito("");
    try {
      const detalle = await obtenerDetalleSolicitud(id);
      setSolicitudSeleccionada(detalle);
    } catch (e) {
      setError(e.message || "Error al cargar el detalle.");
    } finally {
      setCargando(false);
    }
  };

  const guardarInforme = async (informeArmado) => {
    if (!solicitudSeleccionada) return;
    const confirmar = window.confirm("¿Está seguro de emitir el informe técnico?");
    if (!confirmar) return;

    setCargando(true); setError(""); setExito("");
    try {
      const resultado = await emitirInforme(solicitudSeleccionada.solicitudId, informeArmado);
      setExito(resultado?.estadoSolicitudActualizado === false ? "Informe guardado." : "Informe emitido correctamente.");
      setSolicitudSeleccionada(null);
      await cargarPendientes();
    } catch (e) {
      setError(e.message || "Error al emitir el informe.");
    } finally {
      setCargando(false);
    }
  };

  // Función para descargar el PDF
  const descargarPDF = async () => {
    if (!solicitudSeleccionada) return;
    try {
      await descargarInformePDF(
        solicitudSeleccionada.solicitudId, 
        solicitudSeleccionada.numeroProceso || `SOL-${solicitudSeleccionada.solicitudId}`
      );
    } catch (error) {
      setError(error.message);
    }
  };

  if (rol !== "COORDINADOR") {
    return <div className="container mt-4"><div className="alert alert-danger">Acceso denegado.</div></div>;
  }

  return (
    <div className="container mt-4">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="mb-0">PANEL DEL COORDINADOR</h2>
        <button className="btn btn-outline-primary btn-sm" onClick={cargarPendientes} disabled={cargando}>
          Actualizar lista
        </button>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {exito && <div className="alert alert-success">{exito}</div>}

      {!solicitudSeleccionada ? (
        <ListaSolicitudes solicitudes={solicitudes} cargando={cargando} onAbrir={abrirDetalle} />
      ) : (
        <>
          {/*Botón de descarga para el coordinador */}
          <div className="d-flex justify-content-end mb-2">
            <button className="btn btn-success fw-bold" onClick={descargarPDF}>
            </button>
          </div>

          <DetalleSolicitud
            key={solicitudSeleccionada.solicitudId}
            solicitud={solicitudSeleccionada}
            onVolver={() => { setSolicitudSeleccionada(null); setError(""); }}
            onGuardar={guardarInforme}
            cargando={cargando}
          />
        </>
      )}
    </div>
  );
}