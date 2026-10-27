export default function ListaDocumentos({ documentos }) {
  if (!documentos || documentos.length === 0) {
    return <p className="mb-3">No hay documentos adjuntos a esta solicitud.</p>;
  }

  return (
    <table className="table table-bordered mt-3">
      <thead className="table-secondary">
        <tr>
          <th>Documento</th>
          <th>Tipo</th>
          <th>Tamaño</th>
          <th>Estado</th>
        </tr>
      </thead>
      <tbody>
        {documentos.map((doc) => (
          <tr key={doc.id || doc.nombre}>
            <td>{doc.nombre}</td>
            <td>{doc.tipo}</td>
            <td>{doc.tamano}</td>
            <td>{doc.estado === "PROCESADO" ? "● PROCESADO" : "○ CARGADO"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}