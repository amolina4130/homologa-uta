export default function ListaSolicitudes({ solicitudes, cargando, onAbrir }) {
  return (
    <div className="card shadow-sm border-0">
      <div className="card-header bg-primary text-white">
        <h5 className="mb-0">Solicitudes pendientes de revisión</h5>
      </div>
      <div className="card-body p-0">
        <table className="table table-hover mb-0">
          <thead className="table-light">
            <tr>
              <th>Fecha</th>
              <th>Estudiante</th>
              <th>Tipo</th>
              <th>Carreras</th>
              <th>Plazo</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {cargando ? (
              <tr><td colSpan="6" className="text-center py-4">Cargando solicitudes...</td></tr>
            ) : solicitudes.length === 0 ? (
              <tr><td colSpan="6" className="text-center py-4">No hay solicitudes pendientes.</td></tr>
            ) : (
              solicitudes.map((sol) => (
                <tr key={sol.id}>
                  <td>{new Date(sol.fecha).toLocaleDateString("es-EC")}</td>
                  <td>{sol.estudiante}</td>
                  <td>{sol.tipo}</td>
                  <td>{sol.origen} → {sol.destino}</td>
                  <td>
                    {/* TODO: Calcular desde el backend usando la fecha límite */}
                    <span className="badge bg-warning text-dark" title="Plazo máximo normativo (5 días término)">5 días</span>
                  </td>
                  <td>
                    <button className="btn btn-sm btn-outline-primary" onClick={() => onAbrir(sol.id)}>
                      Abrir
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}