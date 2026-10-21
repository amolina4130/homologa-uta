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

  async function iniciarSesion(correo, contrasena) {
    const data = await login(correo, contrasena);
    
    localStorage.setItem("token", data.token);
    localStorage.setItem("rol", data.rol);
    
    setToken(data.token);
    setRol(data.rol);
    
    return data.rol; 
  }

  function cerrarSesion() {
    localStorage.removeItem("token");
    localStorage.removeItem("rol");
    setToken(null);
    setRol(null);
  }

  return (
    <AuthContext.Provider value={{ token, rol, iniciarSesion, cerrarSesion }}>
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  return useContext(AuthContext);
}