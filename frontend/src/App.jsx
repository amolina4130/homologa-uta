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
import RutaCarpeta from "./pages/RutaCarpeta";
import SolicitudesDocente from "./pages/SolicitudesDocente";
import EvaluacionDocente from "./pages/EvaluacionDocente";
import PanelReglas from "./pages/PanelReglas";
import PanelCoordinador from "./pages/PanelCoordinador";
import DetalleSolicitudPage from "./pages/DetalleSolicitudPage";
import DelegarDocentes from "./pages/DelegarDocentes";
import InformesSugeridos from "./pages/InformesSugeridos";
import InformeFinal from "./pages/InformeFinal";
import ResultadoAnalisis from "./pages/ResultadoAnalisis";
import Equivalencias from "./pages/Equivalencias";
import EstadoMemoria from "./pages/EstadoMemoria";

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
            <Route path="/carreras" element={<><Menu /><Carreras /></>} />
          </Route>

          {/* Documentos: solo el Comité y el Coordinador registran URL, carpeta y récord.
              El Docente recibe esa información ya guardada en la solicitud. */}
          <Route element={<RutaProtegida rolPermitido={["COMITE", "COORDINADOR"]} />}>
            <Route path="/adjuntar-documentos" element={<><Menu /><AdjuntarDocumentos /></>} />
            <Route path="/ruta-carpeta" element={<><Menu /><RutaCarpeta /></>} />
          </Route>

          {/* El Comité y el Coordinador registran y consultan resultados */}
          <Route element={<RutaProtegida rolPermitido={["COMITE", "COORDINADOR"]} />}>
            <Route path="/mis-solicitudes" element={<><Menu /><MisSolicitudes /></>} />
            <Route path="/nueva-solicitud" element={<><Menu /><NuevaSolicitud /></>} />
            <Route path="/resultado/:id" element={<><Menu /><ResultadoAnalisis /></>} />
            <Route
              path="/editar-solicitud/:id"
              element={<><Menu /><EditarSolicitud /></>}
            />
          </Route>

          {/* El Docente evalúa las solicitudes delegadas */}
          <Route element={<RutaProtegida rolPermitido="DOCENTE" />}>
            <Route path="/solicitudes-docente" element={<><Menu /><SolicitudesDocente /></>} />
            <Route path="/evaluar/:id" element={<><Menu /><EvaluacionDocente /></>} />
          </Route>

          {/* El Coordinador delega, revisa el informe sugerido y emite el informe final */}
          <Route element={<RutaProtegida rolPermitido="COORDINADOR" />}>
            <Route path="/delegar-docentes" element={<><Menu /><DelegarDocentes /></>} />
            <Route path="/informes-sugeridos" element={<><Menu /><InformesSugeridos /></>} />
            <Route path="/informe-final" element={<><Menu /><InformeFinal /></>} />
            {/* Detalle en una página propia, sin meter el contenido debajo de la lista. */}
            <Route path="/detalle-solicitud/:id" element={<DetalleSolicitudPage />} />
            {/* Ruta histórica: se mantiene como acceso directo al panel completo. */}
            <Route path="/panel-coordinador" element={<><Menu /><PanelCoordinador /></>} />
          </Route>

          {/* El Administrador gestiona la configuración del sistema */}
          <Route element={<RutaProtegida rolPermitido="ADMINISTRADOR" />}>
            <Route path="/reglas" element={<><Menu /><PanelReglas /></>} />
            <Route path="/equivalencias" element={<><Menu /><Equivalencias /></>} />
            <Route path="/memoria" element={<><Menu /><EstadoMemoria /></>} />
          </Route>

          {/* Redirección por defecto */}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
