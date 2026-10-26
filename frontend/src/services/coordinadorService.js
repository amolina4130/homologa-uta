const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`
  };
};

export async function obtenerSolicitudesPendientes() {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/pendientes`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar solicitudes pendientes");
  return await res.json();
}

export async function obtenerDetalleSolicitud(id) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}/detalle`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar el detalle");
  return await res.json();
}

export async function emitirInforme(id, decisiones) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}/informe`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({ decisiones }),
  });
  if (!res.ok) throw new Error("Error al emitir el informe");
  return await res.json();
}