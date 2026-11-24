import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerAsignaturas, obtenerCarreras } from "../services/solicitudesService";
import {
  actualizarEquivalencia,
  crearEquivalencia,
  listarEquivalencias,
} from "../services/equivalenciasService";

const formularioInicial = {
  carreraOrigenId: "",
  asignaturaOrigenId: "",
  carreraDestinoId: "",
  asignaturaDestinoId: "",
  activa: true,
};

export default function Equivalencias() {
  const { rol } = useAuth();
  const [equivalencias, setEquivalencias] = useState([]);
  const [carreras, setCarreras] = useState([]);
  const [asignaturasPorCarrera, setAsignaturasPorCarrera] = useState({});
  const [formulario, setFormulario] = useState(formularioInicial);
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  const cargarAsignaturas = useCallback(async (carreraId) => {
    if (!carreraId) return [];
    if (asignaturasPorCarrera[carreraId]) return asignaturasPorCarrera[carreraId];

    const lista = await obtenerAsignaturas(carreraId);
    setAsignaturasPorCarrera((prev) => ({ ...prev, [carreraId]: lista }));
    return lista;
  }, [asignaturasPorCarrera]);

  const cargarTodo = useCallback(async () => {
    setCargando(true);
    setError("");
    try {
      const [lista, listaCarreras] = await Promise.all([
        listarEquivalencias(),
        obtenerCarreras(),
      ]);
      setEquivalencias(Array.isArray(lista) ? lista : []);
      setCarreras(Array.isArray(listaCarreras) ? listaCarreras : []);
    } catch (e) {
      setError(e.message || "No se pudieron cargar las equivalencias.");
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    if (rol !== "ADMINISTRADOR") return;
    const timer = setTimeout(() => cargarTodo(), 0);
    return () => clearTimeout(timer);
  }, [rol, cargarTodo]);

  useEffect(() => {
    if (!formulario.carreraOrigenId) return;
    const timer = setTimeout(() => {
      cargarAsignaturas(Number(formulario.carreraOrigenId)).catch(() => {
        setError("No se pudieron cargar las asignaturas de la carrera de origen.");
      });
    }, 0);
    return () => clearTimeout(timer);
  }, [formulario.carreraOrigenId, cargarAsignaturas]);

  useEffect(() => {
    if (!formulario.carreraDestinoId) return;
    const timer = setTimeout(() => {
      cargarAsignaturas(Number(formulario.carreraDestinoId)).catch(() => {
        setError("No se pudieron cargar las asignaturas de la carrera de destino.");
      });
    }, 0);
    return () => clearTimeout(timer);
  }, [formulario.carreraDestinoId, cargarAsignaturas]);

  const cambiar = (campo, valor) => {
    setFormulario((prev) => {
      const siguiente = { ...prev, [campo]: valor };
      if (campo === "carreraOrigenId") siguiente.asignaturaOrigenId = "";
      if (campo === "carreraDestinoId") siguiente.asignaturaDestinoId = "";
      return siguiente;
    });
  };

  const guardar = async (ev) => {
    ev.preventDefault();
    setError("");
    setMensaje("");

    if (
      !formulario.carreraOrigenId ||
      !formulario.asignaturaOrigenId ||
      !formulario.carreraDestinoId ||
      !formulario.asignaturaDestinoId
    ) {
      setError("Debe seleccionar la carrera y la asignatura de origen y de destino.");
      return;
    }

    if (formulario.carreraOrigenId === formulario.carreraDestinoId) {
      setError("La carrera de origen y la de destino deben ser diferentes.");
      return;
    }

    setGuardando(true);
    try {
      await crearEquivalencia({
        carreraOrigenId: Number(formulario.carreraOrigenId),
        asignaturaOrigenId: Number(formulario.asignaturaOrigenId),
        carreraDestinoId: Number(formulario.carreraDestinoId),
        asignaturaDestinoId: Number(formulario.asignaturaDestinoId),
        activa: formulario.activa,
      });
      setMensaje("Equivalencia registrada correctamente.");
      setFormulario(formularioInicial);
      await cargarTodo();
    } catch (e) {
      setError(
        e.status === 409
          ? e.message || "Ya existe una equivalencia entre esas mismas asignaturas."
          : e.message || "No se pudo registrar la equivalencia."
      );
    } finally {
      setGuardando(false);
    }
  };

  const alternar = async (equivalencia) => {
    setError("");
    setMensaje("");
    try {
      await actualizarEquivalencia(equivalencia.id, {
        carreraOrigenId: equivalencia.carreraOrigenId,
        asignaturaOrigenId: equivalencia.asignaturaOrigenId,
        carreraDestinoId: equivalencia.carreraDestinoId,
        asignaturaDestinoId: equivalencia.asignaturaDestinoId,
        activa: !equivalencia.activa,
      });
      setMensaje(
        equivalencia.activa
          ? "Equivalencia desactivada."
          : "Equivalencia activada."
      );
      await cargarTodo();
    } catch (e) {
      setError(e.message || "No se pudo cambiar el estado de la equivalencia.");
    }
  };

  if (rol !== "ADMINISTRADOR") {
    return (
      <div className="container mt-4">
        <div className="alert alert-danger">
          Acceso denegado. Solo el Administrador puede gestionar las equivalencias.
        </div>
      </div>
    );
  }

  const asignaturasOrigen = asignaturasPorCarrera[Number(formulario.carreraOrigenId)] || [];
  const asignaturasDestino = asignaturasPorCarrera[Number(formulario.carreraDestinoId)] || [];

  return (
    <div className="container mt-4">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="mb-0">EQUIVALENCIAS ENTRE CARRERAS</h2>
        <button
          className="btn btn-outline-primary btn-sm"
          onClick={cargarTodo}
          disabled={cargando || guardando}
        >
          Actualizar lista
        </button>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {mensaje && <div className="alert alert-success">{mensaje}</div>}

      <div className="card shadow-sm border-0 mb-4">
        <div className="card-header bg-primary text-white">
          <h5 className="mb-0 fw-bold">Registrar una equivalencia</h5>
        </div>
        <div className="card-body">
          <form onSubmit={guardar} noValidate>
            <div className="row">
              <div className="col-md-6 mb-3">
                <label htmlFor="carreraOrigen" className="form-label fw-bold">
                  Carrera de origen
                </label>
                <select
                  id="carreraOrigen"
                  className="form-select"
                  value={formulario.carreraOrigenId}
                  onChange={(e) => cambiar("carreraOrigenId", e.target.value)}
                >
                  <option value="">Seleccione una carrera</option>
                  {carreras.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nombre}
                    </option>
                  ))}
                </select>
              </div>

              <div className="col-md-6 mb-3">
                <label htmlFor="asignaturaOrigen" className="form-label fw-bold">
                  Asignatura de origen
                </label>
                <select
                  id="asignaturaOrigen"
                  className="form-select"
                  value={formulario.asignaturaOrigenId}
                  onChange={(e) => cambiar("asignaturaOrigenId", e.target.value)}
                  disabled={!formulario.carreraOrigenId}
                >
                  <option value="">Seleccione una asignatura</option>
                  {asignaturasOrigen.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.nombre} {a.nivel ? `(${a.nivel})` : ""}
                    </option>
                  ))}
                </select>
              </div>

              <div className="col-md-6 mb-3">
                <label htmlFor="carreraDestino" className="form-label fw-bold">
                  Carrera de destino
                </label>
                <select
                  id="carreraDestino"
                  className="form-select"
                  value={formulario.carreraDestinoId}
                  onChange={(e) => cambiar("carreraDestinoId", e.target.value)}
                >
                  <option value="">Seleccione una carrera</option>
                  {carreras.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nombre}
                    </option>
                  ))}
                </select>
              </div>

              <div className="col-md-6 mb-3">
                <label htmlFor="asignaturaDestino" className="form-label fw-bold">
                  Asignatura de destino
                </label>
                <select
                  id="asignaturaDestino"
                  className="form-select"
                  value={formulario.asignaturaDestinoId}
                  onChange={(e) => cambiar("asignaturaDestinoId", e.target.value)}
                  disabled={!formulario.carreraDestinoId}
                >
                  <option value="">Seleccione una asignatura</option>
                  {asignaturasDestino.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.nombre} {a.nivel ? `(${a.nivel})` : ""}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="form-check mb-3">
              <input
                id="activa"
                type="checkbox"
                className="form-check-input"
                checked={formulario.activa}
                onChange={(e) => cambiar("activa", e.target.checked)}
              />
              <label htmlFor="activa" className="form-check-label">
                Equivalencia activa (se aplica en el análisis)
              </label>
            </div>

            <div className="text-end">
              <button
                type="submit"
                className="btn btn-primary fw-bold"
                disabled={guardando || cargando}
              >
                {guardando ? "Guardando..." : "Registrar equivalencia"}
              </button>
            </div>
          </form>
        </div>
      </div>

      <div className="card shadow-sm border-0">
        <div className="card-header bg-primary text-white">
          <h5 className="mb-0 fw-bold">Equivalencias registradas</h5>
        </div>
        <div className="card-body p-0">
          {cargando ? (
            <div className="p-4 text-center">Cargando equivalencias...</div>
          ) : equivalencias.length === 0 ? (
            <div className="p-4 text-center text-muted">
              Todavía no hay equivalencias registradas.
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover table-bordered align-middle mb-0">
                <thead className="table-light">
                  <tr>
                    <th>Carrera origen</th>
                    <th>Asignatura origen</th>
                    <th>Carrera destino</th>
                    <th>Asignatura destino</th>
                    <th className="text-center">Estado</th>
                    <th className="text-center">Acción</th>
                  </tr>
                </thead>
                <tbody>
                  {equivalencias.map((e) => (
                    <tr key={e.id}>
                      <td>{e.carreraOrigen}</td>
                      <td>{e.asignaturaOrigen}</td>
                      <td>{e.carreraDestino}</td>
                      <td>{e.asignaturaDestino}</td>
                      <td className="text-center">
                        <span className={`badge ${e.activa ? "bg-success" : "bg-secondary"}`}>
                          {e.activa ? "ACTIVA" : "INACTIVA"}
                        </span>
                      </td>
                      <td className="text-center">
                        <button
                          className={`btn btn-sm ${
                            e.activa ? "btn-outline-danger" : "btn-outline-success"
                          }`}
                          onClick={() => alternar(e)}
                          disabled={guardando}
                        >
                          {e.activa ? "Desactivar" : "Activar"}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
