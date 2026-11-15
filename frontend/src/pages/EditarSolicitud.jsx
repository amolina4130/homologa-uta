import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import FiltroNivel from "../components/FiltroNivel";
import {
  obtenerCarreras,
  obtenerAsignaturas,
  obtenerSolicitud,
  editarSolicitud,
} from "../services/solicitudesService";

/**
 * Edición de una solicitud mientras siga en estado REGISTRADA (HU-16).
 */
export default function EditarSolicitud() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [carreras, setCarreras] = useState([]);
  const [asignaturasDisponibles, setAsignaturasDisponibles] = useState([]);
  const [modulo, setModulo] = useState("INTERNO");
  const [origen, setOrigen] = useState("");
  const [destino, setDestino] = useState("");
  const [universidadOrigen, setUniversidadOrigen] = useState("");
  const [universidadDestino, setUniversidadDestino] = useState("");
  const [seleccionadas, setSeleccionadas] = useState([]);
  const [nivelSeleccionado, setNivelSeleccionado] = useState("Todos");
  const [editable, setEditable] = useState(true);
  const [estadoActual, setEstadoActual] = useState("");
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  useEffect(() => {
    obtenerCarreras()
      .then((data) => setCarreras(data.data || data))
      .catch(() => setError("No se pudieron cargar las carreras."));
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const sol = await obtenerSolicitud(id);
        setModulo(sol.modulo || "INTERNO");
        setOrigen(String(sol.carreraOrigenId ?? ""));
        setDestino(String(sol.carreraDestinoId ?? ""));
        setUniversidadOrigen(sol.universidadOrigen || "");
        setUniversidadDestino(sol.universidadDestino || "");
        setSeleccionadas((sol.asignaturas || []).map((a) => a.asignaturaId));
        setEditable(sol.editable !== false);
        setEstadoActual(sol.estado || "");
      } catch (err) {
        setError(err.message || "No se pudo cargar la solicitud.");
      } finally {
        setCargando(false);
      }
    })();
  }, [id]);

  useEffect(() => {
    if (!origen) return;
    obtenerAsignaturas(origen)
      .then((data) => setAsignaturasDisponibles(data.data || data))
      .catch(() => setError("No se pudieron cargar las asignaturas."));
  }, [origen]);

  const toggleAsignatura = (asigId) => {
    setSeleccionadas((prev) =>
      prev.includes(asigId) ? prev.filter((a) => a !== asigId) : [...prev, asigId]
    );
  };

  const asignaturasFiltradas = asignaturasDisponibles.filter(
    (a) => nivelSeleccionado === "Todos" || a.nivel === nivelSeleccionado
  );

  async function guardar(ev) {
    ev.preventDefault();
    setError("");
    setMensaje("");

    if (!origen || !destino) return setError("Seleccione carrera de origen y destino.");
    if (origen === destino) return setError("La carrera de origen y destino no pueden ser iguales.");
    if (seleccionadas.length === 0) return setError("Seleccione al menos una asignatura.");
    if (modulo === "EXTERNO" && (!universidadOrigen.trim() || !universidadDestino.trim()))
      return setError("En el módulo externo debe indicar la universidad de origen y de destino.");

    setGuardando(true);
    try {
      await editarSolicitud(id, {
        modulo,
        carreraOrigenId: Number(origen),
        carreraDestinoId: Number(destino),
        universidadOrigen: modulo === "EXTERNO" ? universidadOrigen.trim() : null,
        universidadDestino: modulo === "EXTERNO" ? universidadDestino.trim() : null,
        asignaturasIds: seleccionadas,
      });
      setMensaje("Solicitud actualizada correctamente.");
      setTimeout(() => navigate("/mis-solicitudes"), 1200);
    } catch (err) {
      setError(err.message || "No se pudo editar la solicitud.");
    } finally {
      setGuardando(false);
    }
  }

  if (cargando) {
    return <div className="container mt-4"><div className="alert alert-info">Cargando solicitud...</div></div>;
  }

  if (!editable) {
    return (
      <div className="container mt-4">
        <div className="alert alert-warning">
          La solicitud está en estado <strong>{estadoActual}</strong> y ya no se puede editar.
        </div>
        <button className="btn btn-outline-secondary" onClick={() => navigate("/mis-solicitudes")}>
          Volver a Mis solicitudes
        </button>
      </div>
    );
  }

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-10">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Editar solicitud SOL-{String(id).padStart(4, "0")}</h4>
            </div>
            <div className="card-body p-4">
              {error && <div className="alert alert-danger">{error}</div>}
              {mensaje && <div className="alert alert-success">{mensaje}</div>}

              <form onSubmit={guardar}>
                <div className="row">
                  <div className="col-md-6 border-end">
                    <div className="mb-3">
                      <label className="form-label fw-bold">Módulo de análisis</label>
                      <select className="form-select" value={modulo} onChange={(e) => setModulo(e.target.value)}>
                        <option value="INTERNO">Agente interno (reglas normativas UTA)</option>
                        <option value="EXTERNO">Agente externo (universidades externas)</option>
                      </select>
                    </div>

                    {modulo === "EXTERNO" && (
                      <>
                        <div className="mb-3">
                          <label className="form-label fw-bold">Universidad de origen</label>
                          <input
                            className="form-control"
                            value={universidadOrigen}
                            onChange={(e) => setUniversidadOrigen(e.target.value)}
                            placeholder="Ej. Universidad de Cuenca"
                          />
                        </div>
                        <div className="mb-3">
                          <label className="form-label fw-bold">Universidad de destino</label>
                          <input
                            className="form-control"
                            value={universidadDestino}
                            onChange={(e) => setUniversidadDestino(e.target.value)}
                            placeholder="Ej. Universidad Técnica de Ambato"
                          />
                        </div>
                      </>
                    )}

                    <div className="mb-3">
                      <label className="form-label fw-bold">Carrera Origen</label>
                      <select className="form-select" value={origen} onChange={(e) => { setOrigen(e.target.value); setSeleccionadas([]); }} required>
                        <option value="">Seleccione una carrera...</option>
                        {carreras.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                      </select>
                    </div>

                    <div className="mb-3">
                      <label className="form-label fw-bold">Carrera Destino</label>
                      <select className="form-select" value={destino} onChange={(e) => setDestino(e.target.value)} required>
                        <option value="">Seleccione una carrera...</option>
                        {carreras.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                      </select>
                    </div>
                  </div>

                  <div className="col-md-6 ps-md-4">
                    <label className="form-label fw-bold">Asignaturas a homologar</label>
                    <FiltroNivel nivelSeleccionado={nivelSeleccionado} onChange={setNivelSeleccionado} />

                    <div className="border rounded p-3 bg-light" style={{ maxHeight: "280px", overflowY: "auto" }}>
                      {!origen ? (
                        <p className="text-muted small">Seleccione una carrera de origen.</p>
                      ) : asignaturasFiltradas.length === 0 ? (
                        <p className="text-muted small">No hay asignaturas en este nivel.</p>
                      ) : (
                        asignaturasFiltradas.map((a) => (
                          <div className="form-check mb-2" key={a.id}>
                            <input
                              className="form-check-input"
                              type="checkbox"
                              id={`edit-asig-${a.id}`}
                              checked={seleccionadas.includes(a.id)}
                              onChange={() => toggleAsignatura(a.id)}
                            />
                            <label className="form-check-label" htmlFor={`edit-asig-${a.id}`}>
                              {a.nombre} <span className="text-muted small">({a.nivel})</span>
                            </label>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                </div>

                <div className="d-flex justify-content-between mt-4">
                  <button type="button" className="btn btn-outline-secondary" onClick={() => navigate("/mis-solicitudes")}>
                    Cancelar
                  </button>
                  <button type="submit" className="btn btn-primary px-5 fw-bold" disabled={guardando}>
                    {guardando ? "Guardando..." : "Guardar cambios"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
