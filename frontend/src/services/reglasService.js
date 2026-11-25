import { GATEWAY_URL } from "./config";

const getAuthHeaders = () => {
  const token = sessionStorage.getItem("token");
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