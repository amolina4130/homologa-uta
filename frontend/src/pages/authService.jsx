// frontend/src/services/authService.js

const API_URL = "http://localhost:8080/auth"; // Ajusta si tu Gateway usa otro puerto

export const registrarUsuario = async (datosRegistro) => {
  try {
    // 1. INTENTO REAL (Cuando el backend esté listo)
    const response = await fetch(`${API_URL}/registro`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(datosRegistro),
    });

    const data = await response.json();

    if (!response.ok) {
      // Si el backend responde 409 o 400, atrapamos el mensaje exacto del contrato
      throw new Error(data.mensaje || 'Error al registrar el usuario');
    }

    return data; // Retorna 201: { id, nombre, correo, rol }

  } catch (error) {
    // 2. SIMULACIÓN DE RESPUESTAS (El Mock para pruebas de Frontend)
    // Si hay un error de red (backend apagado), simulamos el contrato.
    if (error.name === 'TypeError' || error.message.includes('Failed to fetch')) {
      console.warn("Backend desconectado. Usando respuestas de ejemplo (Mock).");

      // Simulamos el error 409 (Correo repetido)
      if (datosRegistro.correo === "repetido@uta.edu.ec") {
        throw new Error("El correo ya está registrado en el sistema."); 
      }

      // Simulamos el error 400 (Contraseña inválida - ej. menos de 6 caracteres)
      if (datosRegistro.contrasena.length < 6) {
        throw new Error("La contraseña debe tener al menos 6 caracteres."); 
      }

      // Simulamos el 201 (Éxito)
      return {
        id: Math.floor(Math.random() * 1000),
        nombre: datosRegistro.nombre,
        correo: datosRegistro.correo,
        rol: "Estudiante"
      };
    }
    
    // Si el error viene del backend real, lo lanzamos a la pantalla
    throw error;
  }
};