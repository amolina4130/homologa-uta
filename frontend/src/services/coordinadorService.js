const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`
  };
};

// Función auxiliar para extraer los datos de la respuesta del backend
const extraerDatos = (json) => {
  // Si el backend devuelve { data: [...] }, retornamos data; si no, el json directo
  return json.data !== undefined ? json.data : json;
};

export async function obtenerSolicitudesPendientes() {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/pendientes`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar solicitudes pendientes");
  const json = await res.json();
  return extraerDatos(json); // <-- Siempre devuelve un arreglo
}

export async function obtenerDetalleSolicitud(id) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}/detalle`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar el detalle");
  const json = await res.json();
  return extraerDatos(json); // <-- Siempre devuelve el objeto
}

export async function emitirInforme(id, decisiones) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}/informe`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({ decisiones }),
  });
  if (!res.ok) throw new Error("Error al emitir el informe");
  const json = await res.json();
  return extraerDatos(json);
}