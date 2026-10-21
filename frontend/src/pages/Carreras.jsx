import React, { useState, useEffect } from 'react';

export default function Carreras() {
  const [carreras, setCarreras] = useState([]);
  const [carreraSeleccionada, setCarreraSeleccionada] = useState(null);
  const [asignaturas, setAsignaturas] = useState([]);
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem("token");
    fetch("http://localhost:8080/carreras", { headers: { "Authorization": `Bearer ${token}` } })
      .then(res => res.json())
      .then(data => {
        // ORDENAMOS LAS CARRERAS POR ID (1, 2, 3...)
        const carrerasOrdenadas = data.sort((a, b) => a.id - b.id);
        setCarreras(carrerasOrdenadas);
      })
      .catch(() => console.error("Error"))
      .finally(() => setCargando(false));
  }, []);

  const verMalla = (carrera) => {
    setCarreraSeleccionada(carrera);
    const token = localStorage.getItem("token");
    fetch(`http://localhost:8080/carreras/${carrera.id}/asignaturas`, { headers: { "Authorization": `Bearer ${token}` } })
      .then(res => res.json())
      .then(data => setAsignaturas(data))
      .catch(() => console.error("Error mallas"));
  };

  return (
    <div className="container mt-4">
      <div className="row">
        
        {/* Lista de Carreras (Izquierda) */}
        <div className="col-md-4">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h5 className="mb-0 fw-bold">Carreras</h5>
            </div>
            <div className="list-group list-group-flush">
              {cargando && <div className="p-3 text-center">Cargando...</div>}
              {carreras.map(c => (
                <button 
                  key={c.id} 
                  className={`list-group-item list-group-item-action d-flex justify-content-between align-items-center ${carreraSeleccionada?.id === c.id ? 'active' : ''}`}
                  onClick={() => verMalla(c)}
                >
                  <span className="fw-bold">{c.nombre}</span>
                  <span className="badge bg-primary">ID: {c.id}</span>
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Tabla de Mallas (Derecha) */}
        <div className="col-md-8">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h5 className="mb-0 fw-bold">
                {carreraSeleccionada ? `Malla: ${carreraSeleccionada.nombre}` : 'Selecciona una carrera'}
              </h5>
            </div>
            <div className="card-body p-0">
              {!carreraSeleccionada ? (
                <div className="p-5 text-center text-muted">
                  <h1 className="display-4">🎓</h1>
                  <p>Haz clic en una carrera de la izquierda para ver sus asignaturas.</p>
                </div>
              ) : (
                <div className="table-responsive">
                  <table className="table table-hover align-middle mb-0">
                    <thead className="table-light">
                      <tr>
                        <th>Nivel</th>
                        <th>Asignatura</th>
                        <th>Créditos</th>
                        <th>Horas</th>
                      </tr>
                    </thead>
                    <tbody>
                      {asignaturas.length === 0 ? (
                        <tr><td colSpan="4" className="text-center py-4">Cargando asignaturas...</td></tr>
                      ) : (
                        asignaturas.map(a => (
                          <tr key={a.id}>
                            <td>{a.nivel}</td>
                            <td>{a.nombre}</td>
                            <td>{a.creditos}</td>
                            <td>{a.horas}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </div>

      </div>
    </div>
  );
}