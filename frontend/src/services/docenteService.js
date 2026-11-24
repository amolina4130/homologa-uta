const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = sessionStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
};

const extraerDatos = (json) => (json && json.data !== undefined ? json.data : json);

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

export async function obtenerSolicitudesDocente() {
  const res = await fetch(`${GATEWAY_URL}/homologacion/docente/solicitudes`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudieron cargar las solicitudes asignadas");
  return extraerDatos(await res.json());
}

export async function obtenerDetalleDocente(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/docente/${id}/detalle`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo cargar el detalle de la solicitud");
  return extraerDatos(await res.json());
}

export async function generarSugerenciaDocente(id, payload = {}) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/docente/${id}/sugerencia`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(payload),
  });
  if (!res.ok) await lanzarError(res, "No se pudo generar la sugerencia");
  return extraerDatos(await res.json());
}

export async function obtenerRecomendacionInterna(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/docente/${id}/recomendacion`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo obtener la recomendación del agente interno");
  return extraerDatos(await res.json());
}

/**
 * Ayuda del agente interno al seleccionar carrera, antes de registrar la solicitud.
 * No necesita id de solicitud ni delegación.
 */
export async function recomendarPorCarrera(payload) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/recomendacion-carrera`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(payload),
  });
  if (!res.ok) await lanzarError(res, "No se pudo obtener la recomendación del agente interno");
  return extraerDatos(await res.json());
}

export async function evaluarSolicitud(id, payload) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/docente/${id}/evaluar`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(payload),
  });
  if (!res.ok) await lanzarError(res, "No se pudo registrar la evaluación");
  return extraerDatos(await res.json());
}

export async function descargarInformeDocente(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/docente/${id}/informe`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo descargar el informe sugerido");

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = `Informe_sugerido_SOL-${String(id).padStart(4, "0")}.pdf`;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}
