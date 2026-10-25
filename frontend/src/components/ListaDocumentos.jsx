export default function ListaDocumentos({ documentos }) {
  if (!documentos || documentos.length === 0) {
    return <p style={{ marginTop: 20 }}>No hay documentos adjuntos a esta solicitud.</p>;
  }

  const tabla = { width: "100%", borderCollapse: "collapse", border: "2px solid #000", marginTop: 20 };
  const celda = { border: "1px solid #000", padding: 8, textAlign: "left" };

  return (
    <table style={tabla}>
      <thead>
        <tr>
          <th style={celda}>Documento</th>
          <th style={celda}>Tipo</th>
          <th style={celda}>Tamaño</th>
          <th style={celda}>Estado</th>
        </tr>
      </thead>
      <tbody>
        {documentos.map((doc) => (
          <tr key={doc.id}>
            <td style={celda}>{doc.nombre}</td>
            <td style={celda}>{doc.tipo}</td>
            <td style={celda}>{doc.tamano}</td>
            <td style={celda}>
              {doc.estado === "PROCESADO" ? "●" : "○"} {doc.estado}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}