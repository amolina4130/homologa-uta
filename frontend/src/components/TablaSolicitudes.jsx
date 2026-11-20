import { Link } from "react-router-dom";

export const TablaSolicitudes = ({ solicitudes }) => {
  if (!solicitudes || solicitudes.length === 0) {
    return (
      <div className="alert alert-info text-center shadow-sm mt-3" role="alert">
        No tienes solicitudes registradas por el momento.
      </div>
    );
  }

  return (
    <div className="table-responsive mt-3">
      <table className="table table-hover table-bordered shadow-sm align-middle">
        <thead className="table-primary">
          <tr>
            <th className="text-center">N° Proceso</th>
            <th>Fecha</th>
            <th>Tipo</th>
            <th>Carrera Origen</th>
            <th>Carrera Destino</th>
            <th className="text-center">Estado</th>
            <th className="text-center">Acciones</th>
          </tr>
        </thead>
        <tbody>
          {solicitudes.map((sol) => (
            <tr key={sol.id}>
              
              {/* 1. Mostramos el numeroProceso. Si el backend no lo envía, lo simulamos con el ID */}
              <td className="text-center fw-bold text-primary">
                {sol.numeroProceso || `SOL-${sol.id.toString().padStart(4, '0')}`}
              </td>
              
              <td>
                {sol.fechaRegistro 
                  ? new Date(sol.fechaRegistro).toLocaleDateString() 
                  : sol.fecha 
                  ? new Date(sol.fecha).toLocaleDateString() 
                  : '-'}
              </td>
              
              <td>{sol.tipo || 'Homologación'}</td>
              
              <td>{sol.origen || sol.carreraOrigen || '-'}</td>
              <td>{sol.destino || sol.carreraDestino || '-'}</td>
              
              <td className="text-center">
                <span
                  className={`badge p-2 ${
                    sol.estado === "INFORME_EMITIDO" || sol.estado === "RESUELTA"
                      ? "bg-success"
                      : sol.estado === "PENDIENTE_REVISION"
                      ? "bg-warning text-dark"
                      : "bg-primary" // REGISTRADA en azul
                  }`}
                >
                  {sol.estado || 'REGISTRADA'}
                </span>
              </td>

              {/* 2. Columna de Acciones con Lógica Condicional */}
              <td className="text-center">
                <div className="d-flex justify-content-center gap-2">
                  <Link 
                    to={`/resultado/${sol.id}`} 
                    className="btn btn-sm btn-outline-secondary fw-bold"
                  >
                    Ver detalle
                  </Link>

                  {/* El botón Editar solo aparece si el estado es REGISTRADA */}
                  {(sol.estado === "REGISTRADA" || !sol.estado) && (
                    <Link 
                      to={`/editar-solicitud/${sol.id}`} 
                      className="btn btn-sm btn-outline-primary fw-bold"
                    >
                      Editar
                    </Link>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};