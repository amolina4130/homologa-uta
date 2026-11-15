const formatoFecha = (valor) =>
  valor ? new Date(valor).toLocaleString("es-EC") : "-";

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
              <th>N°</th>
              <th>Fecha de registro</th>
              <th>Estudiante</th>
              <th>Tipo</th>
              <th>Carreras</th>
              <th className="text-center">Asignaturas</th>
              <th>Fecha del análisis</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {cargando ? (
              <tr><td colSpan="8" className="text-center py-4">Cargando solicitudes...</td></tr>
            ) : solicitudes.length === 0 ? (
              <tr><td colSpan="8" className="text-center py-4">No hay solicitudes pendientes.</td></tr>
            ) : (
              solicitudes.map((sol) => {
                const idSolicitud = sol.solicitudId;
                const origen = sol.carreraOrigen || "-";
                const destino = sol.carreraDestino || "-";

                return (
                  <tr key={idSolicitud}>
                    <td className="fw-bold">SOL-{String(idSolicitud).padStart(4, "0")}</td>
                    <td>{formatoFecha(sol.fecha)}</td>
                    <td>{sol.estudiante}</td>
                    <td>{sol.tipo}</td>
                    <td>{origen} → {destino}</td>
                    <td className="text-center">
                      <span className="badge bg-secondary">
                        {sol.totalAsignaturas ?? 0}
                      </span>
                    </td>
                    <td>{formatoFecha(sol.fechaAnalisis)}</td>
                    <td>
                      <button className="btn btn-sm btn-outline-primary" onClick={() => onAbrir(idSolicitud)}>
                        Ver detalle
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
