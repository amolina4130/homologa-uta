const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`
  };
};

// Obtener el resultado del análisis de una solicitud
export async function obtenerResultadoAnalisis(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/resultado`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar el resultado del análisis");
  const json = await res.json();
  return json.data !== undefined ? json.data : json;
}

// Validar las reglas objetivas (HU-07)
export async function validarReglasObjetivas(solicitudId) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/${solicitudId}/validar`, {
    method: "POST",
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al validar las reglas objetivas");
  const json = await res.json();
  return json.data !== undefined ? json.data : json;
}