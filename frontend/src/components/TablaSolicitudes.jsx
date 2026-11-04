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
            <th className="text-center">N°</th>
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
              {/* Mostramos el Numero (SOL-0001) o el ID */}
              <td className="text-center fw-bold">{sol.numero || sol.id}</td>
              
              {/* Formateo de fecha seguro */}
              <td>
                {sol.fechaRegistro 
                  ? new Date(sol.fechaRegistro).toLocaleDateString() 
                  : sol.fecha 
                  ? new Date(sol.fecha).toLocaleDateString() 
                  : '-'}
              </td>
              
              <td>{sol.tipo || '-'}</td>
              
              {/* Buscamos sol.origen y sol.destino */}
              <td>{sol.origen || sol.carreraOrigen || '-'}</td>
              <td>{sol.destino || sol.carreraDestino || '-'}</td>
              
              <td className="text-center">
                <span className="badge bg-success p-2">
                  {sol.estado || 'REGISTRADA'}
                </span>
              </td>

              {/* NUEVA COLUMNA DE ACCIONES */}
              <td className="text-center">
                <Link 
                  to={`/resultado/${sol.id}`} 
                  className="btn btn-sm btn-outline-primary fw-bold"
                >
                  Ver Análisis
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};