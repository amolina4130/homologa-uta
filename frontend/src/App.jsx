import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import EstadoSistema from "./pages/EstadoSistema";
import Login from "./pages/Login";
import { AuthProvider, useAuth } from "./context/AuthContext";

// Pantalla temporal; el menú por rol llega en la TA-02.6
function Inicio() {
  const { rol, cerrarSesion } = useAuth();
  return (
    <div style={{ padding: 40, fontFamily: "sans-serif" }}>
      <p>Sesión iniciada. Rol: {rol ?? "desconocido"}</p>
      <button onClick={cerrarSesion}>Cerrar sesión</button>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/estado" element={<EstadoSistema />} />
          <Route path="/login" element={<Login />} />
          <Route path="/inicio" element={<Inicio />} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}