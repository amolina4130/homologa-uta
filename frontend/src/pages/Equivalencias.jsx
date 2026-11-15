import { useEffect, useState } from "react";
import { obtenerCarreras, obtenerAsignaturas } from "../services/solicitudesService";
import {
  listarEquivalencias,
  crearEquivalencia,
  actualizarEquivalencia,
} from "../services/catalogoService";

const VACIO = {
  carreraOrigenId: "",
  asignaturaOrigenId: "",
  carreraDestinoId: "",
  asignaturaDestinoId: "",
  activa: true,
};

export default function Equivalencias() {
  const [carreras, setCarreras] = useState([]);
  const [asignaturasOrigen, setAsignaturasOrigen] = useState([]);
  const [asignaturasDestino, setAsignaturasDestino] = useState([]);
  const [equivalencias, setEquivalencias] = useState([]);
  const [form, setForm] = useState(VACIO);
  const [editandoId, setEditandoId] = useState(null);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");
  const [cargando, setCargando] = useState(false);

  const cargar = () =>
    listarEquivalencias()
      .then((data) => setEquivalencias(Array.isArray(data) ? data : []))
      .catch(() => setError("No se pudieron cargar las equivalencias."));

  useEffect(() => {
    obtenerCarreras().then((data) => setCarreras(data.data || data)).catch(console.error);
    cargar();
  }, []);

  useEffect(() => {
    if (!form.carreraOrigenId) {
      setAsignaturasOrigen([]);
      return;
    }
    obtenerAsignaturas(form.carreraOrigenId)
      .then((data) => setAsignaturasOrigen(data.data || data))
      .catch(console.error);
  }, [form.carreraOrigenId]);

  useEffect(() => {
    if (!form.carreraDestinoId) {
      setAsignaturasDestino([]);
      return;
    }
    obtenerAsignaturas(form.carreraDestinoId)
      .then((data) => setAsignaturasDestino(data.data || data))
      .catch(console.error);
  }, [form.carreraDestinoId]);

  const cambiar = (campo, valor) => setForm((prev) => ({ ...prev, [campo]: valor }));

  async function guardar(ev) {
    ev.preventDefault();
    setError("");
    setExito("");

    if (!form.carreraOrigenId || !form.asignaturaOrigenId || !form.carreraDestinoId || !form.asignaturaDestinoId)
      return setError("Complete los cuatro campos de la equivalencia.");
    if (form.carreraOrigenId === form.carreraDestinoId)
      return setError("La carrera de origen y destino no pueden ser iguales.");

    const datos = {
      carreraOrigenId: Number(form.carreraOrigenId),
      asignaturaOrigenId: Number(form.asignaturaOrigenId),
      carreraDestinoId: Number(form.carreraDestinoId),
      asignaturaDestinoId: Number(form.asignaturaDestinoId),
      activa: Boolean(form.activa),
    };

    setCargando(true);
    try {
      if (editandoId) await actualizarEquivalencia(editandoId, datos);
      else await crearEquivalencia(datos);
      setExito(editandoId ? "Equivalencia actualizada." : "Equivalencia registrada.");
      setForm(VACIO);
      setEditandoId(null);
      await cargar();
    } catch (err) {
      setError(err.message);
    } finally {
      setCargando(false);
    }
  }

  function editar(e) {
    setEditandoId(e.id);
    setForm({
      carreraOrigenId: e.carreraOrigenId,
      asignaturaOrigenId: e.asignaturaOrigenId,
      carreraDestinoId: e.carreraDestinoId,
      asignaturaDestinoId: e.asignaturaDestinoId,
      activa: e.activa,
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  return (
    <div className="container mt-4">
      <h3 className="fw-bold mb-3">Equivalencias entre carreras</h3>
      {error && <div className="alert alert-danger">{error}</div>}
      {exito && <div className="alert alert-success">{exito}</div>}

      <div className="card shadow-sm border-0 mb-4">
        <div className="card-header bg-primary text-white fw-bold">
          {editandoId ? "Modificar equivalencia" : "Registrar equivalencia"}
        </div>
        <div className="card-body">
          <form onSubmit={guardar} className="row g-3">
            <div className="col-md-6">
              <label className="form-label fw-bold">Carrera de origen</label>
              <select className="form-select" value={form.carreraOrigenId} onChange={(e) => { cambiar("carreraOrigenId", e.target.value); cambiar("asignaturaOrigenId", ""); }} required>
                <option value="">Seleccione...</option>
                {carreras.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
              </select>
            </div>
            <div className="col-md-6">
              <label className="form-label fw-bold">Carrera de destino</label>
              <select className="form-select" value={form.carreraDestinoId} onChange={(e) => { cambiar("carreraDestinoId", e.target.value); cambiar("asignaturaDestinoId", ""); }} required>
                <option value="">Seleccione...</option>
                {carreras.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
              </select>
            </div>
            <div className="col-md-6">
              <label className="form-label fw-bold">Asignatura de origen</label>
              <select className="form-select" value={form.asignaturaOrigenId} onChange={(e) => cambiar("asignaturaOrigenId", e.target.value)} required>
                <option value="">Seleccione...</option>
                {asignaturasOrigen.map((a) => <option key={a.id} value={a.id}>{a.nombre}</option>)}
              </select>
            </div>
            <div className="col-md-6">
              <label className="form-label fw-bold">Asignatura de destino</label>
              <select className="form-select" value={form.asignaturaDestinoId} onChange={(e) => cambiar("asignaturaDestinoId", e.target.value)} required>
                <option value="">Seleccione...</option>
                {asignaturasDestino.map((a) => <option key={a.id} value={a.id}>{a.nombre}</option>)}
              </select>
            </div>
            <div className="col-12 form-check ms-2">
              <input className="form-check-input" type="checkbox" id="activa" checked={form.activa} onChange={(e) => cambiar("activa", e.target.checked)} />
              <label className="form-check-label" htmlFor="activa">Activa</label>
            </div>
            <div className="col-12 text-end">
              {editandoId && (
                <button type="button" className="btn btn-outline-secondary me-2" onClick={() => { setEditandoId(null); setForm(VACIO); }}>
                  Cancelar
                </button>
              )}
              <button className="btn btn-primary fw-bold" disabled={cargando}>
                {cargando ? "Guardando..." : editandoId ? "Actualizar" : "Registrar"}
              </button>
            </div>
          </form>
        </div>
      </div>

      <div className="table-responsive">
        <table className="table table-bordered table-hover shadow-sm align-middle">
          <thead className="table-primary">
            <tr>
              <th>Carrera origen</th>
              <th>Asignatura origen</th>
              <th>Carrera destino</th>
              <th>Asignatura destino</th>
              <th className="text-center">Activa</th>
              <th className="text-center">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {equivalencias.length === 0 ? (
              <tr><td colSpan="6" className="text-center text-muted">No hay equivalencias registradas.</td></tr>
            ) : equivalencias.map((e) => (
              <tr key={e.id}>
                <td>{e.carreraOrigen}</td>
                <td>{e.asignaturaOrigen}</td>
                <td>{e.carreraDestino}</td>
                <td>{e.asignaturaDestino}</td>
                <td className="text-center">
                  <span className={`badge ${e.activa ? "bg-success" : "bg-secondary"}`}>{e.activa ? "Sí" : "No"}</span>
                </td>
                <td className="text-center">
                  <button className="btn btn-sm btn-outline-primary" onClick={() => editar(e)}>Editar</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
