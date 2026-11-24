import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  obtenerDetalleSolicitud,
  emitirInforme,
} from "../services/coordinadorService";
import { descargarInforme } from "../services/homologacionService";
import DetalleSolicitud from "../components/DetalleSolicitud";

/**
 * Vista propia del detalle. Se abre en otra pestaña desde "Ver detalle" para que el
 * Coordinador no tenga que leer el contenido debajo de la lista de solicitudes.
 */
export default function DetalleSolicitudPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [solicitud, setSolicitud] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [descargando, setDescargando] = useState(false);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  useEffect(() => {
    let activo = true;
    obtenerDetalleSolicitud(id)
      .then((detalle) => {
        if (activo) setSolicitud(detalle);
      })
      .catch((e) => {
        if (activo) setError(e.message || "No se pudo cargar el detalle de la solicitud.");
      })
      .finally(() => {
        if (activo) setCargando(false);
      });
    return () => {
      activo = false;
    };
  }, [id]);

  const guardar = async (informeArmado) => {
    if (!solicitud) return;
    if (!window.confirm("¿Está seguro de emitir el informe técnico?")) return;

    setCargando(true);
    setError("");
    setExito("");
    try {
      await emitirInforme(solicitud.solicitudId, informeArmado);
      setExito("Informe técnico emitido correctamente.");
      setSolicitud(await obtenerDetalleSolicitud(solicitud.solicitudId));
    } catch (e) {
      setError(e.message || "No se pudo emitir el informe.");
    } finally {
      setCargando(false);
    }
  };

  const descargar = async () => {
    if (!solicitud) return;
    setDescargando(true);
    setError("");
    try {
      await descargarInforme(solicitud.solicitudId);
      setExito("Informe descargado.");
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe.");
    } finally {
      setDescargando(false);
    }
  };

  return (
    <div className="container mt-4">
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <div>
          <h4 className="mb-0 fw-bold">Detalle de la solicitud</h4>
          <small className="text-muted">
            Vista independiente, abierta desde “Ver detalle”.
          </small>
        </div>
        <button
          type="button"
          className="btn btn-outline-secondary"
          onClick={() => navigate(-1)}
        >
          Volver
        </button>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {exito && <div className="alert alert-success">{exito}</div>}

      {cargando ? (
        <div className="alert alert-info">Cargando detalle...</div>
      ) : (
        <DetalleSolicitud
          solicitud={solicitud}
          onVolver={() => navigate(-1)}
          onGuardar={guardar}
          onDescargarInforme={descargar}
          descargando={descargando}
          cargando={cargando}
        />
      )}
    </div>
  );
}
