import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  descargarInformeDocente,
  evaluarSolicitud,
  generarSugerenciaDocente,
  obtenerDetalleDocente,
  obtenerRecomendacionInterna,
} from "../services/docenteService";
import { useAuth } from "../context/AuthContext";

const normalizarAsignaturas = (detalle) => {
  const lista =
    detalle?.asignaturas ||
    detalle?.detalleAsignaturas ||
    detalle?.detalle?.asignaturas ||
    [];

  if (!Array.isArray(lista)) return [];

  return lista.map((a) => ({
    asignaturaId: a.asignaturaId ?? a.id,
    origen:
      a.asignaturaOrigen ||
      a.nombreAsignatura ||
      a.asignatura ||
      a.nombre ||
      a.asignaturaDestino ||
      `Asignatura ${a.asignaturaId ?? a.id ?? ""}`,
    destino: a.asignaturaDestino || "",
    homologable:
      typeof a.homologable === "boolean"
        ? a.homologable
        : (a.decision || a.propuesta?.decision) === "HOMOLOGABLE",
    justificacion: a.justificacion || a.propuesta?.justificacion || "",
    porcentajeContenido:
      a.porcentajeContenido ?? a.propuesta?.porcentajeContenido ?? 0,
  }));
};

const desdeSugerencia = (lista) =>
  (Array.isArray(lista) ? lista : []).map((a) => ({
    asignaturaId: a.asignaturaId,
    origen: a.asignaturaOrigen || "",
    destino: a.asignaturaDestino || "",
    homologable: Boolean(a.homologable),
    justificacion: a.justificacion || "",
    porcentajeContenido: a.porcentajeContenido || 0,
  }));

const paraEnvio = (lista) =>
  lista.map((a) => ({
    asignaturaId: a.asignaturaId,
    homologable: Boolean(a.homologable),
    justificacion: (a.justificacion || "").trim(),
  }));

const esInterno = (detalle) =>
  String(detalle?.modulo || "INTERNO").trim().toUpperCase() !== "EXTERNO";

const datosFirmaDocente = (datos) => ({
  nombreDocente: (datos?.nombre || "").trim(),
  docenteCedula: (datos?.cedula || "").trim(),
  docenteCorreoInforme: (datos?.correo || "").trim(),
});

export default function EvaluacionDocente() {
  const { id } = useParams();
  const { usuario } = useAuth();
  const [detalle, setDetalle] = useState(null);
  const [delegacion, setDelegacion] = useState(null);
  const [asignaturas, setAsignaturas] = useState([]);
  const [observacionGeneral, setObservacionGeneral] = useState("");
  const [estadoRevision, setEstadoRevision] = useState("PENDIENTE");
  const [informeTexto, setInformeTexto] = useState("");
  const [informeEditado, setInformeEditado] = useState(false);
  const [datosDocente, setDatosDocente] = useState({
    nombre: usuario?.nombre || "",
    cedula: "",
    correo: usuario?.correo || "",
  });
  const [sugerencia, setSugerencia] = useState(null);
  const [recomendacion, setRecomendacion] = useState(null);
  const [mostrarRecomendacion, setMostrarRecomendacion] = useState(false);
  const [puedeEvaluar, setPuedeEvaluar] = useState(true);
  const [cargando, setCargando] = useState(true);
  const [generando, setGenerando] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [recomendando, setRecomendando] = useState(false);
  const [descargando, setDescargando] = useState(false);
  const [error, setError] = useState("");
  const [estado, setEstado] = useState("");

  useEffect(() => {
    let activo = true;

    obtenerDetalleDocente(id)
      .then((datos) => {
        if (!activo) return;
        const detalleCargado = datos?.detalle || datos;
        const sugerenciaCargada = datos?.sugerencia || null;
        setDetalle(detalleCargado);
        setDelegacion(datos?.delegacion || detalleCargado?.delegacion || null);
        setSugerencia(sugerenciaCargada);
        setAsignaturas(
          sugerenciaCargada?.asignaturas?.length
            ? desdeSugerencia(sugerenciaCargada.asignaturas)
            : normalizarAsignaturas(detalleCargado)
        );
        setObservacionGeneral(sugerenciaCargada?.observacionGeneral || "");
        setInformeTexto(datos?.informeSugeridoTexto || sugerenciaCargada?.informeTexto || "");
        setInformeEditado(false);
        setDatosDocente((prev) => ({
          nombre:
            datos?.delegacion?.nombreDocente ||
            datos?.nombreDocente ||
            prev.nombre ||
            datos?.delegacion?.docenteCorreo ||
            "",
          cedula: datos?.delegacion?.docenteCedula || datos?.docenteCedula || prev.cedula,
          correo:
            datos?.delegacion?.docenteCorreoInforme ||
            datos?.delegacion?.docenteCorreo ||
            datos?.docenteCorreoInforme ||
            prev.correo,
        }));
        setEstadoRevision(
          datos?.delegacion?.estado === "REVISADO" ? "REVISADO" : "PENDIENTE"
        );
        setPuedeEvaluar(datos?.puedeEvaluar !== false);
      })
      .catch((e) => {
        if (activo) setError(e.message || "No se pudo cargar el detalle de la solicitud.");
      })
      .finally(() => {
        if (activo) setCargando(false);
      });

    return () => {
      activo = false;
    };
  }, [id]);

  const cambiarAsignatura = (indice, campo, valor) => {
    setAsignaturas((prev) =>
      prev.map((a, i) => (i === indice ? { ...a, [campo]: valor } : a))
    );
  };

  const aplicarSugerencia = (resultado) => {
    setSugerencia(resultado);
    setAsignaturas(desdeSugerencia(resultado?.asignaturas));
    setInformeTexto(resultado?.informeTexto || "");
    setInformeEditado(false);
    setObservacionGeneral(resultado?.observacionGeneral || "");
  };

  const regenerar = async (lista, opciones = {}) => {
    setError("");
    setEstado("");

    if (!(datosDocente.nombre || "").trim() || !(datosDocente.cedula || "").trim()) {
      setError("Ingrese su nombre y su cédula en Datos del docente antes de generar el informe.");
      return;
    }

    setGenerando(true);
    try {
      const resultado = await generarSugerenciaDocente(id, {
        ...datosFirmaDocente(datosDocente),
        asignaturas: paraEnvio(lista),
        observacionGeneral: (observacionGeneral || "").trim(),
        reutilizarOpinion:
          opciones.reutilizarOpinion !== undefined
            ? opciones.reutilizarOpinion
            : Boolean(sugerencia),
      });
      aplicarSugerencia(resultado);
      setEstado("Sugerencia generada. Revísela, agregue observaciones y envíela cuando esté lista.");
    } catch (e) {
      setError(e.message || "No se pudo generar la sugerencia con IA.");
    } finally {
      setGenerando(false);
    }
  };

  const cargarRecomendacion = async () => {
    setError("");
    setRecomendando(true);
    setMostrarRecomendacion(true);
    try {
      const resultado = await obtenerRecomendacionInterna(id);
      setRecomendacion(resultado);
      if (resultado?.disponible === false) {
        setError(resultado.motivo || "La recomendación no está disponible para esta solicitud.");
      }
    } catch (e) {
      setError(e.message || "No se pudo obtener la recomendación del agente interno.");
    } finally {
      setRecomendando(false);
    }
  };

  const aplicarRecomendacion = async () => {
    const items = recomendacion?.recomendaciones || [];
    if (items.length === 0) return;

    const siguiente = asignaturas.map((a) => {
      const recomendada = items.find((r) => r.asignaturaId === a.asignaturaId);
      if (!recomendada) return a;
      return {
        ...a,
        homologable: Boolean(recomendada.homologable),
        justificacion: recomendada.justificacion || a.justificacion,
        destino: recomendada.asignaturaDestino || a.destino,
      };
    });

    setAsignaturas(siguiente);
    setEstado("Recomendación aplicada. Actualice la sugerencia y envíela cuando esté lista.");
    await regenerar(siguiente, { reutilizarOpinion: Boolean(sugerencia) });
  };

  const enviarInforme = async () => {
    setError("");
    setEstado("");

    if (!sugerencia) {
      setError("Primero genere la sugerencia con IA.");
      return;
    }

    if (estadoRevision !== "REVISADO") {
      setError("Cambie el estado de su revisión a REVISADO antes de enviar el informe.");
      return;
    }

    if (!(datosDocente.nombre || "").trim() || !(datosDocente.cedula || "").trim()) {
      setError("Ingrese su nombre y su cédula en Datos del docente antes de enviar el informe.");
      return;
    }

    if (
      asignaturas.some((a) => a.homologable && !(a.justificacion || "").trim())
    ) {
      setError("Escriba la justificación de cada asignatura que marque como Homologo.");
      return;
    }

    setEnviando(true);
    try {
      const resultado = await evaluarSolicitud(id, {
        ...datosFirmaDocente(datosDocente),
        informeTexto: informeEditado ? informeTexto : undefined,
        asignaturas: paraEnvio(asignaturas),
        observacionGeneral: (observacionGeneral || "").trim(),
        estadoRevision: "REVISADO",
      });

      setInformeTexto(resultado?.informeTexto || informeTexto);
      setSugerencia({ ...sugerencia, enviada: true });
      setPuedeEvaluar(false);
      setDelegacion((prev) => ({
        ...(prev || {}),
        estado: "REVISADO",
        estadoDelegacion: "REVISADO",
        revisadoPor: usuario?.correo || datosDocente.correo || "",
      }));
      setEstado(
        "Su revisión quedó marcada como REVISADO y el informe sugerido fue enviado al Coordinador."
      );
    } catch (e) {
      setError(e.message || "No se pudo enviar el informe sugerido.");
    } finally {
      setEnviando(false);
    }
  };

  const descargar = async () => {
    setError("");
    setEstado("");
    setDescargando(true);
    try {
      await descargarInformeDocente(id);
      setEstado("Informe descargado.");
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe.");
    } finally {
      setDescargando(false);
    }
  };

  const numero =
    detalle?.numeroProceso ||
    detalle?.numero ||
    `SOL-${String(id).padStart(4, "0")}`;
  const correoActual = (usuario?.correo || datosDocente.correo || "").trim().toLowerCase();
  const revisadoPorReal = (delegacion?.revisadoPor || "").trim();
  const esMiRevision =
    delegacion?.estado === "REVISADO" ||
    (Boolean(revisadoPorReal) && revisadoPorReal.toLowerCase() === correoActual);
  const bloqueoPorOtro = !puedeEvaluar && !esMiRevision;
  const nombreRevisor =
    revisadoPorReal ||
    (esMiRevision ? datosDocente.nombre || usuario?.correo || "usted" : "");
  const yaEnviado = Boolean(sugerencia?.enviada) || esMiRevision;
  const bloqueado = !puedeEvaluar || enviando;

  return (
    <div className="container mt-4">
      <div className="card shadow-sm border-0">
        <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
          <div>
            <h4 className="mb-0 fw-bold">EVALUACIÓN DEL DOCENTE — {numero}</h4>
            <small className="d-block fw-normal">
              Solicitud registrada por el Coordinador
            </small>
          </div>
          <Link to="/solicitudes-docente" className="btn btn-sm btn-outline-light">
            Volver a la bandeja
          </Link>
        </div>

        <div className="card-body p-4">
          <div className="alert alert-info">
            <strong>Revisión en tres pasos:</strong> (0) confirme su nombre, cédula y correo en
            <em> Datos del docente</em>; (1) pulse <em>Generar sugerencia con IA</em> para que el
            agente proponga un informe con esos datos; (2) agregue observaciones y marque{" "}
            <strong>Homologo</strong> o <strong>No homologo</strong> en cada materia; (3) envíe
            manualmente el informe sugerido al Coordinador. La URL y los documentos ya los registró
            el Coordinador: no tiene que volver a subirlos. El informe final para el Consejo
            Directivo lo emite el Coordinador.
          </div>

          {detalle && (
            <div className="alert alert-secondary small">
              <strong>
                Solicitud {esInterno(detalle) ? "INTERNA · agente UTA" : "EXTERNA · otra universidad"}
                .
              </strong>{" "}
              {esInterno(detalle)
                ? "El agente compara con las mallas y planes analíticos ya cargados en el sistema."
                : `Universidad de origen: ${detalle.universidadOrigen || "-"}. Universidad de destino: ${
                    detalle.universidadDestino || "-"
                  }. El agente externo usa la URL y los documentos guardados en la solicitud.`}
            </div>
          )}

          {bloqueoPorOtro && (
            <div className="alert alert-warning">
              <strong>
                Esta solicitud ya fue revisada por {nombreRevisor || "otro docente"}.
              </strong>{" "}
              Su delegación quedó en <strong>YA_REVISADO</strong>: solo un docente envía el
              informe sugerido. Ya no puede modificarlo ni enviar otro; puede consultarlo y
              descargarlo.
            </div>
          )}

          {esMiRevision && (
            <div className="alert alert-success">
              <strong>Usted registró esta revisión como REVISADO.</strong> El informe sugerido
              fue enviado al Coordinador por {nombreRevisor}. No puede modificarse, pero puede
              consultarlo y descargarlo.
            </div>
          )}

          {cargando && <div className="alert alert-secondary">Cargando solicitud...</div>}
          {error && <div className="alert alert-danger">{error}</div>}
          {estado && <div className="alert alert-success">{estado}</div>}

          {!cargando && detalle && (
            <div className="row mb-4">
              <div className="col-md-6">
                <p className="mb-1">
                  <strong>Titular del récord:</strong> {detalle.estudiante || "-"}
                </p>
                <p className="mb-1">
                  <strong>Cédula del titular:</strong> {detalle.estudianteCedula || "-"}
                </p>
                <p className="mb-1">
                  <strong>Coordinador:</strong>{" "}
                  {delegacion?.coordinadorCorreo ||
                    detalle.coordinadorCorreo ||
                    "-"}
                </p>
                <p className="mb-1">
                  <strong>Modalidad:</strong>{" "}
                  <span
                    className={`badge ${
                      !esInterno(detalle) ? "bg-info text-dark" : "bg-primary"
                    }`}
                  >
                    {detalle.modulo || "INTERNO"}
                  </span>
                </p>
                <p className="mb-1">
                  <strong>Carreras:</strong> {detalle.carreraOrigen || "-"} →{" "}
                  {detalle.carreraDestino || "-"}
                </p>
              </div>
              <div className="col-md-6">
                <p className="mb-1">
                  <strong>Estado de la delegación:</strong>{" "}
                  {delegacion?.estado || delegacion?.estadoDelegacion || "Asignada"}
                </p>
                <p className="mb-1">
                  <strong>Revisado por:</strong>{" "}
                  {nombreRevisor || (bloqueoPorOtro ? "No disponible" : "Aún sin revisar")}
                </p>
                {delegacion?.resultadoDocente && (
                  <p className="mb-1">
                    <strong>Resultado del Docente:</strong>{" "}
                    <span
                      className={`badge ${
                        delegacion.resultadoDocente === "APROBADO"
                          ? "bg-success"
                          : "bg-danger"
                      }`}
                    >
                      {delegacion.resultadoDocente}
                    </span>
                  </p>
                )}
                <p className="mb-1">
                  <strong>Sugerencia:</strong>{" "}
                  {sugerencia
                    ? yaEnviado
                      ? esMiRevision
                        ? "Enviada por usted al Coordinador"
                        : `Enviada por ${nombreRevisor || "otro docente"}`
                      : "Generada · pendiente de envío"
                    : "Todavía no generada"}
                </p>
              </div>
            </div>
          )}

          <div className="card border-secondary mb-4">
            <div className="card-header bg-light fw-bold">
              Datos del docente que firma el informe
            </div>
            <div className="card-body p-3">
              <div className="row g-3">
                <div className="col-md-4">
                  <label htmlFor="docenteNombre" className="form-label small fw-bold mb-1">
                    Nombre completo
                  </label>
                  <input
                    id="docenteNombre"
                    type="text"
                    className="form-control form-control-sm"
                    value={datosDocente.nombre}
                    onChange={(e) =>
                      setDatosDocente((prev) => ({ ...prev, nombre: e.target.value }))
                    }
                    disabled={bloqueado}
                    placeholder="Ej.: Ing. María Pérez"
                  />
                </div>
                <div className="col-md-4">
                  <label htmlFor="docenteCedula" className="form-label small fw-bold mb-1">
                    Cédula
                  </label>
                  <input
                    id="docenteCedula"
                    type="text"
                    className="form-control form-control-sm"
                    value={datosDocente.cedula}
                    onChange={(e) =>
                      setDatosDocente((prev) => ({ ...prev, cedula: e.target.value }))
                    }
                    disabled={bloqueado}
                    placeholder="Ej.: 1800000001"
                  />
                </div>
                <div className="col-md-4">
                  <label htmlFor="docenteCorreo" className="form-label small fw-bold mb-1">
                    Correo para el informe
                  </label>
                  <input
                    id="docenteCorreo"
                    type="email"
                    className="form-control form-control-sm"
                    value={datosDocente.correo}
                    onChange={(e) =>
                      setDatosDocente((prev) => ({ ...prev, correo: e.target.value }))
                    }
                    disabled={bloqueado}
                    placeholder="Ej.: docente@uta.edu.ec"
                  />
                </div>
              </div>
              <div className="form-text mt-2">
                Estos datos se imprimen en el informe sugerido y en el PDF. Si los cambia después
                de generar, pulse <em>Regenerar sugerencia con IA</em> o edite directamente el
                texto del informe.
              </div>
            </div>
          </div>

          <div className="d-flex flex-wrap justify-content-between align-items-center mb-2">
            <h5 className="fw-bold mb-0">Asignaturas a evaluar</h5>
            {esInterno(detalle) && puedeEvaluar && (
              <button
                type="button"
                className="btn btn-link btn-sm p-0"
                onClick={cargarRecomendacion}
                disabled={recomendando}
              >
                {recomendando
                  ? "Consultando al agente interno..."
                  : "IA me recomienda qué materias homologar"}
              </button>
            )}
          </div>

          {mostrarRecomendacion && (
            <div className="card border-info mb-3">
              <div className="card-header bg-light fw-bold small">
                Recomendación del agente interno
                {recomendacion?.huellaMemoria
                  ? ` · memoria ${recomendacion.huellaMemoria.substring(0, 10)}`
                  : ""}
              </div>
              <div className="card-body p-2">
                {recomendacion?.motivo && (
                  <p className="small text-muted mb-2">{recomendacion.motivo}</p>
                )}
                {recomendando && <p className="small mb-0">Consultando catálogo y planes analíticos...</p>}
                {!recomendando && (recomendacion?.recomendaciones || []).length === 0 && (
                  <p className="small text-muted mb-0">No hay recomendaciones disponibles.</p>
                )}
                {!recomendando && (recomendacion?.recomendaciones || []).length > 0 && (
                  <>
                    <div className="table-responsive">
                      <table className="table table-sm table-bordered mb-2">
                        <thead className="table-light">
                          <tr>
                            <th>Materia de origen</th>
                            <th>Destino sugerido</th>
                            <th className="text-center">¿Homologar?</th>
                            <th className="text-center">%</th>
                            <th>Justificación</th>
                          </tr>
                        </thead>
                        <tbody>
                          {recomendacion.recomendaciones.map((r) => (
                            <tr key={r.asignaturaId}>
                              <td>{r.asignaturaOrigen}</td>
                              <td>{r.asignaturaDestino || "-"}</td>
                              <td className="text-center">
                                <span
                                  className={`badge ${
                                    r.homologable ? "bg-success" : "bg-secondary"
                                  }`}
                                >
                                  {r.homologable ? "Sí" : "No"}
                                </span>
                              </td>
                              <td className="text-center">{r.porcentajeContenido}%</td>
                              <td className="small">{r.justificacion}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-info"
                      onClick={aplicarRecomendacion}
                      disabled={bloqueado || generando}
                    >
                      Aplicar recomendación a mi revisión
                    </button>
                  </>
                )}
              </div>
            </div>
          )}

          {!cargando && asignaturas.length === 0 ? (
            <p className="text-muted">No hay asignaturas registradas en esta solicitud.</p>
          ) : (
            <div className="table-responsive">
              <table className="table table-bordered align-middle">
                <thead className="table-light">
                  <tr>
                    <th>Materia de origen</th>
                    <th>Equivalente en destino</th>
                    <th className="text-center" style={{ width: "140px" }}>
                      Decisión
                    </th>
                    <th>Justificación</th>
                  </tr>
                </thead>
                <tbody>
                  {asignaturas.map((a, indice) => (
                    <tr key={a.asignaturaId ?? indice}>
                      <td>{a.origen}</td>
                      <td>{a.destino || "Sin equivalente en la malla destino"}</td>
                      <td className="text-center">
                        <select
                          className="form-select form-select-sm"
                          value={a.homologable ? "SI" : "NO"}
                          onChange={(e) =>
                            cambiarAsignatura(
                              indice,
                              "homologable",
                              e.target.value === "SI"
                            )
                          }
                          disabled={bloqueado}
                          aria-label={`Decisión de ${a.origen}`}
                        >
                          <option value="SI">Homologo</option>
                          <option value="NO">No homologo</option>
                        </select>
                      </td>
                      <td>
                        <textarea
                          className="form-control form-control-sm"
                          rows="2"
                          placeholder="Escriba la justificación de su sugerencia..."
                          value={a.justificacion}
                          onChange={(e) =>
                            cambiarAsignatura(indice, "justificacion", e.target.value)
                          }
                          disabled={bloqueado}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="mb-3 mt-4">
            <label htmlFor="observacionGeneral" className="form-label fw-bold">
              Observaciones del Docente
            </label>
            <textarea
              id="observacionGeneral"
              className="form-control"
              rows="3"
              placeholder="Observaciones generales sobre la solicitud..."
              value={observacionGeneral}
              onChange={(e) => setObservacionGeneral(e.target.value)}
              disabled={bloqueado}
            />
            <div className="mt-3 p-3 border rounded bg-light">
              <label htmlFor="estadoRevision" className="form-label fw-bold mb-1">
                Estado de mi revisión
              </label>
              <select
                id="estadoRevision"
                className="form-select form-select-sm"
                value={estadoRevision}
                onChange={(e) => {
                  setEstadoRevision(e.target.value);
                  setError("");
                }}
                disabled={bloqueado}
                aria-describedby="ayudaEstadoRevision"
              >
                <option value="PENDIENTE">PENDIENTE · todavía no he enviado</option>
                <option value="REVISADO">REVISADO · revisé y voy a enviar</option>
              </select>
              <div id="ayudaEstadoRevision" className="form-text mt-1">
                El estado no cambia automáticamente al guardar observaciones ni al generar la
                sugerencia. Usted debe elegir <strong>REVISADO</strong> y después pulsar el botón
                de envío. Así queda claro que revisó y que solo un docente envió el informe.
              </div>
            </div>
            {sugerencia && !yaEnviado && (
              <button
                type="button"
                className="btn btn-link btn-sm p-0 mt-1"
                onClick={() => regenerar(asignaturas, { reutilizarOpinion: true })}
                disabled={generando || bloqueado}
              >
                Actualizar texto de la vista previa con mis observaciones
              </button>
            )}
          </div>

          <div className="d-flex flex-wrap gap-2">
            <button
              type="button"
              className="btn btn-primary fw-bold"
              onClick={() => regenerar(asignaturas, { reutilizarOpinion: false })}
              disabled={generando || cargando || bloqueado}
            >
              {generando
                ? "Generando sugerencia con IA..."
                : sugerencia
                  ? "Regenerar sugerencia con IA"
                  : "Generar sugerencia con IA"}
            </button>
            <button
              type="button"
              className="btn btn-success fw-bold"
              onClick={enviarInforme}
              disabled={
                enviando ||
                cargando ||
                bloqueado ||
                !sugerencia ||
                estadoRevision !== "REVISADO"
              }
              title={
                estadoRevision !== "REVISADO"
                  ? "Marque REVISADO en Estado de mi revisión antes de enviar"
                  : undefined
              }
            >
              {enviando
                ? "Enviando al Coordinador..."
                : "Marcar como REVISADO y enviar informe"}
            </button>
            <button
              type="button"
              className="btn btn-outline-primary"
              onClick={descargar}
              disabled={descargando || !informeTexto}
            >
              {descargando ? "Descargando..." : "Descargar PDF"}
            </button>
          </div>
          <div className="form-text mt-2">
            Generar o guardar observaciones no envía nada y deja el estado en PENDIENTE. El envío
            al Coordinador solo ocurre cuando marque REVISADO y pulse el botón verde. La IA
            propone el texto; la decisión final de esta etapa es suya.
          </div>

          {informeTexto && (
            <div className="card border-info mt-4">
              <div className="card-header bg-info text-white fw-bold">
                {yaEnviado
                  ? "Informe sugerido enviado"
                  : "Informe sugerido (editable, aún no enviado)"}
              </div>
              <div className="card-body">
                {yaEnviado ? (
                  <pre className="mb-0" style={{ whiteSpace: "pre-wrap" }}>
                    {informeTexto}
                  </pre>
                ) : (
                  <>
                    <textarea
                      className="form-control"
                      rows="18"
                      aria-label="Texto del informe sugerido"
                      value={informeTexto}
                      onChange={(e) => {
                        setInformeTexto(e.target.value);
                        setInformeEditado(true);
                      }}
                    />
                    <div className="form-text mt-2">
                      Puede corregir aquí su nombre, cédula, correo o cualquier párrafo. La tabla
                      de las materias que sí se pueden homologar aparece en el PDF con el encabezado
                      <strong> “Tabla de asignaturas homologables (materias que se pueden homologar)”</strong>.
                    </div>
                    <button
                      type="button"
                      className="btn btn-link btn-sm p-0 mt-2"
                      onClick={() => regenerar(asignaturas, { reutilizarOpinion: true })}
                      disabled={generando || bloqueado}
                    >
                      Volver a generar el texto con mis datos y observaciones actuales
                    </button>
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
