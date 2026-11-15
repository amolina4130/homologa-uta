import { useState, useEffect } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerMisSolicitudes, subirDocumento } from "../services/documentosService";
import {
  extraerRecord,
  obtenerMateriasAprobadas,
  guardarMateriasAprobadas,
  preseleccionarMaterias,
} from "../services/catalogoService";
import ListaDocumentos from "../components/ListaDocumentos";
import TablaMateriasAprobadas from "../components/TablaMateriasAprobadas";

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

  // Récord académico (HU-20) y preselección (HU-21)
  const [materias, setMaterias] = useState([]);
  const [motivoRevision, setMotivoRevision] = useState("");
  const [pares, setPares] = useState(null);
  const [mensajeRecord, setMensajeRecord] = useState("");
  const [cargandoRecord, setCargandoRecord] = useState(false);

  useEffect(() => {
    obtenerMisSolicitudes()
      .then(setSolicitudes)
      .catch(() => setError("No se pudieron cargar sus solicitudes."));
  }, []);

  useEffect(() => {
    if (!solicitudId) {
      setMaterias([]);
      setPares(null);
      setMotivoRevision("");
      return;
    }
    setMensajeRecord("");
    setPares(null);
    obtenerMateriasAprobadas(solicitudId)
      .then((data) => setMaterias(Array.isArray(data) ? data : []))
      .catch(() => setMaterias([]));
  }, [solicitudId]);

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
        estado: "PROCESADO",
      };
      setDocumentosPorSolicitud((prev) => ({
        ...prev,
        [solicitudId]: [...(prev[solicitudId] ?? []), nuevo],
      }));
      setArchivo(null);
      setClaveInput((n) => n + 1);
      setMensajeRecord("Documento subido. Ahora puede extraer las materias aprobadas del historial académico.");
    } catch (err) {
      setError(err.message || MENSAJE_RECHAZO);
    } finally {
      setCargando(false);
    }
  }

  async function extraer() {
    if (!solicitudId) return setError("Seleccione una solicitud.");
    setCargandoRecord(true);
    setError("");
    setMensajeRecord("");
    try {
      const data = await extraerRecord(solicitudId);
      setMaterias(data.materias || []);
      setMotivoRevision(data.requiereRevision ? data.motivo || "" : "");
      setMensajeRecord(
        data.requiereRevision
          ? "El agente no pudo leer el récord automáticamente. Corrija la lista manualmente."
          : `Se extrajeron ${data.materias?.length ?? 0} materias del récord.`
      );
    } catch (err) {
      setError(err.message || "No se pudo extraer el récord académico.");
    } finally {
      setCargandoRecord(false);
    }
  }

  async function guardarMaterias() {
    if (!solicitudId) return;
    setCargandoRecord(true);
    setError("");
    setMensajeRecord("");
    try {
      const data = await guardarMateriasAprobadas(
        solicitudId,
        materias.map((m) => ({
          nombre: m.nombre,
          nota: m.nota === "" || m.nota == null ? null : Number(m.nota),
          estadoSegunDocumento: m.estadoSegunDocumento,
          periodo: m.periodo,
          asignaturaId: m.asignaturaId ?? null,
        }))
      );
      setMaterias(Array.isArray(data) ? data : []);
      setMensajeRecord("Materias aprobadas guardadas correctamente.");
    } catch (err) {
      setError(err.message || "No se pudieron guardar las materias aprobadas.");
    } finally {
      setCargandoRecord(false);
    }
  }

  async function preseleccionar() {
    if (!solicitudId) return;
    setCargandoRecord(true);
    setError("");
    try {
      setPares(await preseleccionarMaterias(solicitudId));
    } catch (err) {
      setError(err.message || "No se pudo ejecutar la preselección.");
    } finally {
      setCargandoRecord(false);
    }
  }

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-10">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
              <h5 className="mb-0 fw-bold">ADJUNTAR DOCUMENTOS</h5>
              <span className="badge bg-light text-primary fs-6">
                Rol: {rol === "ESTUDIANTE" ? "Estudiante" : rol}
              </span>
            </div>

            <div className="card-body p-4">
              <h3 className="text-center fw-bold mb-4">SISTEMA DE HOMOLOGACIÓN - UTA</h3>

              <div className="mb-4">
                <label htmlFor="solicitud" className="form-label fw-bold">Solicitud</label>
                <select
                  id="solicitud"
                  className="form-select"
                  value={solicitudId}
                  onChange={(e) => { setSolicitudId(e.target.value); setError(""); }}
                >
                  <option value="">Seleccione una solicitud</option>
                  {solicitudes.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.numeroProceso || s.numero || `SOL-${s.id}`} · {s.origen} → {s.destino}
                    </option>
                  ))}
                </select>
              </div>

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
                      Solo se aceptan archivos PDF. Tamaño máximo configurable (por ejemplo, 10 MB).
                    </div>
                  </div>
                </div>

                {error && (
                  <div className="alert alert-danger d-flex align-items-center mt-3 mb-0 py-2">
                    <span className="me-2">✖</span> {error}
                  </div>
                )}

                <div className="text-end mt-4">
                  <button onClick={subir} disabled={cargando || !archivo} className="btn btn-primary px-4 fw-bold">
                    {cargando ? "Subiendo..." : "Adjuntar documento"}
                  </button>
                </div>
              </div>

              <div className="mb-4">
                <h5 className="fw-bold mb-3">Documentos de la solicitud</h5>
                <ListaDocumentos documentos={documentosPorSolicitud[solicitudId] ?? []} />
              </div>

              {/* RÉCORD ACADÉMICO (HU-20) */}
              <div className="border rounded p-4 mb-4">
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <h5 className="fw-bold mb-0">Materias aprobadas del récord (HU-20)</h5>
                  <div>
                    <button
                      className="btn btn-sm btn-outline-primary me-2"
                      onClick={extraer}
                      disabled={!solicitudId || cargandoRecord}
                    >
                      {cargandoRecord ? "Procesando..." : "Extraer del récord"}
                    </button>
                    <button
                      className="btn btn-sm btn-outline-success me-2"
                      onClick={guardarMaterias}
                      disabled={!solicitudId || cargandoRecord || materias.length === 0}
                    >
                      Guardar correcciones
                    </button>
                    <button
                      className="btn btn-sm btn-outline-dark"
                      onClick={preseleccionar}
                      disabled={!solicitudId || cargandoRecord}
                    >
                      Preseleccionar (HU-21)
                    </button>
                  </div>
                </div>

                {mensajeRecord && <div className="alert alert-info py-2">{mensajeRecord}</div>}
                {motivoRevision && <div className="alert alert-warning py-2">{motivoRevision}</div>}

                <TablaMateriasAprobadas materias={materias} onChange={setMaterias} editable />

                {pares && (
                  <div className="mt-3">
                    <h6 className="fw-bold">Materias candidatas por área y nivel</h6>
                    {pares.pares?.length === 0 ? (
                      <p className="text-muted mb-0">No se encontraron candidatas.</p>
                    ) : (
                      <div className="table-responsive">
                        <table className="table table-sm table-bordered align-middle mb-0">
                          <thead className="table-light">
                            <tr>
                              <th>Materia de origen</th>
                              <th>Área</th>
                              <th>Candidatas de destino</th>
                            </tr>
                          </thead>
                          <tbody>
                            {pares.pares?.map((p, i) => (
                              <tr key={i}>
                                <td>{p.materiaOrigen}</td>
                                <td>{p.areaOrigen}</td>
                                <td>
                                  {p.candidatasDestino?.map((c) => c.nombre).join(", ") || "—"}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                    {pares.sinCandidata?.length > 0 && (
                      <div className="alert alert-warning mt-2 mb-0 py-2">
                        Sin candidata: {pares.sinCandidata.join(", ")}
                      </div>
                    )}
                  </div>
                )}
              </div>

              <div className="alert alert-secondary small mb-0">
                El texto de cada PDF se extrae con PdfPig y el documento pasa a PROCESADO.
                <br />
                Solo ves los documentos de tus propias solicitudes.
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
