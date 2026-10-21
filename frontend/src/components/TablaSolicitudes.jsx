import React from 'react';

export const TablaSolicitudes = ({ solicitudes }) => {
  if (!solicitudes || solicitudes.length === 0) {
    return <p style={{ padding: '10px' }}>No tienes solicitudes registradas.</p>;
  }

  return (
    <table border="1" style={{ width: '100%', textAlign: 'left', borderCollapse: 'collapse', marginTop: '10px' }}>
      <thead>
        <tr style={{ backgroundColor: '#f0f0f0' }}>
          <th style={{ padding: '8px' }}>N°</th>
          <th style={{ padding: '8px' }}>Fecha</th>
          <th style={{ padding: '8px' }}>Tipo</th>
          <th style={{ padding: '8px' }}>Carrera Origen</th>
          <th style={{ padding: '8px' }}>Carrera Destino</th>
          <th style={{ padding: '8px' }}>Estado</th>
        </tr>
      </thead>
      <tbody>
        {solicitudes.map((sol) => (
          <tr key={sol.id}>
            <td style={{ padding: '8px' }}>{sol.id}</td>
            
            {/* Formateo de fecha seguro: si viene nula, muestra un guion */}
            <td style={{ padding: '8px' }}>
              {sol.fechaRegistro 
                ? new Date(sol.fechaRegistro).toLocaleDateString() 
                : sol.fecha 
                ? new Date(sol.fecha).toLocaleDateString() 
                : '-'}
            </td>
            
            <td style={{ padding: '8px' }}>{sol.tipo || '-'}</td>
            
            {/* Si Anahi devuelve el nombre de la carrera, lo usa. Si devuelve el ID, lo muestra */}
            <td style={{ padding: '8px' }}>{sol.carreraOrigenNombre || sol.carreraOrigen || sol.carreraOrigenId || '-'}</td>
            <td style={{ padding: '8px' }}>{sol.carreraDestinoNombre || sol.carreraDestino || sol.carreraDestinoId || '-'}</td>
            
            <td style={{ padding: '8px' }}>
              <strong>{sol.estado || 'REGISTRADA'}</strong>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
};