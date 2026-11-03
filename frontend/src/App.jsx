import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import EstadoSistema from "./pages/EstadoSistema";
import Login from "./pages/Login";
import Menu from "./components/Menu";
import RutaProtegida from "./components/RutaProtegida";
import { AuthProvider } from "./context/AuthContext"; 

// Importamos las pantallas
import Inicio from "./pages/Inicio"; 
import MisSolicitudes from "./pages/MisSolicitudes"; 
import NuevaSolicitud from "./pages/NuevaSolicitud";
import Carreras from "./pages/Carreras";
import AdjuntarDocumentos from "./pages/AdjuntarDocumentos";
import PanelReglas from "./pages/PanelReglas"; 
import PanelCoordinador from "./pages/PanelCoordinador"; 
import ResultadoAnalisis from "./pages/ResultadoAnalisis";

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          {/* Ruta pública */}
          <Route path="/login" element={<Login />} />

          {/* Rutas protegidas generales */}
          <Route element={<RutaProtegida />}>
            {/* Ahora usa el componente Inicio importado */}
            <Route path="/inicio" element={<><Menu /><Inicio /></>} />
            <Route path="/estado" element={<><Menu /><EstadoSistema /></>} />
            <Route path="/mis-solicitudes" element={<><Menu /><MisSolicitudes /></>} />
            <Route path="/nueva-solicitud" element={<><Menu /><NuevaSolicitud /></>} />
            <Route path="/carreras" element={<><Menu /><Carreras /></>} />
            <Route path="/resultado/:id" element={<><Menu /><ResultadoAnalisis /></>} />
          </Route>

          {/* Rutas restringidas por rol */}
          <Route element={<RutaProtegida rolPermitido="COORDINADOR" />}>
            <Route path="/panel-coordinador" element={<><Menu /><PanelCoordinador /></>} />
          </Route>

          <Route element={<RutaProtegida rolPermitido="ADMINISTRADOR" />}>
            <Route path="/reglas" element={<><Menu /><PanelReglas /></>} />
          </Route>

          {/* Ruta para adjuntar documentos */}
          <Route element={<RutaProtegida />}>
            <Route path="/adjuntar-documentos" element={<><Menu /><AdjuntarDocumentos /></>} />
          </Route>

          {/* Redirección por defecto */}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}