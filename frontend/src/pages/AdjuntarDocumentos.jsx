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
        estado: "PROCEADO",
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
    <div style={{ maxWidth: 900, margin: "40px auto", fontFamily: "sans-serif", border: "2px solid #000" }}>
      <div style={{ display: "flex", justifyContent: "space-between", borderBottom: "2px solid #000", padding: "8px 16px", fontWeight: "bold" }}>
        <span>ADJUNTAR DOCUMENTOS</span>
        <span>Rol: {rol === "ESTUDIANTE" ? "Estudiante" : rol}</span>
      </div>

      <div style={{ padding: 24 }}>
        <h1 style={{ textAlign: "center" }}>SISTEMA DE HOMOLOGACIÓN - UTA</h1>

        <label htmlFor="solicitud"><strong>Solicitud</strong></label>
        <select
          id="solicitud"
          className="form-control mb-3"
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

        <div className="p-3 mb-3" style={{ border: "2px solid #000" }}>
          <h3 style={{ marginTop: 0 }}>Nuevo documento</h3>

          <label htmlFor="tipo"><strong>Tipo de documento</strong></label>
          <select id="tipo" className="form-control mb-3" value={tipoCodigo} onChange={(e) => setTipoCodigo(e.target.value)}>
            {TIPOS.map((t) => (
              <option key={t.codigo} value={t.codigo}>{t.etiqueta}</option>
            ))}
          </select>

          <label htmlFor="archivo"><strong>Archivo PDF</strong></label>
          <div>
            <input key={claveInput} id="archivo" type="file" className="form-control mb-3" accept=".pdf" onChange={cambiarArchivo} />
          </div>
          <p style={{ fontSize: 14 }}>
            Solo se aceptan archivos PDF. Tamaño máximo configurable (por ejemplo, 10 MB).
          </p>

          {error && (
            <p style={{ border: "1px dashed #000", padding: 12 }}>✖ {error}</p>
          )}

          <button onClick={subir} disabled={cargando || !archivo} className="btn btn-primary mt-2">
            {cargando ? "Subiendo..." : "Adjuntar documento"}
          </button>
        </div>

        <h3>Documentos de la solicitud</h3>
        <ListaDocumentos documentos={documentosPorSolicitud[solicitudId] ?? []} />

        <p style={{ marginTop: 24 }}>
          El texto de cada PDF se extrae con PdfPig y el documento pasa a PROCESADO.
          <br />
          Solo ves los documentos de tus propias solicitudes.
        </p>
      </div>
    </div>
  );
}