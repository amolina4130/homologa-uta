// frontend/src/pages/Equivalencias.jsx
import React, { useState, useEffect } from 'react';
import { obtenerEquivalencias, crearEquivalencia, cambiarEstadoEquivalencia } from '../services/equivalenciasService';

export default function Equivalencias() {
  const [equivalencias, setEquivalencias] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [mensajeExito, setMensajeExito] = useState('');

  // Formulario (IDs simplificados para el ejemplo)
  const [formData, setFormData] = useState({
    carreraOrigenId: '', asignaturaOrigenId: '',
    carreraDestinoId: '', asignaturaDestinoId: ''
  });

  useEffect(() => {
    cargarDatos();
  }, []);

  const cargarDatos = async () => {
    setCargando(true);
    try {
      const data = await obtenerEquivalencias();
      setEquivalencias(data);
    } catch (err) {
      setError('No se pudieron cargar las equivalencias.');
    } finally {
      setCargando(false);
    }
  };

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleCrear = async (e) => {
    e.preventDefault();
    setError(''); setMensajeExito('');
    try {
      await crearEquivalencia({ ...formData, activa: true });
      setMensajeExito('Equivalencia creada exitosamente.');
      setFormData({ carreraOrigenId: '', asignaturaOrigenId: '', carreraDestinoId: '', asignaturaDestinoId: '' });
      cargarDatos(); // Recargar la tabla
    } catch (err) {
      setError(err.message); // Aquí se muestra el mensaje de "ya existe"
    }
  };

  const handleToggle = async (id, estadoActual) => {
    try {
      await cambiarEstadoEquivalencia(id, !estadoActual);
      // Actualizamos la tabla visualmente sin recargar
      setEquivalencias(equivalencias.map(eq => eq.id === id ? { ...eq, activa: !estadoActual } : eq));
    } catch (err) {
      alert("Error al cambiar el estado");
    }
  };

  if (cargando) return <div className="text-center mt-5">Cargando...</div>;

  return (
    <div className="container mt-4">
      <h2 className="mb-4">Equivalencias entre Carreras</h2>

      {error && <div className="alert alert-danger">{error}</div>}
      {mensajeExito && <div className="alert alert-success">{mensajeExito}</div>}

      {/* Formulario de creación */}
      <div className="card shadow-sm mb-4">
        <div className="card-header bg-primary text-white">
          <h5 className="mb-0">Crear Nueva Equivalencia</h5>
        </div>
        <div className="card-body">
          <form onSubmit={handleCrear} className="row g-3">
            <div className="col-md-3">
              <label className="form-label">ID Carrera Origen</label>
              <input type="number" className="form-control" name="carreraOrigenId" value={formData.carreraOrigenId} onChange={handleChange} required />
            </div>
            <div className="col-md-3">
              <label className="form-label">ID Asignatura Origen</label>
              <input type="number" className="form-control" name="asignaturaOrigenId" value={formData.asignaturaOrigenId} onChange={handleChange} required />
            </div>
            <div className="col-md-3">
              <label className="form-label">ID Carrera Destino</label>
              <input type="number" className="form-control" name="carreraDestinoId" value={formData.carreraDestinoId} onChange={handleChange} required />
            </div>
            <div className="col-md-3">
              <label className="form-label">ID Asignatura Destino</label>
              <input type="number" className="form-control" name="asignaturaDestinoId" value={formData.asignaturaDestinoId} onChange={handleChange} required />
            </div>
            <div className="col-12 mt-3">
              <button type="submit" className="btn btn-success">Guardar Equivalencia</button>
            </div>
          </form>
        </div>
      </div>

      {/* Tabla de equivalencias */}
      <div className="card shadow-sm">
        <div className="card-body">
          <table className="table table-hover">
            <thead className="table-light">
              <tr>
                <th>Carrera Origen</th>
                <th>Asignatura Origen</th>
                <th>Carrera Destino</th>
                <th>Asignatura Destino</th>
                <th>Estado</th>
                <th>Acción</th>
              </tr>
            </thead>
            <tbody>
              {equivalencias.map((eq) => (
                <tr key={eq.id}>
                  <td>{eq.carreraOrigenNombre || eq.carreraOrigenId}</td>
                  <td>{eq.asignaturaOrigenNombre || eq.asignaturaOrigenId}</td>
                  <td>{eq.carreraDestinoNombre || eq.carreraDestinoId}</td>
                  <td>{eq.asignaturaDestinoNombre || eq.asignaturaDestinoId}</td>
                  <td>
                    <span className={`badge ${eq.activa ? 'bg-success' : 'bg-secondary'}`}>
                      {eq.activa ? 'Activa' : 'Inactiva'}
                    </span>
                  </td>
                  <td>
                    <button 
                      className={`btn btn-sm ${eq.activa ? 'btn-outline-danger' : 'btn-outline-success'}`}
                      onClick={() => handleToggle(eq.id, eq.activa)}
                    >
                      {eq.activa ? 'Desactivar' : 'Activar'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}