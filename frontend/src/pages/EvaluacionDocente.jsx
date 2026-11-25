import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  analizarSolicitudDocente,
  descargarInformeDocente,
  evaluarSolicitud,
  generarSugerenciaDocente,
  obtenerDetalleDocente,
  solicitarDocumentacion,
} from "../services/docenteService";
import { useAuth } from "../context/AuthContext";
import { Paso } from "../components/revision/PiezasRevision";
import { TarjetaSugerencia } from "../components/revision/SugerenciasIA";
import DecisionesDocente from "../components/revision/DecisionesDocente";
import { JUSTIFICACION_ACEPTA, requiereJustificacion } from "../components/revision/reglasDecision";

// Evaluación del docente (etapa 3). Recibe los datos del estudiante, el récord y la sugerencia
// ORIGINAL de la IA (que no se modifica). Por cada sugerencia decide homologar o no, con
// justificación obligatoria; puede cambiar el destino o agregar propuestas que la IA no vio, y
// pedir documentación adicional. Al final envía su informe técnico al Coordinador.

const esExterno = (d) => String(d?.modulo || "INTERNO").toUpperCase() === "EXTERNO";
const propuestaIA = (a) => (a.propuesta?.decision || "").toUpperCase();
// Solo las materias aprobadas del récord pueden proponerse (el récord puede traer reprobadas).
const estaAprobada = (m) => !/reprob|nos*aprob|retir|anulad|p[eé]rdid|desaprob/i.test(m?.estadoSegunDocumento || "");
const PROPUESTA_VACIA = { asignaturaOrigen: "", asignaturaDestinoId: "", homologable: true, justificacion: "" };

export default function EvaluacionDocente() {
  const { id } = useParams();
  const { usuario } = useAuth();

  const [datos, setDatos] = useState(null);
  const [decisiones, setDecisiones] = useState({}); // asignaturaId -> { homologable, justificacion }
  const [nuevas, setNuevas] = useState([]);
  const [observacion, setObservacion] = useState("");
  const [firma, setFirma] = useState({ nombre: usuario?.nombre || "", cedula: usuario?.cedula || "", correo: usuario?.correo || "" });
  const [informe, setInforme] = useState("");
  const [informeEditado, setInformeEditado] = useState(false);
  const [confirmado, setConfirmado] = useState(false);
  const [pedido, setPedido] = useState("");

  const [cargando, setCargando] = useState(true);
  const [ocupado, setOcupado] = useState("");
  const [aviso, setAviso] = useState(null);

  const aplicarDatos = (d) => {
    setDatos(d);
    const borrador = Object.fromEntries((d?.sugerencia?.asignaturas || []).map((a) => [a.asignaturaId, a]));
    const iniciales = {};
    for (const a of d?.detalle?.asignaturas || []) {
      if ((a.propuestaPor || "IA") !== "IA") continue;
      const previo = borrador[a.id];
      // Sin borrador, se parte de lo que recomendó la IA; las "en revisión" quedan sin decidir.
      iniciales[a.id] = previo
        ? {
            homologable: Boolean(previo.homologable),
            justificacion: previo.justificacion === JUSTIFICACION_ACEPTA ? "" : previo.justificacion || "",
          }
        : {
            homologable: propuestaIA(a) === "REQUIERE_REVISION" ? null : propuestaIA(a) === "HOMOLOGABLE",
            justificacion: "",
          };
    }
    setDecisiones(iniciales);
    setObservacion(d?.sugerencia?.observacionGeneral || "");
    setInforme(d?.informeSugeridoTexto || d?.sugerencia?.informeTexto || "");
    setInformeEditado(false);
    setFirma((f) => ({
      nombre: d?.nombreDocente && !d.nombreDocente.includes("@") ? d.nombreDocente : f.nombre,
      cedula: d?.docenteCedula && d.docenteCedula !== "-" ? d.docenteCedula : f.cedula,
      correo: d?.docenteCorreoInforme || f.correo,
    }));
  };

  useEffect(() => {
    let activo = true;
    obtenerDetalleDocente(id)
      .then((d) => activo && aplicarDatos(d))
      .catch((e) => activo && setAviso({ tipo: "danger", texto: e.message || "No se pudo cargar la solicitud." }))
      .finally(() => activo && setCargando(false));
    return () => {
      activo = false;
    };
  }, [id]);

  const detalle = datos?.detalle;
  const delegacion = datos?.delegacion;
  const filasIA = useMemo(() => (detalle?.asignaturas || []).filter((a) => (a.propuestaPor || "IA") === "IA"), [detalle]);
  const filasDocente = (detalle?.asignaturas || []).filter((a) => a.propuestaPor === "DOCENTE");
  const estado = (delegacion?.estado || "").toUpperCase();
  const enviada = estado === "REVISADO" || estado === "YA_REVISADO";
  const bloqueado = !datos?.puedeEvaluar || enviada;
  const numero = detalle?.numeroProceso || `SOL-${String(id).padStart(4, "0")}`;

  const cambiarDecision = (asigId, campo, valor) =>
    setDecisiones((prev) => ({ ...prev, [asigId]: { ...prev[asigId], [campo]: valor } }));

  // Modificar la equivalencia: la fila de la IA queda "No homologar" y se abre una propuesta
  // nueva con la misma materia de origen para elegir el destino correcto.
  const modificarDestino = (a) => {
    cambiarDecision(a.id, "homologable", false);
    if (!decisiones[a.id]?.justificacion)
      cambiarDecision(a.id, "justificacion", "La equivalencia propuesta por la IA no corresponde; se propone otro destino.");
    const origen = (a.materiasOrigen?.[0]?.nombre || a.asignaturaOrigen || "").trim();
    setNuevas((n) => [...n, { ...PROPUESTA_VACIA, asignaturaOrigen: origen }]);
  };

  const cambiarNueva = (i, campo, valor) =>
    setNuevas((n) => n.map((p, j) => (j === i ? { ...p, [campo]: valor } : p)));

  // Aceptar la sugerencia de la IA no exige texto: se registra "Acepta la sugerencia de la IA."
  const evaluaciones = () =>
    filasIA.map((a) => ({
      asignaturaId: a.id,
      homologable: Boolean(decisiones[a.id]?.homologable),
      justificacion: (decisiones[a.id]?.justificacion || "").trim() || JUSTIFICACION_ACEPTA,
    }));

  const firmaPayload = () => ({
    nombreDocente: firma.nombre.trim(),
    docenteCedula: firma.cedula.trim(),
    docenteCorreoInforme: firma.correo.trim(),
  });

  const faltantes = () => {
    if (datos?.requiereAnalisis) return "Primero genere el análisis con la IA (paso 2).";
    const sinDecidir = filasIA.filter((a) => decisiones[a.id]?.homologable == null);
    if (sinDecidir.length)
      return `Decida si se homologa: ${sinDecidir.map((a) => `${a.asignaturaOrigen} → ${a.asignaturaDestino}`).join(", ")}.`;
    const sinJustificar = filasIA.filter(
      (a) => requiereJustificacion(a, decisiones[a.id]) && !(decisiones[a.id]?.justificacion || "").trim()
    );
    if (sinJustificar.length)
      return `Justifique su decisión en: ${sinJustificar.map((a) => `${a.asignaturaOrigen} → ${a.asignaturaDestino}`).join(", ")}.`;
    const incompleta = nuevas.find((p) => !p.asignaturaOrigen || !p.asignaturaDestinoId || !p.justificacion.trim());
    if (incompleta) return "Complete la materia de origen, el destino y la justificación de cada propuesta nueva.";
    if (!firma.nombre.trim() || !firma.cedula.trim()) return "Ingrese su nombre y su cédula para firmar el informe.";
    return "";
  };

  const ejecutar = async (clave, accion, exito) => {
    setOcupado(clave);
    setAviso(null);
    try {
      await accion();
      if (exito) setAviso({ tipo: "success", texto: exito });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo completar la acción." });
    } finally {
      setOcupado("");
    }
  };

  const vistaPrevia = () => {
    if (!firma.nombre.trim() || !firma.cedula.trim()) {
      setAviso({ tipo: "danger", texto: "Ingrese su nombre y su cédula para generar el informe." });
      return;
    }
    return ejecutar("previa", async () => {
      const r = await generarSugerenciaDocente(id, {
        ...firmaPayload(),
        asignaturas: evaluaciones(),
        observacionGeneral: observacion.trim(),
        reutilizarOpinion: Boolean(datos?.sugerencia),
      });
      setInforme(r?.informeTexto || "");
      setInformeEditado(false);
    }, "Vista previa generada. Puede editar el texto antes de enviarlo.");
  };

  const enviar = () => {
    const error = faltantes();
    if (error) {
      setAviso({ tipo: "danger", texto: error });
      return;
    }
    return ejecutar("enviar", async () => {
      await evaluarSolicitud(id, {
        ...firmaPayload(),
        informeTexto: informeEditado ? informe : undefined,
        asignaturas: evaluaciones(),
        nuevasPropuestas: nuevas.map((p) => ({
          asignaturaOrigen: p.asignaturaOrigen,
          asignaturaDestinoId: Number(p.asignaturaDestinoId),
          homologable: p.homologable,
          justificacion: p.justificacion.trim(),
        })),
        observacionGeneral: observacion.trim(),
        estadoRevision: "REVISADO",
      });
      setNuevas([]);
      aplicarDatos(await obtenerDetalleDocente(id));
    }, "Su informe técnico fue enviado al Coordinador.");
  };

  const analizar = () => {
    if (!datos?.requiereAnalisis && !window.confirm("Se volverá a generar el análisis y se perderán las decisiones que no haya enviado. ¿Continuar?"))
      return;
    return ejecutar("analizar", async () => {
      aplicarDatos(await analizarSolicitudDocente(id));
      setNuevas([]);
    }, "Análisis de la IA generado. Revise cada sugerencia.");
  };

  const pedirDocumentacion = () =>
    ejecutar("documentacion", async () => {
      const d = await solicitarDocumentacion(id, pedido.trim());
      setDatos((prev) => ({ ...prev, delegacion: { ...prev.delegacion, ...d, estado: prev.delegacion.estado } }));
      setPedido("");
    }, "El pedido de documentación quedó registrado para el Coordinador.");

  if (cargando) {
    return (
      <div className="container d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
        <div className="spinner-border spinner-border-sm text-primary" role="status" />
        Cargando solicitud…
      </div>
    );
  }

  const homologadas = filasIA.filter((a) => decisiones[a.id]?.homologable).length + nuevas.filter((p) => p.homologable).length;
  const pendientes = filasIA.filter((a) => decisiones[a.id]?.homologable == null).length;

  return (
    <div className="container pb-5">
      <div className="d-flex flex-wrap justify-content-between align-items-end gap-3 mb-4">
        <div>
          <p className="text-muted small text-uppercase fw-semibold mb-1 dd-sobretitulo">Revisión docente · {numero}</p>
          <h1 className="h3 fw-bold mb-1">Evaluación técnica de la solicitud</h1>
          <p className="text-muted mb-0">
            Revise cada sugerencia de la IA, decida y justifique. Su informe se envía al Coordinador.
          </p>
        </div>
        <Link to="/solicitudes-docente" className="btn btn-outline-secondary">← Mis solicitudes</Link>
      </div>

      {!enviada && detalle && (
        <div className="dd-guia mb-3" aria-label="Cómo revisar">
          <span><b>1</b>Revise los datos y el récord del estudiante</span>
          <span><b>2</b>{datos?.requiereAnalisis ? "Analice con la IA las materias aprobadas" : `Confirme o cambie lo que sugiere la IA${pendientes > 0 ? ` (${pendientes} sin decidir)` : ""}`}</span>
          <span><b>3</b>Si falta algo, agregue equivalencias o pida documentos</span>
          <span><b>4</b>Firme y envíe su informe al Coordinador · homologa {homologadas}</span>
        </div>
      )}
      {aviso && <div className={`alert alert-${aviso.tipo} py-2`}>{aviso.texto}</div>}
      {enviada && (
        <div className="alert alert-success py-2">
          {estado === "REVISADO"
            ? "Usted ya envió su informe técnico. Puede consultarlo y descargarlo."
            : "Otro docente asignado ya envió el informe técnico de esta solicitud; solo puede consultarlo."}
        </div>
      )}

      {detalle && (
        <div className="card border-0 shadow-sm">
          <div className="card-body p-4">
            {/* 1. Solicitud */}
            <Paso numero={1} titulo="Datos de la solicitud" hecho>
              <div className="row g-3">
                <div className="col-md-6">
                  <div className="dd-caja h-100">
                    <div className="small text-muted">Estudiante</div>
                    <div className="fw-semibold">{detalle.estudiante || "—"}</div>
                    <div className="small">C.I. {detalle.estudianteCedula || "—"}</div>
                    <div className="small mt-2">
                      <span className={`badge ${esExterno(detalle) ? "bg-info text-dark" : "bg-primary"} me-1`}>
                        {esExterno(detalle) ? "Agente externo" : "Agente interno"}
                      </span>
                      {esExterno(detalle) && detalle.universidadOrigen ? `${detalle.universidadOrigen} → ` : ""}
                      {detalle.carreraOrigen} → {detalle.carreraDestino}
                    </div>
                    <div className="small text-muted mt-1">Coordinador: {delegacion?.coordinadorCorreo || "—"}</div>
                  </div>
                </div>
                <div className="col-md-6">
                  <div className="dd-caja h-100">
                    <div className="d-flex justify-content-between">
                      <span className="small text-muted">Récord académico · materias aprobadas</span>
                      <Link to="/carreras" className="small" target="_blank" rel="noreferrer">Mallas y sílabos ↗</Link>
                    </div>
                    {(datos.materiasAprobadas || []).length === 0 ? (
                      <div className="small text-muted">No hay materias aprobadas registradas.</div>
                    ) : (
                      <ul className="small mb-0 ps-3 sg-lista">
                        {datos.materiasAprobadas.map((m) => (
                          <li key={m.nombre}>
                            {m.nombre}
                            {m.nota != null ? ` · ${m.nota}` : ""}
                            {m.periodo ? <span className="text-muted"> · {m.periodo}</span> : null}
                            {!estaAprobada(m) && <span className="badge bg-danger-subtle text-danger-emphasis ms-1">{m.estadoSegunDocumento}</span>}
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                </div>
              </div>
            </Paso>

            {/* 2. Sugerencias de la IA */}
            <Paso
              numero={2}
              titulo={datos.requiereAnalisis ? "Analice con la IA las materias aprobadas" : `Decida sobre las sugerencias de la IA (${filasIA.length})`}
              hecho={enviada || (filasIA.length > 0 && pendientes === 0)}
            >
              {datos.requiereAnalisis ? (
                <div className="dd-caja text-center py-4">
                  <p className="mb-1 fw-semibold">Todavía no se ha analizado esta solicitud.</p>
                  <p className="small text-muted mb-3">
                    La IA tomará las {(datos.materiasAprobadas || []).filter(estaAprobada).length} materias aprobadas del récord, las
                    comparará con la malla y los sílabos de {detalle.carreraDestino}
                    {esExterno(detalle) ? " usando criterios generales" : " aplicando las reglas de FISEI (80 % de contenido, profundidad y horas; máximo 10 años)"} y
                    le sugerirá qué homologar. Usted decide.
                  </p>
                  {!bloqueado && (
                    <button type="button" className="btn btn-primary fw-semibold px-4" onClick={analizar} disabled={ocupado === "analizar"}>
                      {ocupado === "analizar" ? "La IA está analizando…" : "Analizar con IA"}
                    </button>
                  )}
                </div>
              ) : (
                <>
                  <div className="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3">
                    <p className="small text-muted mb-0 flex-grow-1" style={{ maxWidth: 720 }}>
                      Ya viene marcada la recomendación de la IA: si está de acuerdo no tiene que hacer nada. Cambie la decisión
                      solo donde no coincida (se le pedirá la justificación) y decida las que «requieren su decisión». La
                      sugerencia original de la IA se conserva aparte.
                    </p>
                    {!bloqueado && (
                      <button type="button" className="btn btn-sm btn-outline-secondary" onClick={analizar} disabled={ocupado === "analizar"}>
                        {ocupado === "analizar" ? "Analizando…" : "Volver a analizar"}
                      </button>
                    )}
                  </div>
                  <DecisionesDocente
                    filas={filasIA}
                    decisiones={decisiones}
                    onCambiar={cambiarDecision}
                    bloqueado={bloqueado}
                    onModificarDestino={(datos.mallaDestino || []).length > 0 ? modificarDestino : null}
                  />
                </>
              )}
            </Paso>

            {/* 3. Propuestas del docente */}
            <Paso numero={3} titulo="Propuestas propias (opcional)" hecho={filasDocente.length > 0}>
              {filasDocente.map((a) => (
                <div key={a.id} className="mb-2">
                  <TarjetaSugerencia a={a} compacta />
                </div>
              ))}
              {!bloqueado && (
                <>
                  <p className="small text-muted">
                    Agregue equivalencias que la IA no identificó. Solo se pueden proponer materias aprobadas del récord.
                  </p>
                  {nuevas.map((p, i) => (
                    <div key={i} className="dd-caja mb-2">
                      <div className="row g-2 align-items-end">
                        <div className="col-md-4">
                          <label className="form-label small mb-1">Materia aprobada (origen)</label>
                          <select
                            className="form-select form-select-sm"
                            value={p.asignaturaOrigen}
                            onChange={(e) => cambiarNueva(i, "asignaturaOrigen", e.target.value)}
                          >
                            <option value="">Elija…</option>
                            {(datos.materiasAprobadas || []).filter(estaAprobada).map((m) => (
                              <option key={m.nombre} value={m.nombre}>{m.nombre}</option>
                            ))}
                          </select>
                        </div>
                        <div className="col-md-4">
                          <label className="form-label small mb-1">Asignatura de destino</label>
                          <select
                            className="form-select form-select-sm"
                            value={p.asignaturaDestinoId}
                            onChange={(e) => cambiarNueva(i, "asignaturaDestinoId", e.target.value)}
                          >
                            <option value="">Elija…</option>
                            {(datos.mallaDestino || []).map((m) => (
                              <option key={m.asignaturaId} value={m.asignaturaId}>
                                {m.nombre} · {m.nivel}
                              </option>
                            ))}
                          </select>
                        </div>
                        <div className="col-md-3">
                          <select
                            className="form-select form-select-sm"
                            value={p.homologable ? "SI" : "NO"}
                            onChange={(e) => cambiarNueva(i, "homologable", e.target.value === "SI")}
                            aria-label="Decisión de la propuesta"
                          >
                            <option value="SI">Homologar</option>
                            <option value="NO">No homologar</option>
                          </select>
                        </div>
                        <div className="col-md-1 text-end">
                          <button
                            type="button"
                            className="btn btn-sm btn-outline-danger"
                            onClick={() => setNuevas((n) => n.filter((_, j) => j !== i))}
                            aria-label="Quitar propuesta"
                          >
                            ✕
                          </button>
                        </div>
                        <div className="col-12">
                          <textarea
                            className="form-control form-control-sm"
                            rows={2}
                            placeholder="Justificación (obligatoria)"
                            value={p.justificacion}
                            onChange={(e) => cambiarNueva(i, "justificacion", e.target.value)}
                          />
                        </div>
                      </div>
                    </div>
                  ))}
                  <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => setNuevas((n) => [...n, { ...PROPUESTA_VACIA }])}>
                    + Agregar propuesta
                  </button>
                </>
              )}
              {bloqueado && filasDocente.length === 0 && <p className="small text-muted mb-0">No se agregaron propuestas.</p>}
            </Paso>

            {/* 4. Documentación adicional */}
            <Paso numero={4} titulo="Documentación adicional (opcional)" hecho={Boolean(delegacion?.documentacionSolicitada)}>
              {delegacion?.documentacionSolicitada && (
                <div className="alert alert-info py-2 small">
                  <strong>Pedido registrado:</strong> {delegacion.documentacionSolicitada}
                </div>
              )}
              {!bloqueado && (
                <div className="d-flex flex-wrap gap-2">
                  <input
                    className="form-control form-control-sm flex-grow-1"
                    style={{ minWidth: 260 }}
                    placeholder="Ej.: sílabo de Física II de la universidad de origen"
                    value={pedido}
                    onChange={(e) => setPedido(e.target.value)}
                    aria-label="Documentación que necesita"
                  />
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary"
                    onClick={pedirDocumentacion}
                    disabled={!pedido.trim() || ocupado === "documentacion"}
                  >
                    Solicitar al Coordinador
                  </button>
                </div>
              )}
            </Paso>

            {/* 5. Informe técnico */}
            <Paso numero={5} titulo="Informe técnico docente" hecho={enviada}>
              <div className="row g-2 mb-3">
                {[
                  ["nombre", "Nombre completo", "Ej.: Ing. María Pérez"],
                  ["cedula", "Cédula", "Ej.: 1800000001"],
                  ["correo", "Correo para el informe", "docente@uta.edu.ec"],
                ].map(([campo, etiqueta, ayuda]) => (
                  <div className="col-md-4" key={campo}>
                    <label htmlFor={`firma-${campo}`} className="form-label small mb-1">{etiqueta}</label>
                    <input
                      id={`firma-${campo}`}
                      className="form-control form-control-sm"
                      placeholder={ayuda}
                      value={firma[campo]}
                      onChange={(e) => setFirma((f) => ({ ...f, [campo]: e.target.value }))}
                      disabled={bloqueado}
                    />
                  </div>
                ))}
              </div>

              <label htmlFor="observacion" className="form-label small fw-semibold mb-1">Observación general</label>
              <textarea
                id="observacion"
                className="form-control mb-3"
                rows={2}
                value={observacion}
                onChange={(e) => setObservacion(e.target.value)}
                disabled={bloqueado}
              />

              {informe && (
                <details className="mb-3" open={!enviada}>
                  <summary className="small fw-semibold">Texto del informe {enviada ? "enviado" : "(editable)"}</summary>
                  {enviada ? (
                    <pre className="mt-2 mb-0 small" style={{ whiteSpace: "pre-wrap" }}>{informe}</pre>
                  ) : (
                    <textarea
                      className="form-control form-control-sm mt-2"
                      rows={14}
                      value={informe}
                      onChange={(e) => {
                        setInforme(e.target.value);
                        setInformeEditado(true);
                      }}
                      aria-label="Texto del informe técnico"
                    />
                  )}
                </details>
              )}

              {!bloqueado && (
                <div className="form-check mb-3">
                  <input
                    id="confirmar"
                    type="checkbox"
                    className="form-check-input"
                    checked={confirmado}
                    onChange={(e) => setConfirmado(e.target.checked)}
                  />
                  <label htmlFor="confirmar" className="form-check-label small">
                    Revisé cada sugerencia y justifiqué mis decisiones. Recomiendo homologar <strong>{homologadas}</strong> asignatura(s).
                  </label>
                </div>
              )}

              <div className="d-flex flex-wrap gap-2">
                {!bloqueado && (
                  <>
                    <button type="button" className="btn btn-outline-primary" onClick={vistaPrevia} disabled={Boolean(ocupado)}>
                      {ocupado === "previa" ? "Generando…" : informe ? "Actualizar vista previa" : "Generar vista previa del informe"}
                    </button>
                    <button type="button" className="btn btn-success fw-semibold" onClick={enviar} disabled={!confirmado || Boolean(ocupado)}>
                      {ocupado === "enviar" ? "Enviando…" : "Enviar informe al Coordinador"}
                    </button>
                  </>
                )}
                <button
                  type="button"
                  className="btn btn-outline-secondary"
                  onClick={() => ejecutar("pdf", () => descargarInformeDocente(id), "Informe descargado.")}
                  disabled={!informe || Boolean(ocupado)}
                >
                  ⬇ Descargar PDF
                </button>
              </div>
            </Paso>
          </div>
        </div>
      )}
    </div>
  );
}
