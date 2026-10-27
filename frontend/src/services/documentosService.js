const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

function getAuthHeaders() {
  const token = localStorage.getItem("token");
  return { Authorization: `Bearer ${token}` };
}

export async function obtenerMisSolicitudes() {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/mias`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar solicitudes");
  return await res.json();
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