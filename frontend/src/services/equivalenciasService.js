const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
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

export async function listarEquivalencias() {
  const res = await fetch(`${GATEWAY_URL}/equivalencias`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "Error al cargar las equivalencias");
  return await res.json();
}

export async function crearEquivalencia(datos) {
  const res = await fetch(`${GATEWAY_URL}/equivalencias`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(datos),
  });
  if (!res.ok) await lanzarError(res, "Error al crear la equivalencia");
  return await res.json();
}

export async function actualizarEquivalencia(id, datos) {
  const res = await fetch(`${GATEWAY_URL}/equivalencias/${id}`, {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify(datos),
  });
  if (!res.ok) await lanzarError(res, "Error al actualizar la equivalencia");
  return await res.json();
}
