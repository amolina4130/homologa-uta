const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

const getAuthHeaders = () => {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
};

export async function obtenerEstadoMemoria() {
  try {
    const res = await fetch(`${GATEWAY_URL}/memoria/estado`, {
      headers: getAuthHeaders(),
    });

    if (res.status === 404) throw new Error("Aún no hay memoria cargada en el sistema.");
    if (!res.ok) throw new Error("Error al consultar el estado de la memoria.");

    const json = await res.json();
    return json.data !== undefined ? json.data : json;
  } catch (error) {
    console.warn("Backend no disponible. Usando Mock de memoria.");
    return {
      huella: "a1b2c3d4e5f6...",
      fechaCarga: new Date().toISOString(),
      totalCarreras: 5,
      totalAsignaturas: 142,
    };
  }
}

export async function actualizarMemoria() {
  try {
    const res = await fetch(`${GATEWAY_URL}/memoria/carga`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({}),
    });

    if (res.status === 403) throw new Error("No tienes permiso para actualizar la memoria (403).");
    if (!res.ok) throw new Error("Error al actualizar la memoria.");

    const json = await res.json();
    return json.data !== undefined ? json.data : json;
  } catch (error) {
    console.warn("Backend no disponible. Simulando actualización.");
    
    const simularSinCambios = false; 
    
    return {
      cargadas: simularSinCambios ? 0 : 3,
      sinCambios: simularSinCambios,
      huella: "f9e8d7c6b5a4...", 
    };
  }
}