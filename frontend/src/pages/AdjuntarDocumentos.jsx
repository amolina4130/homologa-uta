import { useCallback, useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import {
  obtenerDocumentosSolicitud,
  obtenerMisSolicitudes,
  subirDocumento,
} from "../services/documentosService";
import {
  actualizarMateriasAprobadas,
  extraerRecord,
  obtenerMateriasAprobadas,
} from "../services/homologacionService";
import { obtenerSolicitud } from "../services/solicitudesService";
import { obtenerSolicitudesDocente } from "../services/docenteService";
import ListaDocumentos from "../components/ListaDocumentos";
import TablaMateriasAprobadas from "../components/TablaMateriasAprobadas";

const MENSAJE_RECHAZO = "El archivo no es un PDF o excede el tamaño máximo permitido. No se guardó.";

// Solo se adjunta el récord académico. La malla, los sílabos y los planes ya están
// cargados en el sistema para el agente interno; para el externo se referencia la URL.
const TIPOS = [{ codigo: "HISTORIAL", etiqueta: "Récord académico" }];

export default function AdjuntarDocumentos() {
  const { rol } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const solicitudIdParam = searchParams.get("solicitudId") || "";
  const volverResultado = searchParams.get("volver") === "resultado";

  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudId, setSolicitudId] = useState(solicitudIdParam);
  const [tipoCodigo, setTipoCodigo] = useState(TIPOS[0].codigo);
  const [archivo, setArchivo] = useState(null);
  const [claveInput, setClaveInput] = useState(0);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");
  const [cargando, setCargando] = useState(false);
  const [documentos, setDocumentos] = useState([]);
  const [estadoSolicitud, setEstadoSolicitud] = useState("");

  const [materias, setMaterias] = useState([]);
  const [requiereRevision, setRequiereRevision] = useState(false);
  const [motivoRevision, setMotivoRevision] = useState("");
  const [extrayendo, setExtrayendo] = useState(false);
  const [guardandoMaterias, setGuardandoMaterias] = useState(false);
  const [errorMaterias, setErrorMaterias] = useState("");
  const [mensajeMaterias, setMensajeMaterias] = useState("");

  useEffect(() => {
    const cargar = rol === "DOCENTE"
      ? obtenerSolicitudesDocente().then((data) =>
          (Array.isArray(data) ? data : []).map((s) => ({
            id: s.solicitudId,
            numeroProceso: s.numeroProceso,
            carreraOrigen: s.carreraOrigen,
            carreraDestino: s.carreraDestino,
          }))
        )
      : obtenerMisSolicitudes();

    cargar
      .then((data) => setSolicitudes(Array.isArray(data) ? data : data?.data || []))
      .catch(() => setError("No se pudieron cargar sus solicitudes."));
  }, [rol]);

  const cargarSolicitud = useCallback(async (id) => {
    setDocumentos([]);
    setMaterias([]);
    setRequiereRevision(false);
    setMotivoRevision("");
    setEstadoSolicitud("");
    setErrorMaterias("");
    setMensajeMaterias("");

    if (!id) return;

    try {
      setDocumentos(await obtenerDocumentosSolicitud(id));
    } catch (e) {
      setError(e.message || "No se pudieron cargar los documentos de la solicitud.");
    }

    try {
      const lista = await obtenerMateriasAprobadas(id);
      setMaterias(Array.isArray(lista) ? lista : []);
    } catch {
      // Sin materias aprobadas todavía: la extracción las generará.
      setMaterias([]);
    }

    try {
      const detalle = await obtenerSolicitud(id);
      setEstadoSolicitud(detalle?.estado || "");
    } catch {
      setEstadoSolicitud("");
    }
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => cargarSolicitud(solicitudId), 0);
    return () => clearTimeout(timer);
  }, [solicitudId, cargarSolicitud]);

  function cambiarArchivo(e) {
    const file = e.target.files[0];
    setError("");
    setMensaje("");
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
    setMensaje("");
    try {
      const respuesta = await subirDocumento(solicitudId, tipoCodigo, archivo);
      const etiqueta = TIPOS.find((t) => t.codigo === tipoCodigo)?.etiqueta ?? tipoCodigo;
      const nuevo = {
        id: respuesta.documentoId,
        nombre: archivo.name,
        tipoDocumento: etiqueta,
        tamanoBytes: archivo.size,
        estado: "PROCESADO",
      };
      setDocumentos((prev) => [...prev, nuevo]);
      setArchivo(null);
      setClaveInput((n) => n + 1);
      setMensaje("Documento subido y procesado correctamente.");
    } catch (err) {
      setError(err.message || MENSAJE_RECHAZO);
    } finally {
      setCargando(false);
    }
  }

  // HU-20: extrae las materias del récord ya procesado.
  const extraer = async () => {
    if (!solicitudId) return;
    setExtrayendo(true);
    setError("");
    setMensaje("");
    setErrorMaterias("");
    setMensajeMaterias("");
    setRequiereRevision(false);
    setMotivoRevision("");
    try {
      const resultado = await extraerRecord(solicitudId);
      setMaterias(resultado?.materias ?? []);
      setRequiereRevision(Boolean(resultado?.requiereRevision));
      setMotivoRevision(resultado?.motivo || "");
      if (resultado?.requiereRevision) {
        setErrorMaterias(
          resultado?.motivo ||
            "El récord no se pudo leer con claridad. Revise y corrija las materias manualmente."
        );
      } else {
        const datosTitular = resultado?.nombreEstudiante || resultado?.cedulaEstudiante
          ? ` Datos del titular actualizados: ${resultado?.nombreEstudiante || "-"} · C.I.: ${
              resultado?.cedulaEstudiante || "-"
            }.`
          : "";
        setMensajeMaterias(
          `Materias extraídas del récord. Revise y corrija antes de guardar.${datosTitular}`
        );
      }
    } catch (err) {
      setErrorMaterias(err.message || "No se pudo leer el récord académico.");
    } finally {
      setExtrayendo(false);
    }
  };

  const guardarMaterias = async (lista) => {
    if (!solicitudId) return;
    setGuardandoMaterias(true);
    setErrorMaterias("");
    setMensajeMaterias("");
    try {
      const guardadas = await actualizarMateriasAprobadas(solicitudId, lista);
      setMaterias(Array.isArray(guardadas) ? guardadas : lista);
      setMensajeMaterias("Materias aprobadas guardadas correctamente.");
    } catch (err) {
      setErrorMaterias(err.message || "No se pudieron guardar las materias aprobadas.");
    } finally {
      setGuardandoMaterias(false);
    }
  };

  const soloLecturaMaterias = Boolean(estadoSolicitud) && estadoSolicitud !== "REGISTRADA";

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-11">
          <div className="card shadow-sm border-0">

            {/* ENCABEZADO */}
            <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
              <h5 className="mb-0 fw-bold">ADJUNTAR DOCUMENTOS</h5>
              <span className="badge bg-light text-primary fs-6">
                Rol: {rol === "COMITE" ? "Comité" : rol}
              </span>
            </div>

            <div className="card-body p-4">
              <h3 className="text-center fw-bold mb-4">SISTEMA DE HOMOLOGACIÓN - UTA</h3>

              {/* SELECTOR DE SOLICITUD */}
              <div className="mb-4">
                <label htmlFor="solicitud" className="form-label fw-bold">Solicitud</label>
                <select
                  id="solicitud"
                  className="form-select"
                  value={solicitudId}
                  onChange={(e) => { setSolicitudId(e.target.value); setError(""); setMensaje(""); }}
                >
                  <option value="">Seleccione una solicitud</option>
                  {solicitudes.map((s) => (
                    <option key={s.id} value={s.id}>
                      {(s.numeroProceso || s.numero || `SOL-${String(s.id).padStart(4, "0")}`)} ·{" "}
                      {s.origen || s.carreraOrigen} → {s.destino || s.carreraDestino}
                    </option>
                  ))}
                </select>
              </div>

              {/* SECCIÓN NUEVO DOCUMENTO */}
              <div className="border rounded p-4 bg-light mb-4">
                <h5 className="fw-bold mb-3">Nuevo documento</h5>

                <div className="row">
                  <div className="col-md-6 mb-3">
                    <label htmlFor="tipo" className="form-label fw-bold">Tipo de documento</label>
                    <select
                      id="tipo"
                      className="form-select"
                      value={tipoCodigo}
                      onChange={(e) => setTipoCodigo(e.target.value)}
                    >
                      {TIPOS.map((t) => (
                        <option key={t.codigo} value={t.codigo}>{t.etiqueta}</option>
                      ))}
                    </select>
                    <div className="form-text">
                      La malla, los sílabos y los planes de estudio ya están cargados en el
                      sistema para el agente interno. Para el agente externo se referencia
                      desde <strong>Ruta / URL externa</strong>.
                    </div>
                  </div>

                  <div className="col-md-6 mb-3">
                    <label htmlFor="archivo" className="form-label fw-bold">Archivo PDF</label>
                    <input
                      key={claveInput}
                      id="archivo"
                      type="file"
                      className="form-control"
                      accept=".pdf"
                      onChange={cambiarArchivo}
                    />
                    <div className="form-text">
                      Solo se aceptan archivos PDF. Tamaño máximo de 10 MB.
                    </div>
                  </div>
                </div>

                {error && (
                  <div className="alert alert-danger d-flex align-items-center mt-3 mb-0 py-2">
                    <span className="me-2">✖</span> {error}
                  </div>
                )}
                {mensaje && (
                  <div className="alert alert-success mt-3 mb-0 py-2">{mensaje}</div>
                )}

                <div className="text-end mt-4">
                  <button onClick={subir} disabled={cargando || !archivo} className="btn btn-primary px-4 fw-bold">
                    {cargando ? "Subiendo..." : "Adjuntar documento"}
                  </button>
                </div>
              </div>

              {/* LISTA DE DOCUMENTOS */}
              <div className="mb-4">
                <h5 className="fw-bold mb-3">Documentos de la solicitud</h5>
                <ListaDocumentos documentos={documentos} />
              </div>

              {/* MATERIAS APROBADAS DEL RÉCORD (HU-20) */}
              {solicitudId && (
                <div className="mb-4">
                  <div className="d-flex flex-wrap justify-content-between align-items-center mb-3 gap-2">
                    <h5 className="fw-bold mb-0">Materias aprobadas</h5>
                    <button
                      type="button"
                      className="btn btn-outline-primary"
                      onClick={extraer}
                      disabled={extrayendo || soloLecturaMaterias}
                    >
                      {extrayendo ? "Extrayendo..." : "Extraer materias del récord"}
                    </button>
                  </div>

                  {requiereRevision && (
                    <div className="alert alert-warning">
                      <strong>Requiere revisión.</strong>{" "}
                      {motivoRevision ||
                        "El documento no se pudo leer con claridad. Revise las materias manualmente."}
                    </div>
                  )}

                  {soloLecturaMaterias && (
                    <div className="alert alert-info">
                      La solicitud ya pasó a revisión: las materias aprobadas se muestran en
                      modo lectura.
                    </div>
                  )}

                  <TablaMateriasAprobadas
                    materias={materias}
                    onGuardar={guardarMaterias}
                    guardando={guardandoMaterias}
                    soloLectura={soloLecturaMaterias}
                    mensaje={mensajeMaterias}
                    error={errorMaterias}
                  />
                </div>
              )}

              {volverResultado && solicitudId && (
                <div className="alert alert-info d-flex justify-content-between align-items-center">
                  <span>
                    Suba al menos un documento procesado. Después continúe para generar el análisis externo.
                  </span>
                  <button
                    type="button"
                    className="btn btn-sm btn-primary"
                    onClick={() => navigate(`/resultado/${solicitudId}`)}
                  >
                    Continuar al resultado
                  </button>
                </div>
              )}

              <div className="alert alert-secondary small mb-0">
                El texto de cada PDF se extrae con PdfPig y el documento pasa a PROCESADO.
                <br />
                Solo ves los documentos de tus solicitudes: las registradas por el Coordinador,
                el Comité o las que te fueron delegadas como Docente.
              </div>
            </div>

          </div>
        </div>
      </div>
    </div>
  );
}
