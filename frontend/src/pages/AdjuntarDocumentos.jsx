import { useState, useEffect } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerMisSolicitudes, subirDocumento } from "../services/documentosService";
import ListaDocumentos from "../components/ListaDocumentos";

const MENSAJE_RECHAZO = "El archivo no es un PDF o excede el tamaño máximo permitido. No se guardó.";

const TIPOS = [
  { codigo: "MALLA", etiqueta: "Malla curricular" },
  { codigo: "HISTORIAL", etiqueta: "Historial académico" },
  { codigo: "SILABO", etiqueta: "Sílabo" },
];

export default function AdjuntarDocumentos() {
  const { rol } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudId, setSolicitudId] = useState("");
  const [tipoCodigo, setTipoCodigo] = useState(TIPOS[0].codigo);
  const [archivo, setArchivo] = useState(null);
  const [claveInput, setClaveInput] = useState(0);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);
  const [documentosPorSolicitud, setDocumentosPorSolicitud] = useState({});

  useEffect(() => {
    obtenerMisSolicitudes()
      .then(setSolicitudes)
      .catch(() => setError("No se pudieron cargar sus solicitudes."));
  }, []);

  function cambiarArchivo(e) {
    const file = e.target.files[0];
    setError("");
    setArchivo(null);
    if (!file) return;

    if (file.type !== "application/pdf" || file.size > 10 * 1024 * 1024) {
      setError(MENSAJE_RECHAZO);
      e.target.value = "";
      return;
    }
    setArchivo(file);
  }

  async function subir(e) {
    e.preventDefault();
    if (!solicitudId || !archivo) {
      setError("Debe seleccionar una solicitud y un archivo PDF.");
      return;
    }

    setCargando(true);
    setError("");
    try {
      const respuesta = await subirDocumento(solicitudId, tipoCodigo, archivo);
      const etiqueta = TIPOS.find((t) => t.codigo === tipoCodigo)?.etiqueta ?? tipoCodigo;
      const nuevo = {
        id: respuesta.nombreArchivo,
        nombre: archivo.name,
        tipo: etiqueta,
        tamano: `${(archivo.size / (1024 * 1024)).toFixed(1)} MB`,
        estado: "PROCESADO", // Corregido: estaba "PROCEADO"
      };
      setDocumentosPorSolicitud((prev) => ({
        ...prev,
        [solicitudId]: [...(prev[solicitudId] ?? []), nuevo],
      }));
      setArchivo(null);
      setClaveInput((n) => n + 1);
    } catch (err) {
      setError(err.message || MENSAJE_RECHAZO);
    } finally {
      setCargando(false);
    }
  }

  return (
    <div className="container mt-4">
      <div className="card shadow-sm border-0">
        <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
          <h5 className="mb-0">ADJUNTAR DOCUMENTOS</h5>
          <span>Rol: {rol === "ESTUDIANTE" ? "Estudiante" : rol}</span>
        </div>
        <div className="card-body">
          <h2 className="text-center mb-4">SISTEMA DE HOMOLOGACIÓN - UTA</h2>

          <label className="form-label fw-bold">Solicitud</label>
          <select
            className="form-select mb-3"
            value={solicitudId}
            onChange={(e) => { setSolicitudId(e.target.value); setError(""); }}
          >
            <option value="">Seleccione una solicitud</option>
            {solicitudes.map((s) => (
              <option key={s.id} value={s.id}>
                SOL-{s.numero} · {s.origen} → {s.destino}
              </option>
            ))}
          </select>

          <div className="border rounded p-3 mb-3 bg-light">
            <h4 className="mb-3">Nuevo documento</h4>

            <label className="form-label fw-bold">Tipo de documento</label>
            <select className="form-select mb-3" value={tipoCodigo} onChange={(e) => setTipoCodigo(e.target.value)}>
              {TIPOS.map((t) => (
                <option key={t.codigo} value={t.codigo}>{t.etiqueta}</option>
              ))}
            </select>

            <label className="form-label fw-bold">Archivo PDF</label>
            <input 
              key={claveInput} 
              type="file" 
              className="form-control mb-3" 
              accept=".pdf" 
              onChange={cambiarArchivo} 
            />
            
            <p className="text-muted small">
              Solo se aceptan archivos PDF. Tamaño máximo configurable (por ejemplo, 10 MB).
            </p>

            {error && <div className="alert alert-danger">{error}</div>}

            <button 
              onClick={subir} 
              disabled={cargando || !archivo} 
              className="btn btn-primary"
            >
              {cargando ? "Subiendo..." : "Adjuntar documento"}
            </button>
          </div>

          <h4>Documentos de la solicitud</h4>
          <ListaDocumentos documentos={documentosPorSolicitud[solicitudId] ?? []} />

          <p className="text-muted small mt-3">
            El texto de cada PDF se extrae con PdfPig y el documento pasa a PROCESADO.<br />
            Solo ves los documentos de tus propias solicitudes.
          </p>
        </div>
      </div>
    </div>
  );
}