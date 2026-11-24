const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = sessionStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
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

// HU-19: estado de la última carga de memoria del agente.
// Devuelve null cuando todavía no se ha cargado (404).
export async function obtenerEstadoMemoria() {
  const res = await fetch(`${GATEWAY_URL}/memoria/estado`, {
    headers: getAuthHeaders(),
  });

  if (res.status === 404) return null;
  if (!res.ok) await lanzarError(res, "Error al consultar el estado de la memoria");
  return await res.json();
}

// HU-19: recalcula la memoria. Si nada cambió, el backend responde sinCambios: true.
export async function cargarMemoria() {
  const res = await fetch(`${GATEWAY_URL}/memoria/carga`, {
    method: "POST",
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "Error al actualizar la memoria del agente");
  return await res.json();
}
