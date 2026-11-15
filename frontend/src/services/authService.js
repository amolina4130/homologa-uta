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

export async function registro(nombre, correo, contrasena) {
  const res = await fetch(`${GATEWAY_URL}/auth/registro`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ nombre, correo, contrasena }),
  });

  const json = await res.json().catch(() => ({}));
  if (res.status === 409) {
    const err = new Error(json.mensaje || "El correo ya está registrado.");
    err.tipo = "duplicado";
    throw err;
  }
  if (res.status === 400) {
    const err = new Error(json.mensaje || "La contraseña no cumple los requisitos.");
    err.tipo = "invalido";
    throw err;
  }
  if (!res.ok) {
    const err = new Error("Error del servidor");
    err.tipo = "servidor";
    throw err;
  }

  return json;
}