import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import EstadoSistema from "./pages/EstadoSistema";
import Login from "./pages/Login";
import Registro from "./pages/Registro";
import Menu from "./components/Menu";
import RutaProtegida from "./components/RutaProtegida";
import { AuthProvider } from "./context/AuthContext";

// Importamos las pantallas
import Inicio from "./pages/Inicio";
import MisSolicitudes from "./pages/MisSolicitudes";
import NuevaSolicitud from "./pages/NuevaSolicitud";
import EditarSolicitud from "./pages/EditarSolicitud";
import Carreras from "./pages/Carreras";
import AdjuntarDocumentos from "./pages/AdjuntarDocumentos";
import PanelReglas from "./pages/PanelReglas";
import PanelCoordinador from "./pages/PanelCoordinador";
import ResultadoAnalisis from "./pages/ResultadoAnalisis";
import Equivalencias from "./pages/Equivalencias";
import EstadoMemoria from "./pages/EstadoMemoria";
import Resoluciones from "./pages/Resoluciones";

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          {/* Rutas públicas */}
          <Route path="/login" element={<Login />} />
          <Route path="/registro" element={<Registro />} />

          {/* Rutas protegidas generales */}
          <Route element={<RutaProtegida />}>
            <Route path="/inicio" element={<><Menu /><Inicio /></>} />
            <Route path="/estado" element={<><Menu /><EstadoSistema /></>} />
            <Route path="/mis-solicitudes" element={<><Menu /><MisSolicitudes /></>} />
            <Route path="/nueva-solicitud" element={<><Menu /><NuevaSolicitud /></>} />
            <Route path="/solicitudes/:id/editar" element={<><Menu /><EditarSolicitud /></>} />
            <Route path="/carreras" element={<><Menu /><Carreras /></>} />
            <Route path="/resultado/:id" element={<><Menu /><ResultadoAnalisis /></>} />
          </Route>

          {/* Rutas restringidas por rol */}
          <Route element={<RutaProtegida rolPermitido="COORDINADOR" />}>
            <Route path="/panel-coordinador" element={<><Menu /><PanelCoordinador /></>} />
          </Route>

          <Route element={<RutaProtegida rolPermitido="CONSEJO" />}>
            <Route path="/resoluciones" element={<><Menu /><Resoluciones /></>} />
          </Route>

          <Route element={<RutaProtegida rolPermitido="ADMINISTRADOR" />}>
            <Route path="/reglas" element={<><Menu /><PanelReglas /></>} />
            <Route path="/equivalencias" element={<><Menu /><Equivalencias /></>} />
            <Route path="/memoria" element={<><Menu /><EstadoMemoria /></>} />
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
