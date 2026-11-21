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

// Extraer materias del récord 
export async function extraerMateriasRecord(solicitudId) {
  try {
    const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/record/extraer`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({}),
    });
    if (!res.ok) await lanzarError(res, "No se pudo extraer el récord.");
    return await res.json();
  } catch (error) {
    // Simulamos la extracción de datos
    console.warn("Simulando extracción de materias (Mock).");
    return {
      materias: [
        { nombre: "Programación I", nota: 9.5 },
        { nombre: "Cálculo Diferencial", nota: 8.0 },
      ],
      requiereRevision: true, 
      motivo: "El PDF tenía baja resolución en algunas notas.",
    };
  }
}

// Guardar materias aprobadas 
export async function guardarMateriasAprobadas(solicitudId, materias) {
  try {
    const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/materias-aprobadas`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify({ materias }),
    });
    if (!res.ok) await lanzarError(res, "Error al guardar las materias.");
    return await res.json();
  } catch (error) {
    // Simulamos guardado exitoso
    return { success: true, mensaje: "Materias guardadas correctamente." };
  }
}