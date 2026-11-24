import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import FiltroNivel from "../components/FiltroNivel";
import {
  actualizarSolicitud,
  obtenerAsignaturas,
  obtenerCarreras,
  obtenerSolicitud,
} from "../services/solicitudesService";

export default function EditarSolicitud() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [carreras, setCarreras] = useState([]);
  const [asignaturasDisponibles, setAsignaturasDisponibles] = useState([]);
  const [modulo, setModulo] = useState("INTERNO");
  const [universidadOrigen, setUniversidadOrigen] = useState("");
  const [universidadDestino, setUniversidadDestino] = useState("");
  const [origen, setOrigen] = useState("");
  const [destino, setDestino] = useState("");
  const [seleccionadas, setSeleccionadas] = useState([]);
  const [nivelSeleccionado, setNivelSeleccionado] = useState("Todos");

  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");
  const [numero, setNumero] = useState("");
  const [noEditable, setNoEditable] = useState(false);

  // Carga inicial: carreras y datos de la solicitud.
  useEffect(() => {
    let activo = true;

    const cargar = async () => {
      setCargando(true);
      setError("");
      try {
        const [listaCarreras, solicitud] = await Promise.all([
          obtenerCarreras(),
          obtenerSolicitud(id),
        ]);
        if (!activo) return;

        setCarreras(Array.isArray(listaCarreras) ? listaCarreras : []);
        setNumero(
          solicitud.numeroProceso ||
            solicitud.numero ||
            `SOL-${String(solicitud.id).padStart(4, "0")}`
        );
        setModulo(solicitud.modulo || "INTERNO");
        setUniversidadOrigen(solicitud.universidadOrigen || "");
        setUniversidadDestino(solicitud.universidadDestino || "");
        setOrigen(String(solicitud.carreraOrigenId ?? ""));
        setDestino(String(solicitud.carreraDestinoId ?? ""));
        setSeleccionadas(
          (solicitud.asignaturas || []).map((a) => a.asignaturaId).filter(Boolean)
        );

        if (solicitud.puedeEditar === false || solicitud.estado !== "REGISTRADA") {
          setNoEditable(true);
        }
      } catch (e) {
        if (activo) setError(e.message || "No se pudo cargar la solicitud.");
      } finally {
        if (activo) setCargando(false);
      }
    };

    cargar();
    return () => {
      activo = false;
    };
  }, [id]);

  // Asignaturas de la carrera de origen.
  useEffect(() => {
    if (!origen) {
      const timer = setTimeout(() => setAsignaturasDisponibles([]), 0);
      return () => clearTimeout(timer);
    }

    let activo = true;
    obtenerAsignaturas(origen)
      .then((data) => {
        if (activo) {
          setAsignaturasDisponibles(Array.isArray(data) ? data : []);
        }
      })
      .catch(() => {
        if (activo) setError("No se pudieron cargar las asignaturas de la carrera de origen.");
      });

    return () => {
      activo = false;
    };
  }, [origen]);

  const cambiarOrigen = (valor) => {
    setOrigen(valor);
    // Al cambiar de carrera, las asignaturas seleccionadas dejan de pertenecer a la malla.
    setSeleccionadas([]);
    setNivelSeleccionado("Todos");
  };

  const toggleAsignatura = (asignaturaId) => {
    setSeleccionadas((prev) =>
      prev.includes(asignaturaId)
        ? prev.filter((a) => a !== asignaturaId)
        : [...prev, asignaturaId]
    );
  };

  const asignaturasFiltradas = asignaturasDisponibles.filter(
    (a) => nivelSeleccionado === "Todos" || a.nivel === nivelSeleccionado
  );

  const guardar = async (ev) => {
    ev.preventDefault();
    setError("");
    setMensaje("");

    if (!origen || !destino) {
      setError("Seleccione la carrera de origen y la de destino.");
      return;
    }
    if (origen === destino) {
      setError("La carrera de origen y la de destino no pueden ser iguales.");
      return;
    }
    if (seleccionadas.length === 0) {
      setError("Seleccione al menos una asignatura para homologar.");
      return;
    }
    if (modulo === "EXTERNO" && (!universidadOrigen.trim() || !universidadDestino.trim())) {
      setError("Para el agente externo indique la universidad de origen y la de destino.");
      return;
    }

    setGuardando(true);
    try {
      await actualizarSolicitud(id, {
        modulo,
        universidadOrigen: modulo === "EXTERNO" ? universidadOrigen.trim() : null,
        universidadDestino: modulo === "EXTERNO" ? universidadDestino.trim() : null,
        carreraOrigenId: Number(origen),
        carreraDestinoId: Number(destino),
        asignaturasIds: seleccionadas,
      });
      setMensaje("Solicitud actualizada correctamente.");
      setTimeout(() => navigate("/mis-solicitudes"), 1200);
    } catch (e) {
      setError(e.message || "No se pudo actualizar la solicitud.");
    } finally {
      setGuardando(false);
    }
  };

  if (cargando) {
    return (
      <div className="container mt-4">
        <div className="alert alert-info">Cargando solicitud...</div>
      </div>
    );
  }

  if (noEditable) {
    return (
      <div className="container mt-4">
        <div className="card shadow-sm border-0">
          <div className="card-header bg-secondary text-white">
            <h4 className="mb-0 fw-bold">Editar solicitud {numero}</h4>
            <small className="d-block fw-normal">Solicitud registrada desde el sistema</small>
          </div>
          <div className="card-body">
            <div className="alert alert-warning mb-3">
              Esta solicitud ya pasó a revisión y no se puede editar. Solo se edita
              mientras está REGISTRADA.
            </div>
            <Link to="/mis-solicitudes" className="btn btn-outline-secondary">
              Volver a Mis solicitudes
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-10">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Editar solicitud {numero}</h4>
              <small className="d-block fw-normal">Solicitud registrada desde el sistema</small>
            </div>
            <div className="card-body p-4">
              <form onSubmit={guardar} noValidate>
                <div className="mb-4">
                  <label className="form-label fw-bold">Modalidad de homologación</label>
                  <div className="btn-group w-100" role="group" aria-label="Modalidad de homologación">
                    <button
                      type="button"
                      className={`btn btn-outline-primary ${modulo === "INTERNO" ? "active" : ""}`}
                      onClick={() => setModulo("INTERNO")}
                    >
                      INTERNO · Agente UTA
                    </button>
                    <button
                      type="button"
                      className={`btn btn-outline-primary ${modulo === "EXTERNO" ? "active" : ""}`}
                      onClick={() => setModulo("EXTERNO")}
                    >
                      EXTERNO · Otra universidad
                    </button>
                  </div>
                  <div className="form-text">
                    El récord académico se entrega en ambas modalidades y es la fuente de los datos
                    del titular.
                  </div>
                </div>

                {modulo === "EXTERNO" && (
                  <div className="row mb-4">
                    <div className="col-md-6">
                      <label htmlFor="universidadOrigen" className="form-label fw-bold">
                        Universidad de origen
                      </label>
                      <input
                        id="universidadOrigen"
                        type="text"
                        className="form-control"
                        value={universidadOrigen}
                        onChange={(e) => setUniversidadOrigen(e.target.value)}
                      />
                    </div>
                    <div className="col-md-6">
                      <label htmlFor="universidadDestino" className="form-label fw-bold">
                        Universidad de destino
                      </label>
                      <input
                        id="universidadDestino"
                        type="text"
                        className="form-control"
                        value={universidadDestino}
                        onChange={(e) => setUniversidadDestino(e.target.value)}
                      />
                    </div>
                  </div>
                )}

                <div className="row mb-4">
                  <div className="col-md-6">
                    <label htmlFor="carreraOrigen" className="form-label fw-bold">
                      Carrera de origen
                    </label>
                    <select
                      id="carreraOrigen"
                      className="form-select"
                      value={origen}
                      onChange={(e) => cambiarOrigen(e.target.value)}
                    >
                      <option value="">Seleccione una carrera</option>
                      {carreras.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.nombre}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-md-6">
                    <label htmlFor="carreraDestino" className="form-label fw-bold">
                      Carrera de destino
                    </label>
                    <select
                      id="carreraDestino"
                      className="form-select"
                      value={destino}
                      onChange={(e) => setDestino(e.target.value)}
                    >
                      <option value="">Seleccione una carrera</option>
                      {carreras.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.nombre}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="mb-2 d-flex flex-wrap justify-content-between align-items-end gap-2">
                  <div>
                    <h5 className="fw-bold mb-0">Asignaturas a homologar</h5>
                    <span className="text-muted small">
                      {seleccionadas.length} seleccionada(s)
                    </span>
                  </div>
                  {asignaturasDisponibles.length > 0 && (
                    <FiltroNivel
                      nivelSeleccionado={nivelSeleccionado}
                      onChange={setNivelSeleccionado}
                    />
                  )}
                </div>

                <div className="border rounded p-3 mb-4" style={{ maxHeight: "320px", overflowY: "auto" }}>
                  {asignaturasDisponibles.length === 0 ? (
                    <p className="text-muted mb-0">
                      Seleccione una carrera de origen para ver sus asignaturas.
                    </p>
                  ) : asignaturasFiltradas.length === 0 ? (
                    <p className="text-muted mb-0">No hay asignaturas en este nivel.</p>
                  ) : (
                    asignaturasFiltradas.map((a) => (
                      <div className="form-check" key={a.id}>
                        <input
                          id={`asignatura-${a.id}`}
                          type="checkbox"
                          className="form-check-input"
                          checked={seleccionadas.includes(a.id)}
                          onChange={() => toggleAsignatura(a.id)}
                        />
                        <label htmlFor={`asignatura-${a.id}`} className="form-check-label">
                          {a.nombre}
                          <span className="text-muted small ms-2">
                            {a.nivel} · {a.creditos} créditos
                          </span>
                        </label>
                      </div>
                    ))
                  )}
                </div>

                {error && <div className="alert alert-danger">{error}</div>}
                {mensaje && <div className="alert alert-success">{mensaje}</div>}

                <div className="d-flex justify-content-between">
                  <Link to="/mis-solicitudes" className="btn btn-outline-secondary">
                    Cancelar
                  </Link>
                  <button
                    type="submit"
                    className="btn btn-primary fw-bold"
                    disabled={guardando}
                  >
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
