//import React from 'react';

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
          </tr>
        </thead>
        <tbody>
          {solicitudes.map((sol) => (
            <tr key={sol.id}>
              <td className="text-center fw-bold">{sol.id}</td>
              
              {/* Formateo de fecha seguro */}
              <td>
                {sol.fechaRegistro 
                  ? new Date(sol.fechaRegistro).toLocaleDateString() 
                  : sol.fecha 
                  ? new Date(sol.fecha).toLocaleDateString() 
                  : '-'}
              </td>
              
              <td>{sol.tipo || '-'}</td>
              
              <td>{sol.carreraOrigenNombre || sol.carreraOrigen || sol.carreraOrigenId || '-'}</td>
              <td>{sol.carreraDestinoNombre || sol.carreraDestino || sol.carreraDestinoId || '-'}</td>
              
              <td className="text-center">
                <span className="badge bg-success p-2">
                  {sol.estado || 'REGISTRADA'}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};