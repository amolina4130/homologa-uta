const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

// Función auxiliar para obtener el token
const getAuthHeaders = () => {
  const token = sessionStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
};

const leerMensaje = async (res, mensajePorDefecto) => {
  let mensaje = mensajePorDefecto;
  try {
    const cuerpo = await res.json();
    if (cuerpo && (cuerpo.error || cuerpo.mensaje)) {
      mensaje = cuerpo.error || cuerpo.mensaje;
    }
  } catch {
    // La respuesta no trae JSON; se conserva el mensaje por defecto.
  }

  const err = new Error(mensaje);
  err.status = res.status;
  return err;
};

export async function obtenerMisSolicitudes(filtros = {}) {
  const params = new URLSearchParams();
  if (filtros.estado) params.set("estado", filtros.estado);
  if (filtros.carreraId) params.set("carreraId", filtros.carreraId);
  if (filtros.desde) params.set("desde", filtros.desde);
  if (filtros.hasta) params.set("hasta", filtros.hasta);

  const query = params.toString();
  const url = `${GATEWAY_URL}/solicitudes/mias${query ? `?${query}` : ""}`;

  const res = await fetch(url, {
    method: "GET",
    headers: getAuthHeaders(),
    mode: "cors",
  });

  if (res.status === 401) {
    const err = new Error("Sesión expirada o no autorizada");
    err.tipo = "no_autorizado";
    throw err;
  }

  if (!res.ok) {
    const err = new Error("Error al cargar las solicitudes");
    err.tipo = "servidor";
    throw err;
  }

  return await res.json();
}

export async function obtenerCarreras() {
  const res = await fetch(`${GATEWAY_URL}/carreras`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar carreras");
  return await res.json();
}

export async function obtenerAsignaturas(carreraId) {
  const res = await fetch(`${GATEWAY_URL}/carreras/${carreraId}/asignaturas`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Error al cargar asignaturas");
  return await res.json();
}

export async function crearSolicitud(solicitudData) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(solicitudData),
  });
  if (!res.ok) {
    const errorData = await res.json().catch(() => ({}));
    const err = new Error(errorData.error || "Error al registrar la solicitud");
    err.status = res.status;
    throw err;
  }
  return await res.json();
}

export async function obtenerSolicitud(id) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}`, {
    method: "GET",
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const err = new Error("No se pudo cargar la solicitud");
    err.status = res.status;
    throw err;
  }
  return await res.json();
}

// HU-16: edita una solicitud mientras sigue REGISTRADA.
export async function actualizarSolicitud(id, solicitudData) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}`, {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify(solicitudData),
  });

  if (res.status === 409) {
    throw await leerMensaje(
      res,
      "La solicitud ya pasó a revisión y no se puede editar."
    );
  }
  if (res.status === 403) {
    throw await leerMensaje(res, "No puede editar la solicitud de otro estudiante.");
  }
  if (!res.ok) {
    throw await leerMensaje(res, "Error al actualizar la solicitud");
  }

  return await res.json();
}

// Corrige nombre y cédula del titular del récord sin cambiar el estado de la solicitud.
export async function actualizarTitularSolicitud(id, nombre, cedula) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}/titular`, {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({ nombre, cedula }),
  });

  if (!res.ok) {
    throw await leerMensaje(res, "No se pudieron guardar los datos del titular");
  }
  return await res.json();
}

// HU-16: historial de estados de la solicitud (usuario y fecha de cada cambio).
export async function obtenerHistorial(id) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}/historial`, {
    method: "GET",
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    throw await leerMensaje(res, "Error al cargar el historial de la solicitud");
  }
  return await res.json();
}
