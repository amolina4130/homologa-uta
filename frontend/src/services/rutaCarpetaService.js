import { GATEWAY_URL } from "./config";

const lanzarError = async (res, mensajePorDefecto) => {
  let mensaje = mensajePorDefecto;
  try {
    const cuerpo = await res.json();
    if (cuerpo && (cuerpo.mensaje || cuerpo.error)) {
      mensaje = cuerpo.mensaje || cuerpo.error;
    }
  } catch {
    // La respuesta no trae JSON; se conserva el mensaje por defecto.
  }

  const error = new Error(mensaje);
  error.status = res.status;
  throw error;
};

// HU: procesa una carpeta local con malla, récord, planes analíticos y sílabos.
export async function procesarCarpeta({ solicitudId, ruta, usuario }) {
  const token = sessionStorage.getItem("token");
  const res = await fetch(`${GATEWAY_URL}/documentos/carpeta`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ solicitudId, ruta, usuario }),
  });

  if (!res.ok) await lanzarError(res, "No se pudo procesar la carpeta de documentos");
  const json = await res.json();
  return json && json.data !== undefined ? json.data : json;
}

// HU: registra la URL de la carpeta compartida por el titular (homologación externa).
export async function registrarUrlExterna({ solicitudId, url, usuario }) {
  const token = sessionStorage.getItem("token");
  const res = await fetch(`${GATEWAY_URL}/documentos/url-externa`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ solicitudId, url, usuario }),
  });

  if (!res.ok) await lanzarError(res, "No se pudo registrar la URL externa");
  const json = await res.json();
  return json && json.data !== undefined ? json.data : json;
}
