import { useState, useEffect, useCallback } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesPendientes, obtenerDetalleSolicitud, emitirInforme } from "../services/coordinadorService";
import ListaSolicitudes from "../components/ListaSolicitudes";
import DetalleSolicitud from "../components/DetalleSolicitud";
import FormularioResolucion from "../components/FormularioResolucion";

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
    setCargando(true);
    setError("");
    setExito("");
    try {
      const detalle = await obtenerDetalleSolicitud(id);
      setSolicitudSeleccionada(detalle);
    } catch (e) {
      setError(e.message || "Error al cargar el detalle de la solicitud.");
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
      setExito(resultado?.estadoSolicitudActualizado === false ? "Informe técnico guardado." : "Informe técnico emitido correctamente.");
      setSolicitudSeleccionada(null);
      await cargarPendientes();
    } catch (e) {
      setError(e.message || "Error al emitir el informe.");
    } finally {
      setCargando(false);
    }
  };

  const registrarResolucion = async (datosResolucion) => {
    try {
      
      console.log("Datos a enviar al backend:", datosResolucion);
      
      if (solicitudSeleccionada.estado !== "INFORME_EMITIDO") {
        throw new Error("No se puede registrar la resolución: no existe informe técnico previo (Error 409).");
      }

      setExito("Resolución registrada exitosamente.");
      setSolicitudSeleccionada(null); 
      await cargarPendientes();
    } catch (err) {
      throw err; 
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
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="mb-0">PANEL DEL COORDINADOR</h2>
        <button className="btn btn-outline-primary btn-sm" onClick={cargarPendientes} disabled={cargando}>
          Actualizar lista
        </button>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {exito && <div className="alert alert-success">{exito}</div>}

      {!solicitudSeleccionada ? (
        <ListaSolicitudes
          solicitudes={solicitudes}
          cargando={cargando}
          onAbrir={abrirDetalle}
        />
      ) : (
        
        <>
          <DetalleSolicitud
            key={solicitudSeleccionada.solicitudId}
            solicitud={solicitudSeleccionada}
            onVolver={() => {
              setSolicitudSeleccionada(null);
              setError("");
            }}
            onGuardar={guardarInforme}
            cargando={cargando}
          />

          {solicitudSeleccionada.estado === "INFORME_EMITIDO" && (
            <FormularioResolucion
              solicitud={solicitudSeleccionada}
              onGuardar={registrarResolucion}
              cargando={cargando}
            />
          )}
        </>
      )}
    </div>
  );
}