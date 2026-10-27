import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { jwtDecode } from "jwt-decode";
import { useState } from "react";

export default function RutaProtegida({ rolPermitido }) { 
  const { token, rol, cerrarSesion } = useAuth(); 
  
  const [tiempoActual] = useState(() => Date.now() / 1000);
  
  let esValido = false;

  if (token) {
    try {
      const decoded = jwtDecode(token);
      if (decoded.exp && decoded.exp > tiempoActual) {
        esValido = true;
      }
    } catch {
      esValido = false;
    }
  }

  if (!esValido) {
    if (token) cerrarSesion(); 
    return <Navigate to="/login" replace />;
  }

  if (rolPermitido && rol !== rolPermitido) {
    return <Navigate to="/inicio" replace />;
  }

  return <Outlet />;
}