import { createContext, useContext, useState } from "react";
import { actualizarPerfil, login } from "../services/authService";

const AuthContext = createContext(null);

const USUARIO_POR_DEFECTO = {
  id: null,
  nombre: "",
  correo: "",
  rol: "",
  tipo: "",
  titulo: "",
  cedula: "",
  telefono: "",
};

function leerUsuarioGuardado() {
  try {
    const guardado = sessionStorage.getItem("usuario");
    return guardado ? { ...USUARIO_POR_DEFECTO, ...JSON.parse(guardado) } : null;
  } catch {
    // Si la sesión está corrupta se descarta y se vuelve a pedir el login.
    sessionStorage.removeItem("usuario");
    return null;
  }
}

function guardarUsuario(usuario) {
  sessionStorage.setItem("usuario", JSON.stringify(usuario));
}

// eslint-disable-next-line react-refresh/only-export-components
export function rutaPorRol(rol) {
  const rutas = {
    COMITE: "/inicio",
    DOCENTE: "/inicio",
    COORDINADOR: "/inicio",
    ADMINISTRADOR: "/inicio",
  };
  return rutas[rol] ?? "/inicio";
}

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => sessionStorage.getItem("token"));
  const [rol, setRol] = useState(() => sessionStorage.getItem("rol"));
  const [usuario, setUsuario] = useState(leerUsuarioGuardado);

  async function iniciarSesion(correo, contrasena) {
    const data = await login(correo, contrasena);

    const usuarioData = {
      ...USUARIO_POR_DEFECTO,
      id: data.id ?? null,
      nombre: data.nombre || "",
      correo: data.correo || correo,
      rol: data.rol || "",
      tipo: data.tipo || "",
      titulo: data.titulo || "",
      cedula: data.cedula || "",
      telefono: data.telefono || "",
    };

    sessionStorage.setItem("token", data.token);
    sessionStorage.setItem("rol", usuarioData.rol);
    guardarUsuario(usuarioData);

    setToken(data.token);
    setRol(usuarioData.rol);
    setUsuario(usuarioData);

    return usuarioData.rol;
  }

  async function guardarPerfil(datos) {
    const perfil = await actualizarPerfil(datos);
    const usuarioData = {
      ...USUARIO_POR_DEFECTO,
      ...usuario,
      id: perfil.id ?? usuario?.id ?? null,
      nombre: perfil.nombre ?? "",
      correo: perfil.correo ?? usuario?.correo ?? "",
      rol: perfil.rol ?? usuario?.rol ?? "",
      tipo: perfil.tipo ?? "",
      titulo: perfil.titulo ?? "",
      cedula: perfil.cedula ?? "",
      telefono: perfil.telefono ?? "",
    };

    guardarUsuario(usuarioData);
    setUsuario(usuarioData);
    if (perfil.rol) setRol(perfil.rol);

    return usuarioData;
  }

  function cerrarSesion() {
    sessionStorage.removeItem("token");
    sessionStorage.removeItem("rol");
    sessionStorage.removeItem("usuario");

    setToken(null);
    setRol(null);
    setUsuario(null);
  }

  return (
    <AuthContext.Provider
      value={{ token, rol, usuario, iniciarSesion, guardarPerfil, cerrarSesion }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  return useContext(AuthContext);
}
