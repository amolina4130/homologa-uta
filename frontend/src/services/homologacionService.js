const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`
  };
};

const extraerDatos = (json) => {
  return json.data !== undefined ? json.data : json;
};

const lanzarError = async (res, mensajePorDefecto) => {
  let mensaje = mensajePorDefecto;
  let reintentable = false;
  try {
    const cuerpo = await res.json();
    if (cuerpo && (cuerpo.error || cuerpo.mensaje)) {
      mensaje = cuerpo.error || cuerpo.mensaje;
    }
    if (cuerpo && cuerpo.reintentable) reintentable = true;
  } catch {
    // La respuesta no trae JSON; se conserva el mensaje por defecto.
  }

  const error = new Error(mensaje);
  error.status = res.status;
  error.reintentable = reintentable;
  throw error;
};

// Obtener el resultado del análisis de una solicitud
export async function obtenerResultadoAnalisis(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/resultado`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "Error al cargar el resultado del análisis");
  const json = await res.json();
  return extraerDatos(json);
}

// Generar (o regenerar) el análisis de una solicitud con las asignaturas reales
export async function analizarSolicitud(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/analizar`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({}),
  });
  if (!res.ok) await lanzarError(res, "Error al generar el análisis");
  const json = await res.json();
  return extraerDatos(json);
}

// Generar el análisis externo (HU-09). La documentación procesada se toma de la solicitud.
export async function analizarSolicitudExterna(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/analizar-externo`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({}),
  });
  if (!res.ok) await lanzarError(res, "Error al generar el análisis externo");
  const json = await res.json();
  return extraerDatos(json);
}

// Validar las reglas objetivas (HU-07)
export async function validarReglasObjetivas(solicitudId, datos) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/validar`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(datos ?? {}),
  });
  if (!res.ok) await lanzarError(res, "Error al validar las reglas objetivas");
  const json = await res.json();
  return extraerDatos(json);
}

// HU-20: reutiliza el récord ya subido y extrae sus materias y notas.
export async function extraerRecord(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/record/extraer`, {
    method: "POST",
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo extraer el récord académico");
  return await res.json();
}

// HU-20: materias aprobadas guardadas para la solicitud.
export async function obtenerMateriasAprobadas(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/materias-aprobadas`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudieron cargar las materias aprobadas");
  return await res.json();
}

// HU-20: guarda la lista corregida por el estudiante.
export async function actualizarMateriasAprobadas(solicitudId, materias) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/materias-aprobadas`, {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify({ materias }),
  });
  if (!res.ok) await lanzarError(res, "No se pudieron guardar las materias aprobadas");
  return await res.json();
}

// HU-17: resolución del Consejo Directivo (null si todavía no existe).
export async function obtenerResolucion(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/resolucion`, {
    headers: getAuthHeaders(),
  });
  if (res.status === 404) return null;
  if (!res.ok) await lanzarError(res, "No se pudo cargar la resolución del Consejo");
  return await res.json();
}

// HU-18: descarga el informe PDF y lo entrega al navegador.
export async function descargarInforme(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/informe`, {
    headers: getAuthHeaders(),
  });

  if (!res.ok) {
    await lanzarError(res, "No se pudo descargar el informe");
  }

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = `Informe_SOL-${String(solicitudId).padStart(4, "0")}.pdf`;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}
