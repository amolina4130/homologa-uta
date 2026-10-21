// Forzamos la URL directa al puerto 8080 (Servicio de Solicitudes)
const GATEWAY_URL = "http://localhost:8080";

export async function obtenerMisSolicitudes() {
  // Obtenemos el token que Karol guardará al hacer login
  const token = localStorage.getItem("token"); 

  const res = await fetch(`${GATEWAY_URL}/solicitudes/mias`, {
    method: "GET",
    headers: {
      "Content-Type": "application/json",
      // Enviamos el token directamente al servicio de Estefania
      "Authorization": `Bearer ${token}` 
    },
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