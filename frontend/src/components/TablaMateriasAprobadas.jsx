import { useState } from "react";

export default function TablaMateriasAprobadas({ materiasIniciales = [], onGuardar, requiereRevision, motivo }) {
  const [materias, setMaterias] = useState(materiasIniciales);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  // Agregar una fila vacía
  const agregarFila = () => {
    setMaterias([...materias, { nombre: "", nota: "" }]);
  };

  // Eliminar una fila por índice
  const eliminarFila = (index) => {
    const nuevas = materias.filter((_, i) => i !== index);
    setMaterias(nuevas);
  };

  // Editar un campo de la fila
  const editarFila = (index, campo, valor) => {
    const nuevas = [...materias];
    nuevas[index][campo] = valor;
    setMaterias(nuevas);
  };

  const handleGuardar = async () => {
    setError(""); setExito("");
    // Validar que no haya campos vacíos
    if (materias.some(m => !m.nombre.trim() || !m.nota.toString().trim())) {
      setError("Todas las materias deben tener un nombre y una nota.");
      return;
    }

    try {
      await onGuardar(materias);
      setExito("Materias guardadas correctamente.");
    } catch (err) {
      setError(err.message || "Error al guardar las materias.");
    }
  };

  return (
    <div className="card shadow-sm mt-4 border-info">
      <div className="card-header bg-info text-white">
        <h5 className="mb-0">Revisar Materias Aprobadas Extraídas</h5>
      </div>
      <div className="card-body">
        
        {/* AVISO DE REQUIERE REVISIÓN (Criterio) */}
        {requiereRevision && (
          <div className="alert alert-warning">
            <strong> Atención:</strong> El sistema no pudo leer perfectamente el récord. Motivo: {motivo || "Formato no reconocido"}. Por favor, revise y corrija las materias a continuación.
          </div>
        )}

        {error && <div className="alert alert-danger py-2">{error}</div>}
        {exito && <div className="alert alert-success py-2">{exito}</div>}

        <div className="table-responsive">
          <table className="table table-bordered align-middle">
            <thead className="table-light">
              <tr>
                <th>Materia</th>
                <th style={{ width: "150px" }}>Nota</th>
                <th style={{ width: "80px" }} className="text-center">Acción</th>
              </tr>
            </thead>
            <tbody>
              {materias.length === 0 ? (
                <tr><td colSpan="3" className="text-center text-muted">No hay materias extraídas. Agregue una manualmente.</td></tr>
              ) : (
                materias.map((m, index) => (
                  <tr key={index}>
                    <td>
                      <input 
                        type="text" 
                        className="form-control form-control-sm" 
                        value={m.nombre} 
                        onChange={(e) => editarFila(index, "nombre", e.target.value)} 
                        placeholder="Nombre de la materia"
                      />
                    </td>
                    <td>
                      <input 
                        type="number" 
                        step="0.01"
                        className="form-control form-control-sm" 
                        value={m.nota} 
                        onChange={(e) => editarFila(index, "nota", e.target.value)} 
                        placeholder="Ej. 8.5"
                      />
                    </td>
                    <td className="text-center">
                      <button className="btn btn-sm btn-outline-danger" onClick={() => eliminarFila(index)}>
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="d-flex justify-content-between mt-3">
          <button className="btn btn-outline-primary btn-sm" onClick={agregarFila}>
            + Agregar Materia
          </button>
          <button className="btn btn-primary px-4" onClick={handleGuardar}>
            Guardar Materias
          </button>
        </div>
      </div>
    </div>
  );
}