import { useState } from "react";
import { formatearTamano } from "../utils/formato";

const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

export default function ListaDocumentos({ documentos }) {
  const [pdfVista, setPdfVista] = useState(null);

  if (!documentos || documentos.length === 0) {
    return <p className="mb-3">No hay documentos adjuntos a esta solicitud.</p>;
  }

  const cerrarVista = () => setPdfVista(null);

  return (
    <>
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
            const idDoc = doc.id || doc.Id;
            const nombreArchivo =
              doc.nombre ||
              doc.Nombre ||
              doc.nombreArchivo ||
              doc.NombreArchivo ||
              "Documento.pdf";

            return (
              <tr key={index}>
                <td>{nombreArchivo}</td>
                <td>{doc.tipoDocumento || doc.TipoDocumento || "Malla"}</td>
                <td>{formatearTamano(doc)}</td>
                <td>
                  <span className="badge bg-success">
                    ● {doc.estado || doc.Estado || "PROCESADO"}
                  </span>
                </td>
                <td className="text-center">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-primary"
                    onClick={() =>
                      setPdfVista({
                        url: `${GATEWAY_URL}/documentos/descargar/${idDoc}#toolbar=0&navpanes=0`,
                        nombre: nombreArchivo,
                      })
                    }
                  >
                    Ver PDF
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {pdfVista && (
        <div
          className="modal d-block"
          tabIndex="-1"
          role="dialog"
          aria-modal="true"
          aria-label={`Vista de ${pdfVista.nombre}`}
          style={{ backgroundColor: "rgba(0,0,0,0.55)" }}
          onClick={cerrarVista}
        >
          <div
            className="modal-dialog modal-xl modal-dialog-centered"
            style={{ maxWidth: "95vw" }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-content">
              <div className="modal-header py-2">
                <h6 className="modal-title mb-0 text-truncate" title={pdfVista.nombre}>
                  {pdfVista.nombre}
                </h6>
                <button type="button" className="btn-close" aria-label="Cerrar" onClick={cerrarVista} />
              </div>
              <div className="modal-body p-0" style={{ height: "80vh" }}>
                <iframe
                  title={pdfVista.nombre}
                  src={pdfVista.url}
                  style={{ width: "100%", height: "100%", border: 0 }}
                />
              </div>
              <div className="modal-footer py-2">
                <small className="text-muted me-auto">Vista de solo lectura.</small>
                <button type="button" className="btn btn-secondary btn-sm" onClick={cerrarVista}>
                  Cerrar
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
