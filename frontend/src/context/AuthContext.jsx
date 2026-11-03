import { createContext, useContext, useState } from "react";
import { login } from "../services/authService";

const AuthContext = createContext(null);

// eslint-disable-next-line react-refresh/only-export-components
export function rutaPorRol(rol) {
  const rutas = {
    ESTUDIANTE: "/inicio",
    COORDINADOR: "/inicio",
    SECRETARIA: "/inicio",
    CONSEJO: "/inicio",
    ADMINISTRADOR: "/inicio",
  };
  return rutas[rol] ?? "/inicio";
}

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => localStorage.getItem("token"));
  const [rol, setRol] = useState(() => localStorage.getItem("rol")); 
  
  // Estado para el usuario completo
  const [usuario, setUsuario] = useState(() => {
    const user = localStorage.getItem("usuario");
    return user ? JSON.parse(user) : null;
  });

  async function iniciarSesion(correo, contrasena) {
    const data = await login(correo, contrasena);
    
    localStorage.setItem("token", data.token);
    localStorage.setItem("rol", data.rol);
    
    // Guardamos el objeto completo del usuario
    const usuarioData = {
      nombre: data.nombre,
      correo: data.correo || correo,
      rol: data.rol
    };
    localStorage.setItem("usuario", JSON.stringify(usuarioData));
    
    setToken(data.token);
    setRol(data.rol);
    setUsuario(usuarioData); 
    
    return data.rol; 
  }

  function cerrarSesion() {
    localStorage.removeItem("token");
    localStorage.removeItem("rol");
    localStorage.removeItem("usuario"); // Limpiamos el usuario
    
    setToken(null);
    setRol(null);
    setUsuario(null); 
  }

  return (
    <AuthContext.Provider value={{ token, rol, usuario, iniciarSesion, cerrarSesion }}>
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  return useContext(AuthContext);
}