import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import EstadoSistema from "./pages/EstadoSistema";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Opción A: Redirigir la raíz a /estado */}
        <Route path="/" element={<Navigate to="/estado" replace />} />
        <Route path="/estado" element={<EstadoSistema />} />
      </Routes>
    </BrowserRouter>
  );
}