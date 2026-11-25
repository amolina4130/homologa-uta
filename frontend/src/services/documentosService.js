import { GATEWAY_URL } from "./config";

function getAuthHeaders() {
  const token = sessionStorage.getItem("token");
  return { Authorization: `Bearer ${token}` };
}

export async function obtenerMisSolicitudes() {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/mias`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar solicitudes");
  return await res.json();
}

// Documentos ya guardados para una solicitud del estudiante autenticado.
export async function obtenerDocumentosSolicitud(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/documentos/solicitud/${solicitudId}`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("No se pudieron cargar los documentos de la solicitud");
  return await res.json();
}

// Lee el texto de un PDF sin guardarlo (para prellenar los datos de una nueva solicitud).
export async function leerTextoPdf(archivo, { diseno = false } = {}) {
  const formData = new FormData();
  formData.append("archivo", archivo);
  const res = await fetch(`${GATEWAY_URL}/documentos/leer-texto${diseno ? "?diseno=true" : ""}`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: formData,
  });
  const cuerpo = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(cuerpo.error || "No se pudo leer el PDF.");
  return cuerpo.texto || "";
}

export async function subirDocumento(solicitudId, tipoDocumento, archivo) {
  const formData = new FormData();
  formData.append("Archivo", archivo);
  formData.append("SolicitudId", solicitudId);
  formData.append("TipoDocumento", tipoDocumento);

  // Sin Content-Type: el navegador lo pone solo al enviar FormData
  const res = await fetch(`${GATEWAY_URL}/documentos`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: formData,
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => ({}));
    // El backend rechaza archivos con 400 y { error: "..." }
    const mensaje =
      res.status === 400 && errorData.error
        ? errorData.error
        : "No se pudo subir el documento. Intente más tarde.";
    throw new Error(mensaje);
  }
  return await res.json();
}