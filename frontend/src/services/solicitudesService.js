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

export async function obtenerMisSolicitudes() {
  const res = await fetch(`${GATEWAY_URL}/solicitudes/mias`, {
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

