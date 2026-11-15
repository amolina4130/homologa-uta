const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
};

const extraer = (json) => (json && json.data !== undefined ? json.data : json);

async function peticion(ruta, opciones = {}) {
  const res = await fetch(`${GATEWAY_URL}${ruta}`, {
    ...opciones,
    headers: { ...getAuthHeaders(), ...(opciones.headers || {}) },
  });

  if (res.status === 401) {
    const err = new Error("Sesión expirada o no autorizada");
    err.tipo = "no_autorizado";
    throw err;
  }
  if (!res.ok) {
    const cuerpo = await res.json().catch(() => ({}));
    const err = new Error(cuerpo.mensaje || cuerpo.error || "Error del servidor");
    err.status = res.status;
    throw err;
  }
  if (res.status === 204) return null;
  return extraer(await res.json().catch(() => null));
}

// --- Equivalencias (HU-14) ---
export const listarEquivalencias = (filtros = {}) => {
  const params = new URLSearchParams();
  if (filtros.carreraOrigenId) params.append("carreraOrigenId", filtros.carreraOrigenId);
  if (filtros.carreraDestinoId) params.append("carreraDestinoId", filtros.carreraDestinoId);
  if (filtros.activa !== undefined && filtros.activa !== "") params.append("activa", filtros.activa);
  const qs = params.toString();
  return peticion(`/equivalencias${qs ? `?${qs}` : ""}`);
};

export const crearEquivalencia = (datos) =>
  peticion("/equivalencias", { method: "POST", body: JSON.stringify(datos) });

export const actualizarEquivalencia = (id, datos) =>
  peticion(`/equivalencias/${id}`, { method: "PUT", body: JSON.stringify(datos) });

// --- Memoria del agente (HU-19) ---
export const obtenerEstadoMemoria = () => peticion("/memoria/estado");
export const cargarMemoria = () => peticion("/memoria/carga", { method: "POST" });
export const obtenerAsignaturasMemoria = (carreraId, nivelMaximo) => {
  const qs = nivelMaximo ? `?nivelMaximo=${nivelMaximo}` : "";
  return peticion(`/memoria/carreras/${carreraId}/asignaturas${qs}`);
};

// --- Planes analíticos (HU-13) ---
export async function cargarPlanesAnaliticos(archivos) {
  const formData = new FormData();
  for (const archivo of archivos) formData.append("archivos", archivo);

  const res = await fetch(`${GATEWAY_URL}/planes-analiticos/carga`, {
    method: "POST",
    headers: { Authorization: `Bearer ${localStorage.getItem("token")}` },
    body: formData,
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(json.mensaje || json.error || "No se pudieron cargar los planes analíticos.");
  }
  return extraer(json);
}

// --- Récord académico (HU-20) y preselección (HU-21) ---
export const extraerRecord = (solicitudId) =>
  peticion(`/homologacion/${solicitudId}/record/extraer`, { method: "POST" });
export const obtenerMateriasAprobadas = (solicitudId) =>
  peticion(`/homologacion/${solicitudId}/materias-aprobadas`);
export const guardarMateriasAprobadas = (solicitudId, materias) =>
  peticion(`/homologacion/${solicitudId}/materias-aprobadas`, {
    method: "PUT",
    body: JSON.stringify(materias),
  });
export const preseleccionarMaterias = (solicitudId) =>
  peticion(`/homologacion/${solicitudId}/preseleccion`, { method: "POST" });

// --- Análisis con memoria (HU-14/HU-22) ---
export const analizarConMemoria = (solicitudId, datos = {}) =>
  peticion(`/homologacion/${solicitudId}/analizar-memoria`, {
    method: "POST",
    body: JSON.stringify(datos),
  });
export const obtenerResultado = (solicitudId) =>
  peticion(`/homologacion/${solicitudId}/resultado`);

// --- Resolución del Consejo (HU-17) ---
export const registrarResolucion = (solicitudId, datos) =>
  peticion(`/homologacion/${solicitudId}/resolucion`, {
    method: "POST",
    body: JSON.stringify(datos),
  });
export const obtenerResolucion = (solicitudId) =>
  peticion(`/homologacion/${solicitudId}/resolucion`);

// --- Informe PDF (HU-18) ---
export async function descargarInforme(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/informe`, {
    headers: { Authorization: `Bearer ${localStorage.getItem("token")}` },
  });
  if (!res.ok) {
    const cuerpo = await res.json().catch(() => ({}));
    throw new Error(cuerpo.mensaje || "No se pudo descargar el informe.");
  }
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `Informe_SOL-${String(solicitudId).padStart(4, "0")}.pdf`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
