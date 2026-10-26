const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`
  };
};

export async function obtenerReglas() {
  const res = await fetch(`${GATEWAY_URL}/reglas`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar las reglas");
  return await res.json();
}

export async function actualizarRegla(id, datos) {
  const res = await fetch(`${GATEWAY_URL}/reglas/${id}`, {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify(datos),
  });
  if (!res.ok) throw new Error("Error al guardar los cambios de la regla");
  return await res.json();
}