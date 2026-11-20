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
    if (cuerpo && cuerpo.error) mensaje = cuerpo.error;
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

// Descargar el informe en PDF (HU-18)
export async function descargarInformePDF(solicitudId, numeroProceso) {
  try {
    const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/informe`, {
      method: "GET",
      headers: getAuthHeaders(),
    });

    if (!res.ok) {
      if (res.status === 404) throw new Error("El informe aún no ha sido generado (404).");
      if (res.status === 403) throw new Error("No tienes permiso para descargar este informe (403).");
      await lanzarError(res, "Error al descargar el informe.");
    }

    // Convertimos la respuesta en un archivo (Blob) y forzamos la descarga
    const blob = await res.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    // Nombre del archivo: Informe_SOL-XXXX.pdf
    a.download = `Informe_${numeroProceso || `SOL-${solicitudId}`}.pdf`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);

  } catch (error) {
    
    console.warn("Simulando descarga de PDF (Mock)...");
    
    if (!numeroProceso && !solicitudId) {
      throw new Error("No se puede descargar el informe: datos incompletos.");
    }

  
    const contenidoFalso = `Este es un PDF de prueba simulado para el informe de la solicitud ${numeroProceso || solicitudId}.`;
    const blob = new Blob([contenidoFalso], { type: "application/pdf" });
    
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `Informe_${numeroProceso || `SOL-${solicitudId}`}.pdf`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);
  }
}
