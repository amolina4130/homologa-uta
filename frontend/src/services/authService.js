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

// Crea una cuenta de Comité o Docente; el rol se envía explícitamente.
export async function registrar(nombre, correo, contrasena, rol = "COMITE") {
  const res = await fetch(`${GATEWAY_URL}/auth/registro`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ nombre, correo, contrasena, rol }),
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

// Guarda el perfil editable del usuario autenticado (nombre, tipo, título, cédula y teléfono).
export async function actualizarPerfil(datos) {
  const token = sessionStorage.getItem("token");
  const res = await fetch(`${GATEWAY_URL}/auth/perfil`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(datos),
  });

  let cuerpo = {};
  try {
    cuerpo = await res.json();
  } catch {
    // El backend no devolvió JSON; se usa el mensaje por defecto.
  }

  if (!res.ok) {
    const err = new Error(cuerpo.mensaje || "No se pudo guardar el perfil.");
    err.status = res.status;
    throw err;
  }

  return cuerpo;
}