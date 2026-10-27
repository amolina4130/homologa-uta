import AdjuntarDocumentos from "./pages/AdjuntarDocumentos";
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

          {/* Rutas protegidas */}
          <Route element={<RutaProtegida />}>
            <Route path="/inicio" element={<><Menu /><Inicio /></>} />
            <Route path="/estado" element={<><Menu /><EstadoSistema /></>} />
            
            <Route path="/mis-solicitudes" element={<><Menu /><MisSolicitudes /></>} />
            <Route path="/nueva-solicitud" element={<><Menu /><NuevaSolicitud /></>} />
            <Route path="/carreras" element={<><Menu /><Carreras /></>} />
            
            {/* Rutas de tus compañeras y la tuya */}
            <Route path="/panel-coordinador" element={<><Menu /><PanelCoordinador /></>} />
            <Route path="/reglas" element={<><Menu /><PanelReglas /></>} />
            <Route path="/adjuntar-documentos" element={<><Menu /><AdjuntarDocumentos /></>} />
          </Route>

          {/* Redirección por defecto */}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}