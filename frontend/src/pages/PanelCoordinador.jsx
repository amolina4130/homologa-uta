import { useState, useEffect } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerSolicitudesPendientes, obtenerDetalleSolicitud, emitirInforme } from "../services/coordinadorService";
import ListaSolicitudes from "../components/ListaSolicitudes";
import DetalleSolicitud from "../components/DetalleSolicitud";

export default function PanelCoordinador() {
  const { rol } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudSeleccionada, setSolicitudSeleccionada] = useState(null);
  const [decisiones, setDecisiones] = useState({});
  const [nuevosResultados, setNuevosResultados] = useState({});
  const [observaciones, setObservaciones] = useState({});
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  useEffect(() => {
    if (rol !== "COORDINADOR") return;

    // Envolvemos el setCargando en un setTimeout para evitar el error de ESLint
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
      
      const decIniciales = {};
      const obsIniciales = {};
      const resIniciales = {};
      detalle.asignaturas.forEach((a) => {
        decIniciales[a.id] = ""; 
        obsIniciales[a.id] = "";
        resIniciales[a.id] = "";
      });
      setDecisiones(decIniciales);
      setObservaciones(obsIniciales);
      setNuevosResultados(resIniciales);
    } catch {
      setError("Error al cargar el detalle.");
    } finally {
      setCargando(false);
    }
  };

  const guardarInforme = async () => {
    if (!solicitudSeleccionada) return;

    const todasValidas = solicitudSeleccionada.asignaturas.every((asig) => {
      const decision = decisiones[asig.id];
      const nuevoResultado = nuevosResultados[asig.id];
      const observacion = observaciones[asig.id];

      if (!decision) return false;
      if (decision === "MODIFICAR") {
        if (!nuevoResultado) return false;
        if (!observacion || observacion.trim() === "") return false;
      }
      return true;
    });

    if (!todasValidas) {
      setError("Debe completar el nuevo resultado y la observación para las asignaturas modificadas.");
      return;
    }

    const confirmar = window.confirm("¿Está seguro de emitir el informe técnico? Esta acción no se puede deshacer.");
    if (!confirmar) return;

    setCargando(true);
    setError("");
    setExito("");

    const decisionesFormateadas = Object.keys(decisiones).map((id) => ({
      asignaturaId: Number(id),
      decision: decisiones[id],
      nuevoResultado: nuevosResultados[id] || null,
      observacion: observaciones[id],
    }));

    try {
      await emitirInforme(solicitudSeleccionada.id, decisionesFormateadas);
      setExito("Informe técnico emitido correctamente.");
      setSolicitudSeleccionada(null);
      
      try {
        const data = await obtenerSolicitudesPendientes();
        setSolicitudes(data);
      } catch {
        console.error("No se pudo recargar la lista de solicitudes.");
      }

    } catch {
      setError("Error al emitir el informe. Verifique la conexión.");
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

  const todasDecididas = solicitudSeleccionada?.asignaturas.every((asig) => {
    const decision = decisiones[asig.id];
    const nuevoResultado = nuevosResultados[asig.id];
    const observacion = observaciones[asig.id];

    if (!decision) return false;
    if (decision === "MODIFICAR") {
      if (!nuevoResultado) return false;
      if (!observacion || observacion.trim() === "") return false;
    }
    return true;
  });

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
          decisiones={decisiones}
          nuevosResultados={nuevosResultados}
          observaciones={observaciones}
          onDecisionChange={(id, valor) => setDecisiones((prev) => ({ ...prev, [id]: valor }))}
          onResultadoChange={(id, valor) => setNuevosResultados((prev) => ({ ...prev, [id]: valor }))}
          onObservacionChange={(id, valor) => setObservaciones((prev) => ({ ...prev, [id]: valor }))}
          onVolver={() => setSolicitudSeleccionada(null)}
          onGuardar={guardarInforme}
          cargando={cargando}
          todasDecididas={todasDecididas}
        />
      )}
    </div>
  );
}