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

export async function obtenerSolicitudesPendientes() {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/pendientes`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar solicitudes pendientes");
  const json = await res.json();
  return extraerDatos(json); 
}

export async function obtenerDetalleSolicitud(id) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/detalle`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar el detalle");
  const json = await res.json();
  return extraerDatos(json); 
}

export async function emitirInforme(id, informeCompleto) {
  const res = await fetch(`${GATEWAY_URL}/homologacion/coordinador/${id}/informe`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(informeCompleto),
  });
  if (!res.ok) throw new Error("Error al emitir el informe");
  const json = await res.json();
  return extraerDatos(json);
}