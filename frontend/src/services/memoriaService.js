import { GATEWAY_URL } from "./config";

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

// Asignaturas de una carrera con su plan analítico (lo mismo que lee la IA).
export async function obtenerAsignaturasMemoria(carreraId) {
  const res = await fetch(`${GATEWAY_URL}/memoria/carreras/${carreraId}/asignaturas`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) await lanzarError(res, "No se pudo cargar la malla de la carrera");
  return await res.json();
}

// Solo Administrador: guarda (o reemplaza) el plan analítico de una asignatura.
export async function guardarPlanAnalitico(asignaturaId, contenido, resultadosAprendizaje) {
  const res = await fetch(`${GATEWAY_URL}/memoria/asignaturas/${asignaturaId}/plan`, {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify({ contenido, resultadosAprendizaje }),
  });
  if (!res.ok) await lanzarError(res, "No se pudo guardar el plan analítico");
  return await res.json();
}
