import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import EstadoSistema from "./pages/EstadoSistema";
import Login from "./pages/Login";
import Menu from "./components/Menu";
import RutaProtegida from "./components/RutaProtegida";
import { AuthProvider, useAuth } from "./context/AuthContext";

function Inicio() {
  const { rol } = useAuth();
  return (
    <div style={{ padding: 40, fontFamily: "sans-serif" }}>
      <h2>¡Bienvenido!</h2>
      <p>Sesión iniciada con el rol: <strong>{rol ?? "desconocido"}</strong></p>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          {/* Rutas públicas */}
          <Route path="/login" element={<Login />} />

          {/* Rutas protegidas */}
          <Route element={<RutaProtegida />}>
            <Route path="/inicio" element={<><Menu /><Inicio /></>} />
            <Route path="/estado" element={<><Menu /><EstadoSistema /></>} />
          </Route>

          {/* Redirección por defecto */}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}