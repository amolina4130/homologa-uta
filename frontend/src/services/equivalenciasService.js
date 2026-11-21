// frontend/src/services/equivalenciasService.js
const API_URL = "http://localhost:8080/equivalencias";

// Datos de ejemplo (Mock) para cuando el backend no esté
let mockEquivalencias = [
  { id: 1, carreraOrigenId: 1, carreraOrigenNombre: "Software", asignaturaOrigenId: 10, asignaturaOrigenNombre: "Programación I", carreraDestinoId: 2, carreraDestinoNombre: "TI", asignaturaDestinoId: 20, asignaturaDestinoNombre: "Programación Básica", activa: true },
  { id: 2, carreraOrigenId: 3, carreraOrigenNombre: "Robótica", asignaturaOrigenId: 15, asignaturaOrigenNombre: "Cálculo I", carreraDestinoId: 1, carreraDestinoNombre: "Software", asignaturaDestinoId: 5, asignaturaDestinoNombre: "Cálculo Diferencial", activa: false }
];

export const obtenerEquivalencias = async () => {
  try {
    const response = await fetch(API_URL);
    if (!response.ok) throw new Error("Error al cargar equivalencias");
    return await response.json();
  } catch (error) {
    console.warn("Backend desconectado. Usando Mock de equivalencias.");
    return mockEquivalencias;
  }
};

export const crearEquivalencia = async (datos) => {
  try {
    const response = await fetch(API_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(datos),
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.mensaje || 'Error al crear');
    return data;
  } catch (error) {
    // Simulamos el error 409 si ya existe en el Mock
    const existe = mockEquivalencias.some(e => 
      e.asignaturaOrigenId === datos.asignaturaOrigenId && 
      e.asignaturaDestinoId === datos.asignaturaDestinoId
    );
    if (existe) throw new Error("La equivalencia ya existe en el sistema (Error 409).");
    
    // Simulamos el éxito
    const nueva = { id: Date.now(), ...datos, carreraOrigenNombre: "Origen (Mock)", carreraDestinoNombre: "Destino (Mock)", asignaturaOrigenNombre: "Asig. Origen (Mock)", asignaturaDestinoNombre: "Asig. Destino (Mock)" };
    mockEquivalencias.push(nueva);
    return nueva;
  }
};

export const cambiarEstadoEquivalencia = async (id, activa) => {
  try {
    const response = await fetch(`${API_URL}/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ activa }),
    });
    if (!response.ok) throw new Error("Error al actualizar");
    return await response.json();
  } catch (error) {
    // Mock: Cambiamos el estado en el arreglo local
    mockEquivalencias = mockEquivalencias.map(e => e.id === id ? { ...e, activa } : e);
    return { success: true };
  }
};