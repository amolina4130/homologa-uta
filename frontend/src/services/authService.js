const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

export async function login(correo, contrasena) {
  const res = await fetch(`${GATEWAY_URL}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ correo, contrasena }),
  });

  if (res.status === 401) {
    const err = new Error("Credenciales incorrectas");
    err.tipo = "credenciales";
    throw err;
  }
  if (!res.ok) {
    const err = new Error("Error del servidor");
    err.tipo = "servidor";
    throw err;
  }

  return await res.json(); 
}

// HU-12: crea una cuenta de estudiante. El rol lo asigna el backend.
export async function registrar(nombre, correo, contrasena) {
  const res = await fetch(`${GATEWAY_URL}/auth/registro`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ nombre, correo, contrasena }),
  });

  let cuerpo = {};
  try {
    cuerpo = await res.json();
  } catch {
    // El backend no devolvió JSON; se usa el mensaje por defecto.
  }

  if (!res.ok) {
    const err = new Error(cuerpo.mensaje || "No se pudo crear la cuenta.");
    err.status = res.status;
    err.tipo = res.status === 409 ? "duplicado" : "validacion";
    throw err;
  }

  return cuerpo;
}