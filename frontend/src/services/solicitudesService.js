const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

// --- Funciones para la pantalla de Mis Solicitudes (Katherine) ---

export async function obtenerMisSolicitudes() {
  // Obtenemos el token que se guardó al hacer login
  const token = localStorage.getItem("token");

  const res = await fetch(`${GATEWAY_URL}/solicitudes/mias`, {
    method: "GET",
    headers: {
      "Content-Type": "application/json",
      // Enviamos el token al Gateway de Anahi
      "Authorization": `Bearer ${token}`
    },
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

// --- Funciones para la pantalla de Nueva Solicitud (Karol) ---

export async function obtenerCarreras() {
  const token = localStorage.getItem("token");
  const res = await fetch(`${GATEWAY_URL}/carreras`, {
    headers: { "Authorization": `Bearer ${token}` },
  });
  if (!res.ok) throw new Error("Error al cargar carreras");
  return await res.json();
}

export async function obtenerAsignaturas(carreraId) {
  const token = localStorage.getItem("token");
  const res = await fetch(`${GATEWAY_URL}/carreras/${carreraId}/asignaturas`, {
    headers: { "Authorization": `Bearer ${token}` },
  });
  if (!res.ok) throw new Error("Error al cargar asignaturas");
  return await res.json();
}

export async function crearSolicitud(solicitudData) {
  const token = localStorage.getItem("token");
  const res = await fetch(`${GATEWAY_URL}/solicitudes`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${token}`,
    },
    body: JSON.stringify(solicitudData),
  });
  if (!res.ok) throw new Error("Error al registrar la solicitud");
  return await res.json();
}