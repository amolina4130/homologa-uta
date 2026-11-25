const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = sessionStorage.getItem("token");
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

// Solicitudes en revisión. Con { incluirEmitidos: true } también trae las que ya tienen
// informe final (trámite terminado), para consultarlas o corregirlas.
export async function obtenerSolicitudesPendientes({ incluirEmitidos = false } = {}) {
  const query = incluirEmitidos ? "?incluirEmitidos=true" : "";
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/pendientes${query}`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "Error al cargar solicitudes pendientes");
  const json = await res.json();
  return extraerDatos(json);
}

export async function obtenerDetalleSolicitud(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/detalle`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "Error al cargar el detalle");
  const json = await res.json();
  return extraerDatos(json);
}

export async function emitirInforme(id, informeCompleto) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/informe`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(informeCompleto),
  });
  if (!res.ok) await lanzarError(res, "Error al emitir el informe");
  const json = await res.json();
  return extraerDatos(json);
}

// Delega la revisión a uno o dos docentes.
export async function delegarDocentes(id, correos) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/delegar`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({ correos }),
  });
  if (!res.ok) await lanzarError(res, "No se pudieron delegar los docentes");
  const json = await res.json();
  return extraerDatos(json);
}

// Estado de las delegaciones realizadas por el coordinador.
export async function obtenerDelegaciones(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/delegaciones`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudieron cargar las delegaciones");
  const json = await res.json();
  return extraerDatos(json);
}

// Detalle, delegaciones y textos de informes sugerido/final.
export async function obtenerConsolidado(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/consolidado`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo cargar el consolidado");
  const json = await res.json();
  return extraerDatos(json);
}

// Emite el informe final que el Coordinador envía al Comité (cierra el trámite).
// Genera una vista previa editable del informe final sin emitirlo.
export async function previsualizarInformeFinal(id, payload) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/informe-final/vista-previa`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(payload),
  });
  if (!res.ok) await lanzarError(res, "No se pudo generar la vista previa del informe final");
  const json = await res.json();
  return extraerDatos(json);
}

// Emite el informe final que el Coordinador envía al Comité (cierra el trámite).
export async function emitirInformeFinal(id, payload) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/informe-final`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(payload),
  });
  if (!res.ok) await lanzarError(res, "No se pudo emitir el informe final");
  const json = await res.json();
  return extraerDatos(json);
}

// Descarga el PDF del informe final con el token de la sesión.
export async function descargarInformeFinal(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/informe-final/pdf`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo descargar el informe final");

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = `Informe_final_${id}.pdf`;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}

// Descarga el informe sugerido que el Docente envió al Coordinador.
export async function descargarInformeSugeridoDocente(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/informe-docente/pdf`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo descargar el informe sugerido del Docente");

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = `Informe_sugerido_docente_SOL-${String(id).padStart(4, "0")}.pdf`;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}

// HU-17: registra la resolución del Consejo Directivo (solo con informe emitido).
export async function registrarResolucion(id, resolucion) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${id}/resolucion`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(resolucion),
  });
  if (!res.ok) await lanzarError(res, "Error al registrar la resolución");
  const json = await res.json();
  return extraerDatos(json);
}

// Docentes registrados, para elegir a quién delegar (nombre, correo y título).
export async function obtenerDocentes() {
  const res = await fetch(`${GATEWAY_URL}/auth/docentes`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo cargar la lista de docentes");
  return await res.json();
}
