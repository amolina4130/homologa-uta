import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { obtenerMisSolicitudes } from "../services/solicitudesService";
import { obtenerSolicitudesDocente } from "../services/docenteService";
import { procesarCarpeta, registrarUrlExterna } from "../services/rutaCarpetaService";

const RUTA_EJEMPLO = "C:/Users/Usuario/Downloads/CARPETAESTUDIANTE";

export default function RutaCarpeta() {
  const { usuario, rol } = useAuth();
  const [searchParams] = useSearchParams();
  const solicitudInicial = searchParams.get("solicitudId") || "";
  const modoInicial = searchParams.get("modo") === "URL" ? "URL" : "LOCAL";

  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudId, setSolicitudId] = useState(solicitudInicial);
  const [modo, setModo] = useState(modoInicial);
  const [ruta, setRuta] = useState("");
  const [url, setUrl] = useState("");
  const [documentos, setDocumentos] = useState([]);
  const [total, setTotal] = useState(0);
  const [cargandoSolicitudes, setCargandoSolicitudes] = useState(true);
  const [procesando, setProcesando] = useState(false);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  useEffect(() => {
    let activo = true;
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
      .then((data) => {
        if (activo) setSolicitudes(Array.isArray(data) ? data : data?.data || []);
      })
      .catch(() => {
        if (activo) setError("No se pudieron cargar sus solicitudes.");
      })
      .finally(() => {
        if (activo) setCargandoSolicitudes(false);
      });

    return () => {
      activo = false;
    };
  }, [rol]);

  const procesar = async (ev) => {
    ev.preventDefault();
    setError("");
    setMensaje("");
    setDocumentos([]);
    setTotal(0);

    if (!solicitudId) {
      setError("Seleccione una solicitud.");
      return;
    }

    const esUrl = modo === "URL";
    const valor = esUrl ? url.trim() : ruta.trim();
    if (!valor) {
      setError(
        esUrl
          ? "Ingrese la URL de la carpeta compartida."
          : "Ingrese la ruta de la carpeta de documentos."
      );
      return;
    }

    const usuarioSesion = usuario?.correo || usuario?.nombre || "";
    if (!usuarioSesion) {
      setError("No se pudo identificar al usuario de la sesión.");
      return;
    }

    setProcesando(true);
    try {
      const resultado = esUrl
        ? await registrarUrlExterna({
          solicitudId: Number(solicitudId),
          url: valor,
          usuario: usuarioSesion,
        })
        : await procesarCarpeta({
          solicitudId: Number(solicitudId),
          ruta: valor,
          usuario: usuarioSesion,
        });
      const lista = Array.isArray(resultado?.documentos) ? resultado.documentos : [];
      setDocumentos(lista);
      setTotal(resultado?.total ?? lista.length);
      setMensaje(
        esUrl
          ? `URL registrada para la solicitud. ${lista.length} referencia(s) guardada(s).`
          : `Carpeta procesada. Se detectaron ${resultado?.total ?? lista.length} documento(s).`
      );
    } catch (e) {
      setError(e.message || (esUrl ? "No se pudo registrar la URL." : "No se pudo procesar la carpeta."));
    } finally {
      setProcesando(false);
    }
  };

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-11">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">RUTA LOCAL / URL EXTERNA</h4>
              <small className="d-block fw-normal">
                Documentos y carpeta compartida registrados por el{" "}
                {rol === "COORDINADOR" ? "Coordinador" : rol === "DOCENTE" ? "Docente" : "Comité"}
              </small>
            </div>

            <div className="card-body p-4">
              <div className="alert alert-info small">
                La URL o carpeta queda guardada en la solicitud. El Docente la recibe cuando el
                Coordinador lo delega y <strong>no debe volver a subirla</strong>; solo la
                completaría si falta información. La modalidad (INTERNO o EXTERNO) también viaja
                con la solicitud, por eso el agente sabe qué caso está revisando.
              </div>

              <form onSubmit={procesar}>
                <div className="mb-4">
                  <label className="form-label fw-bold">Tipo de ubicación</label>
                  <div className="btn-group w-100" role="group" aria-label="Tipo de ubicación">
                    <button
                      type="button"
                      className={`btn btn-outline-primary ${modo === "LOCAL" ? "active" : ""}`}
                      onClick={() => { setModo("LOCAL"); setError(""); setMensaje(""); }}
                    >
                      Ruta local de carpeta
                    </button>
                    <button
                      type="button"
                      className={`btn btn-outline-primary ${modo === "URL" ? "active" : ""}`}
                      onClick={() => { setModo("URL"); setError(""); setMensaje(""); }}
                    >
                      URL externa (carpeta compartida)
                    </button>
                  </div>
                </div>

                <div className="row">
                  <div className="col-md-6 mb-3">
                    <label htmlFor="solicitudCarpeta" className="form-label fw-bold">
                      Solicitud
                    </label>
                    <select
                      id="solicitudCarpeta"
                      className="form-select"
                      value={solicitudId}
                      onChange={(e) => {
                        setSolicitudId(e.target.value);
                        setDocumentos([]);
                        setTotal(0);
                        setError("");
                        setMensaje("");
                      }}
                      disabled={cargandoSolicitudes || procesando}
                    >
                      <option value="">Seleccione una solicitud</option>
                      {solicitudes.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.numeroProceso || s.numero || `SOL-${String(s.id).padStart(4, "0")}`} ·{" "}
                          {s.carreraOrigen || s.origen || "-"} →{" "}
                          {s.carreraDestino || s.destino || "-"}
                        </option>
                      ))}
                    </select>
                    {cargandoSolicitudes && (
                      <div className="form-text">Cargando solicitudes...</div>
                    )}
                  </div>

                  <div className="col-md-6 mb-3">
                    {modo === "LOCAL" ? (
                      <>
                        <label htmlFor="rutaCarpeta" className="form-label fw-bold">
                          Ruta de la carpeta
                        </label>
                        <input
                          id="rutaCarpeta"
                          type="text"
                          className="form-control"
                          placeholder={RUTA_EJEMPLO}
                          value={ruta}
                          onChange={(e) => setRuta(e.target.value)}
                          disabled={procesando}
                        />
                        <div className="form-text">
                          Puede indicar solo la carpeta general{" "}
                          <code>C:/Users/Usuario/Downloads/CARPETAESTUDIANTE</code>. El sistema
                          entra a todas las subcarpetas (Malla, Sílabo, Récord, etc.) y clasifica
                          cada PDF por su nombre; no necesita poner una ruta por separado.
                        </div>
                      </>
                    ) : (
                      <>
                        <label htmlFor="urlExterna" className="form-label fw-bold">
                          URL de la carpeta compartida
                        </label>
                        <input
                          id="urlExterna"
                          type="url"
                          className="form-control"
                          placeholder="https://drive.google.com/drive/folders/..."
                          value={url}
                          onChange={(e) => setUrl(e.target.value)}
                          disabled={procesando}
                        />
                        <div className="form-text">
                          Use la opción <strong>URL externa</strong> cuando el titular entregó su
                          carpeta en Drive, OneDrive u otro servicio. La URL queda asociada a la
                          solicitud como referencia para el agente externo.
                        </div>
                      </>
                    )}
                  </div>
                </div>

                <div className="alert alert-info small">
                  {modo === "LOCAL" ? (
                    <>
                      La carpeta indicada puede ser la <strong>carpeta general del titular</strong>.
                      El sistema lee todos los PDF que encuentre dentro y en sus subcarpetas,
                      detecta la malla, el récord académico, los planes analíticos y los sílabos, y
                      muestra abajo el texto extraído. Esa es la información que usa el análisis.
                    </>
                  ) : (
                    <>
                      Registre la <strong>URL de la carpeta del titular</strong> para la homologación
                      externa. El récord académico se adjunta por separado en{" "}
                      <strong>Adjuntar documentos</strong> para obtener el nombre, la cédula y las
                      materias aprobadas.
                    </>
                  )}
                </div>

                {error && <div className="alert alert-danger">{error}</div>}
                {mensaje && (
                  <div className="alert alert-success mb-2">
                    <div>{mensaje}</div>
                    {rol === "COORDINADOR" && solicitudId && (
                      <Link
                        className="btn btn-sm btn-outline-primary mt-2"
                        to={`/delegar-docentes?solicitudId=${solicitudId}`}
                      >
                        Ir a delegar al Docente
                      </Link>
                    )}
                  </div>
                )}

                <div className="text-end">
                  <button
                    type="submit"
                    className="btn btn-primary px-4 fw-bold"
                    disabled={procesando || cargandoSolicitudes}
                  >
                    {procesando
                      ? "Procesando..."
                      : modo === "URL" ? "Registrar URL" : "Procesar carpeta"}
                  </button>
                </div>
              </form>

              <hr className="my-4" />

              <h5 className="fw-bold mb-3">Documentos detectados</h5>
              {documentos.length === 0 ? (
                <p className="text-muted">
                  {procesando
                    ? "Leyendo la carpeta..."
                    : "Procese una carpeta para ver los documentos detectados."}
                </p>
              ) : (
                <>
                  <p className="text-muted small mb-2">Total de documentos: {total}</p>
                  <div className="table-responsive">
                    <table className="table table-bordered align-middle">
                      <thead className="table-light">
                        <tr>
                          <th>Documento</th>
                          <th>Tipo</th>
                          <th>Texto extraído</th>
                        </tr>
                      </thead>
                      <tbody>
                        {documentos.map((doc, indice) => {
                          const texto = doc.textoExtraido || "";
                          return (
                            <tr key={doc.id ?? indice}>
                              <td>{doc.nombreArchivo || "-"}</td>
                              <td>{doc.tipoDocumento || "-"}</td>
                              <td className="small">
                                {texto ? (
                                  <span title={texto}>
                                    {texto.length > 180 ? `${texto.slice(0, 180)}…` : texto}
                                  </span>
                                ) : (
                                  <span className="text-muted">Sin texto extraído</span>
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
