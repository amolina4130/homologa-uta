export default function ListaDocumentos({ documentos }) {
  if (!documentos || documentos.length === 0) {
    return <p className="mb-3">No hay documentos adjuntos a esta solicitud.</p>;
  }

  return (
    <table className="table table-bordered mt-3 align-middle">
      <thead className="table-secondary">
        <tr>
          <th>Documento</th>
          <th>Tipo</th>
          <th>Tamaño</th>
          <th>Estado</th>
          <th className="text-center">Acciones</th>
        </tr>
      </thead>
      <tbody>
        {documentos.map((doc, index) => {
          // Buscamos el ID en cualquiera de sus formatos
          const idDoc = doc.id || doc.Id;
          
          // Buscamos el nombre original para mostrarlo en la tabla
          const nombreArchivo = doc.nombre || doc.Nombre || doc.nombreArchivo || doc.NombreArchivo || "Documento.pdf";

          return (
            <tr key={index}>
              <td>{nombreArchivo}</td>
              <td>{doc.tipoDocumento || doc.TipoDocumento || "Malla"}</td>
              <td>{doc.tamano || "-"}</td>
              <td>
                <span className="badge bg-success">
                  ● {doc.estado || doc.Estado || "PROCESADO"}
                </span>
              </td>
              <td className="text-center">
                {/* ENLACE DIRECTO POR ID */}
                <a 
                  href={`http://localhost:8080/documentos/descargar/${idDoc}`} 
                  target="_blank" 
                  rel="noopener noreferrer"
                  className="btn btn-sm btn-outline-primary"
                >
                  Ver PDF
                </a>
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}