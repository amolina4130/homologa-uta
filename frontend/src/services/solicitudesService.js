// Forzamos la URL directa al puerto 8080 (Gateway)
const GATEWAY_URL = "http://localhost:8080";

// Función auxiliar para obtener el token
const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
};

export async function obtenerMisSolicitudes(filtros = {}) {
  const params = new URLSearchParams();
  if (filtros.estado) params.append("estado", filtros.estado);
  if (filtros.carreraId) params.append("carreraId", filtros.carreraId);
  if (filtros.desde) params.append("desde", filtros.desde);
  if (filtros.hasta) params.append("hasta", filtros.hasta);
  const qs = params.toString();

  const res = await fetch(`${GATEWAY_URL}/solicitudes/mias${qs ? `?${qs}` : ""}`, {
    method: "GET",
    headers: getAuthHeaders(),
    mode: 'cors'
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
  if (!res.ok) throw new Error("Error al registrar la solicitud");
  return await res.json();
}

export async function obtenerSolicitud(id) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Error al cargar la solicitud");
  return await res.json();
}

export async function editarSolicitud(id, solicitudData) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}`, {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify(solicitudData),
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    const err = new Error(json.mensaje || json.error || "No se pudo editar la solicitud");
    err.status = res.status;
    throw err;
  }
  return json;
}

export async function obtenerHistorial(id) {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/${id}/historial`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Error al cargar el historial");
  return await res.json();
}

