const formatoFecha = (valor) =>
  valor ? new Date(valor).toLocaleString("es-EC") : "-";

const claseEstado = (estado) => {
  switch (estado) {
    case "RESUELTA":
      return "bg-success";
    case "INFORME_EMITIDO":
      return "bg-info text-dark";
    case "PENDIENTE_REVISION":
      return "bg-warning text-dark";
    default:
      return "bg-secondary";
  }
};

export default function ListaSolicitudes({ solicitudes, cargando, onAbrir }) {
  return (
    <div className="card shadow-sm border-0">
      <div className="card-header bg-primary text-white">
        <h5 className="mb-0">Solicitudes pendientes de revisión</h5>
      </div>
      <div className="card-body p-0">
        <div className="table-responsive">
          <table className="table table-hover mb-0">
            <thead className="table-light">
              <tr>
                <th>N° de proceso</th>
                <th>Fecha de registro</th>
                <th>Titular (récord)</th>
                <th>Registrado por</th>
                <th>Modalidad</th>
                <th>Tipo</th>
                <th>Carreras</th>
                <th className="text-center">Asignaturas</th>
                <th>Fecha del análisis</th>
                <th className="text-center">Estado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan="11" className="text-center py-4">Cargando solicitudes...</td></tr>
              ) : solicitudes.length === 0 ? (
                <tr><td colSpan="11" className="text-center py-4">No hay solicitudes pendientes.</td></tr>
              ) : (
                solicitudes.map((sol) => {
                  const idSolicitud = sol.solicitudId;
                  const numero =
                    sol.numeroProceso || `SOL-${String(idSolicitud).padStart(4, "0")}`;
                  const origen = sol.carreraOrigen || "-";
                  const destino = sol.carreraDestino || "-";

                  return (
                    <tr key={idSolicitud}>
                      <td className="fw-bold">{numero}</td>
                      <td>{formatoFecha(sol.fecha)}</td>
                      <td>
                        <span className="fw-semibold">{sol.estudiante || "-"}</span>
                        <div className="small text-muted">
                          C.I.: {sol.estudianteCedula || "-"}
                        </div>
                      </td>
                      <td>{sol.comiteCorreo || "-"}</td>
                      <td>
                        <span
                          className={`badge ${
                            sol.modulo === "EXTERNO" ? "bg-info text-dark" : "bg-primary"
                          }`}
                        >
                          {sol.modulo || "INTERNO"}
                        </span>
                      </td>
                      <td>{sol.tipo}</td>
                      <td>{origen} → {destino}</td>
                      <td className="text-center">
                        <span className="badge bg-secondary">
                          {sol.totalAsignaturas ?? 0}
                        </span>
                      </td>
                      <td>{formatoFecha(sol.fechaAnalisis)}</td>
                      <td className="text-center">
                        <span className={`badge ${claseEstado(sol.estado)}`}>
                          {sol.estado || "PENDIENTE_REVISION"}
                        </span>
                      </td>
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
    </div>
  );
}
