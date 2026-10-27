import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import EstadoSistema from "./pages/EstadoSistema";
import Login from "./pages/Login";
import Menu from "./components/Menu";
import RutaProtegida from "./components/RutaProtegida";
import { AuthProvider, useAuth } from "./context/AuthContext";
import PanelCoordinador from "./pages/PanelCoordinador";
import PanelReglas from "./pages/PanelReglas";

// Importamos las pantallas adicionales
import MisSolicitudes from "./pages/MisSolicitudes"; 
import NuevaSolicitud from "./pages/NuevaSolicitud";
import Carreras from "./pages/Carreras";

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

          {/* Rutas protegidas generales (cualquier rol logueado) */}
          <Route element={<RutaProtegida />}>
            <Route path="/inicio" element={<><Menu /><Inicio /></>} />
            <Route path="/estado" element={<><Menu /><EstadoSistema /></>} />
            <Route path="/mis-solicitudes" element={<><Menu /><MisSolicitudes /></>} />
            <Route path="/nueva-solicitud" element={<><Menu /><NuevaSolicitud /></>} />
            <Route path="/carreras" element={<><Menu /><Carreras /></>} />
          </Route>

          {/* Rutas restringidas por rol */}
          <Route element={<RutaProtegida rolPermitido="COORDINADOR" />}>
            <Route path="/panel-coordinador" element={<><Menu /><PanelCoordinador /></>} />
          </Route>

          <Route element={<RutaProtegida rolPermitido="ADMINISTRADOR" />}>
            <Route path="/reglas" element={<><Menu /><PanelReglas /></>} />
          </Route>

          {/* Redirección por defecto */}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}