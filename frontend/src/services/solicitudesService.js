const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

export async function obtenerMisSolicitudes() {
  // Obtenemos el token que Karol guardará al hacer login
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