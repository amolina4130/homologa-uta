import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function RutaProtegida() {
  const { token } = useAuth();

  // Si no hay token, lo mandamos al login
  if (!token) {
    return <Navigate to="/login" replace />;
  }

  // Si hay token, lo dejamos pasar a la ruta que quería ver
  return <Outlet />;
}