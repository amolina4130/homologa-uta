import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import {
  descargarInformeFinal,
  emitirInformeFinal,
  obtenerConsolidado,
  previsualizarInformeFinal,
} from "../services/coordinadorService";
import { useSolicitudesRevision } from "../components/revision/useSolicitudesRevision";
import {
  CabeceraSolicitud,
  EncabezadoPaso,
  EstadoCarga,
  ListaRevision,
  Paso,
} from "../components/revision/PiezasRevision";
import {
  CEDULA_SIN_IDENTIFICAR,
  NOMBRE_SIN_IDENTIFICAR,
  coincideBusqueda,
  datoReal,
  etapaDe,
  fechaCorta,
  numeroDe,
  resultadoHomologacion,
} from "../components/revision/etapas";
import "./InformesSugeridos.css";

const HOMOLOGABLE = "HOMOLOGABLE";
const NO_HOMOLOGABLE = "NO_HOMOLOGABLE";

const FILTROS = [
  { valor: "con", etiqueta: "Por emitir" },
  { valor: "emitido", etiqueta: "Enviados" },
];

const nombreOrigen = (a) =>
  a.combinada && a.materiasOrigen?.length ? a.materiasOrigen.map((m) => m.nombre).join(" + ") : a.asignaturaOrigen;

// Decisiones iniciales: si ya hay informe, las del Coordinador; si no, las que sugirió el docente.
const decisionesIniciales = (consolidado) => {
  const docente = Object.fromEntries(
    (consolidado?.evaluacionesConsolidadas || []).map((e) => [e.asignaturaId, e])
  );
  return Object.fromEntries(
    (consolidado?.detalle?.asignaturas || []).map((a) => {
      const sug = docente[a.id];
      const decision =
        a.decisionCoordinador === HOMOLOGABLE || a.decisionCoordinador === NO_HOMOLOGABLE
          ? a.decisionCoordinador
          : sug
            ? sug.homologable
              ? HOMOLOGABLE
              : NO_HOMOLOGABLE
            : "";
      return [a.id, { decision, observacion: a.observacionCoordinador || sug?.justificacion || "" }];
    })
  );
};

export default function InformeFinal() {
  const { usuario } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  // Incluye las ya emitidas para poder consultarlas, descargarlas o corregirlas.
  const rev = useSolicitudesRevision({ incluirEmitidos: true });
  const { delegacionesPorId } = rev;

  const [seleccionId, setSeleccionId] = useState(searchParams.get("solicitudId") || "");
  const [filtro, setFiltro] = useState("con");
  const [texto, setTexto] = useState("");
  const [aviso, setAviso] = useState(null);

  const [consolidado, setConsolidado] = useState(null);
  const [cargandoConsolidado, setCargandoConsolidado] = useState(false);
  const [datos, setDatos] = useState({ nombre: "", cedula: "", memorando: "", firmante: "" });
  const [decisiones, setDecisiones] = useState({});
  const [borrador, setBorrador] = useState("");
  const [editando, setEditando] = useState(false);
  const [previsualizando, setPrevisualizando] = useState(false);
  const [emitiendo, setEmitiendo] = useState(false);
  const [descargando, setDescargando] = useState(false);

  // Esta etapa muestra las que ya tienen sugerencia del docente y las que ya tienen informe.
  const enEtapa = useMemo(
    () =>
      rev.pendientes.filter((s) => {
        const clave = etapaDe(s, delegacionesPorId[s.solicitudId]).clave;
        return clave === "con" || clave === "emitido";
      }),
    [rev.pendientes, delegacionesPorId]
  );

  const pedirConsolidado = useCallback((id) => {
    setConsolidado(null);
    setCargandoConsolidado(true);
    return obtenerConsolidado(id)
      .then((c) => {
        setConsolidado(c || null);
        const d = c?.detalle || {};
        setDatos({
          nombre: datoReal(d.estudiante, NOMBRE_SIN_IDENTIFICAR),
          cedula: datoReal(d.estudianteCedula, CEDULA_SIN_IDENTIFICAR),
          memorando: "",
          firmante: "",
        });
        setDecisiones(decisionesIniciales(c));
        setBorrador("");
        setEditando(!c?.informeFinalTexto);
      })
      .catch((e) => setAviso({ tipo: "danger", texto: e.message || "No se pudo cargar la solicitud." }))
      .finally(() => setCargandoConsolidado(false));
  }, []);

  const abrir = (resultado, idPreferido) => {
    if (!resultado) return;
    const { lista, mapa } = resultado;
    const candidatas = lista.filter((s) => ["con", "emitido"].includes(etapaDe(s, mapa[s.solicitudId]).clave));
    const elegida =
      candidatas.find((s) => String(s.solicitudId) === String(idPreferido)) ||
      candidatas.find((s) => etapaDe(s, mapa[s.solicitudId]).clave === "con") ||
      candidatas[0];
    if (elegida) setFiltro(etapaDe(elegida, mapa[elegida.solicitudId]).clave);
    const id = elegida ? String(elegida.solicitudId) : "";
    setSeleccionId(id);
    if (id) pedirConsolidado(id);
  };

  useEffect(() => {
    rev.cargar().then((r) => abrir(r, searchParams.get("solicitudId")));
    // Solo al entrar: luego la selección se maneja en el estado.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const recargar = () => {
    setAviso(null);
    rev.recargar().then((r) => abrir(r, seleccionId));
  };

  const seleccionar = (s) => {
    const id = String(s.solicitudId);
    setSeleccionId(id);
    setSearchParams({ solicitudId: id }, { replace: true });
    setAviso(null);
    pedirConsolidado(id);
  };

  const conteo = useMemo(() => {
    const c = { con: 0, emitido: 0 };
    for (const s of enEtapa) c[etapaDe(s, delegacionesPorId[s.solicitudId]).clave]++;
    return c;
  }, [enEtapa, delegacionesPorId]);

  const visibles = enEtapa.filter(
    (s) => etapaDe(s, delegacionesPorId[s.solicitudId]).clave === filtro && coincideBusqueda(s, texto)
  );

  const activa = enEtapa.find((s) => String(s.solicitudId) === String(seleccionId));
  const asignaturas = consolidado?.detalle?.asignaturas || [];
  const sugerenciaDocente = Object.fromEntries(
    (consolidado?.evaluacionesConsolidadas || []).map((e) => [e.asignaturaId, e.homologable])
  );
  const justificacionDocente = Object.fromEntries(
    (consolidado?.evaluacionesConsolidadas || []).map((e) => [e.asignaturaId, (e.justificacion || "").trim()])
  );
  const contenidoDe = Object.fromEntries(
    (consolidado?.informeSugeridoAsignaturas || []).map((f) => [f.asignaturaId, f.contenido])
  );
  const homologables = asignaturas.filter((a) => decisiones[a.id]?.decision === HOMOLOGABLE).length;
  const resultado = resultadoHomologacion(homologables, asignaturas.length);
  const emitido = Boolean(consolidado?.informeFinalTexto);
  const cambios = asignaturas.filter(
    (a) => a.id in sugerenciaDocente && (decisiones[a.id]?.decision === HOMOLOGABLE) !== sugerenciaDocente[a.id]
  ).length;

  const cambiarDecision = (id, campo, valor) =>
    setDecisiones((prev) => ({ ...prev, [id]: { ...prev[id], [campo]: valor } }));

  // Lo que falta para poder emitir, o "" si está completo.
  const errorValidacion = () => {
    if (!datos.nombre.trim()) return "Ingrese el nombre del titular.";
    if (!/^\d{10}$/.test(datos.cedula.trim())) return "La cédula del titular debe tener 10 dígitos.";
    const faltan = asignaturas.filter(
      (a) => ![HOMOLOGABLE, NO_HOMOLOGABLE].includes(decisiones[a.id]?.decision) || !decisiones[a.id]?.observacion?.trim()
    ).length;
    if (faltan > 0) return `Falta la decisión o la observación de ${faltan} asignatura${faltan === 1 ? "" : "s"}.`;
    // Trazabilidad: si cambia la decisión del docente, la justificación debe ser la suya propia.
    const sinJustificarCambio = asignaturas.filter(
      (a) =>
        a.id in sugerenciaDocente &&
        (decisiones[a.id]?.decision === HOMOLOGABLE) !== sugerenciaDocente[a.id] &&
        (decisiones[a.id]?.observacion || "").trim().toLowerCase() === (justificacionDocente[a.id] || "").toLowerCase()
    );
    if (sinJustificarCambio.length > 0)
      return `Cambió la decisión del docente en ${sinJustificarCambio.map((a) => a.asignaturaDestino).join(", ")}: escriba su propia justificación del cambio.`;
    return "";
  };

  // Arma el cuerpo que espera el backend; devuelve null (y muestra el motivo) si falta algo.
  const armarPayload = () => {
    const error = errorValidacion();
    if (error) {
      setAviso({ tipo: "danger", texto: error });
      return null;
    }
    return {
      asignaturas: asignaturas.map((a) => ({
        asignaturaId: a.id,
        decision: decisiones[a.id].decision,
        observacion: decisiones[a.id].observacion.trim(),
      })),
      nombreEstudiante: datos.nombre.trim(),
      cedulaEstudiante: datos.cedula.trim(),
      numeroMemorando: datos.memorando.trim(),
      nombreFirmante: datos.firmante.trim(),
    };
  };

  const previsualizar = async () => {
    setAviso(null);
    const payload = armarPayload();
    if (!payload) return;
    setPrevisualizando(true);
    try {
      const r = await previsualizarInformeFinal(activa.solicitudId, payload);
      setBorrador(r?.texto || r?.informeFinalTexto || "");
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo generar la vista previa." });
    } finally {
      setPrevisualizando(false);
    }
  };

  const emitir = async () => {
    setAviso(null);
    const payload = armarPayload();
    if (!payload) return;
    if (
      !window.confirm(
        `¿Emitir el informe final de ${numeroDe(activa)} y enviarlo al Comité? Quedan ${homologables} de ${asignaturas.length} asignaturas homologables y el trámite se cierra.`
      )
    )
      return;
    setEmitiendo(true);
    try {
      await emitirInformeFinal(activa.solicitudId, { ...payload, informeFinalTexto: borrador.trim() || undefined });
      rev.setPendientes((prev) =>
        prev.map((s) =>
          s.solicitudId === activa.solicitudId
            ? { ...s, estado: "INFORME_EMITIDO", estudiante: payload.nombreEstudiante, estudianteCedula: payload.cedulaEstudiante }
            : s
        )
      );
      setFiltro("emitido");
      await pedirConsolidado(activa.solicitudId);
      setAviso({ tipo: "success", texto: `Informe final de ${numeroDe(activa)} enviado al Comité. Trámite finalizado.` });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo emitir el informe final." });
    } finally {
      setEmitiendo(false);
    }
  };

  const descargar = async () => {
    setDescargando(true);
    setAviso(null);
    try {
      await descargarInformeFinal(activa.solicitudId);
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo descargar el informe final." });
    } finally {
      setDescargando(false);
    }
  };

  return (
    <div className="container pb-5">
      <EncabezadoPaso
        paso={3}
        titulo="Informe final"
        descripcion="Confirme la decisión de cada asignatura y emita el informe. Al enviarlo al Comité, el trámite termina."
        anterior={{ ruta: "/informes-sugeridos", texto: "Paso 2" }}
        onActualizar={recargar}
        cargando={rev.cargando}
      />

      <EstadoCarga
        cargando={rev.cargando && rev.pendientes.length === 0}
        error={rev.error}
        onReintentar={recargar}
        vacio={
          enEtapa.length === 0 && {
            titulo: "No hay informes por emitir",
            texto: "Cuando un docente envíe su sugerencia, la solicitud aparecerá aquí.",
          }
        }
      >
        <div className="row g-4">
          <div className="col-lg-4">
            <ListaRevision
              filtros={FILTROS}
              conteo={conteo}
              filtro={filtro}
              onFiltro={setFiltro}
              texto={texto}
              onTexto={setTexto}
              items={visibles}
              delegacionesPorId={delegacionesPorId}
              seleccionId={seleccionId}
              onSeleccionar={seleccionar}
              textoVacio={filtro === "con" ? "No hay informes pendientes de emitir." : "Aún no se ha enviado ningún informe."}
            />
          </div>

          <div className="col-lg-8">
            {!activa ? (
              <div className="card border-0 shadow-sm">
                <div className="card-body text-center text-muted py-5">Elija una solicitud de la lista.</div>
              </div>
            ) : (
              <div className="card border-0 shadow-sm">
                <div className="card-body p-4">
                  <CabeceraSolicitud solicitud={activa} />
                  {aviso && <div className={`alert alert-${aviso.tipo} py-2`}>{aviso.texto}</div>}

                  {cargandoConsolidado || !consolidado ? (
                    <div className="d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
                      <div className="spinner-border spinner-border-sm text-primary" role="status" />
                      Cargando…
                    </div>
                  ) : emitido && !editando ? (
                    /* Informe ya emitido */
                    <>
                      <div className="is-estado mb-3">
                        <div className="d-flex flex-wrap justify-content-between align-items-start gap-3">
                          <div>
                            <div className="fw-bold text-success mb-1">✓ Informe enviado al Comité · Trámite finalizado</div>
                            <div className="small text-muted">
                              {consolidado.detalle?.fechaInforme ? `El ${fechaCorta(consolidado.detalle.fechaInforme)}` : ""}
                              {consolidado.detalle?.coordinadorCorreo ? ` · por ${consolidado.detalle.coordinadorCorreo}` : ""}
                            </div>
                          </div>
                          <span className={`badge rounded-pill fs-6 ${resultado.clase}`}>{resultado.texto}</span>
                        </div>
                        <div className="small mt-2">
                          <strong>{homologables}</strong> de {asignaturas.length} asignaturas se homologan. El Comité
                          puede descargar el informe desde sus solicitudes.
                        </div>
                      </div>
                      <details className="is-memorando mb-4">
                        <summary className="fw-semibold">Ver texto del informe final</summary>
                        <pre className="mt-2 mb-0">{consolidado.informeFinalTexto}</pre>
                      </details>
                      <div className="is-siguiente d-flex flex-wrap justify-content-between align-items-center gap-2">
                        <button type="button" className="btn btn-link px-0" onClick={() => setEditando(true)}>
                          Corregir y volver a emitir
                        </button>
                        <button type="button" className="btn btn-primary" onClick={descargar} disabled={descargando}>
                          {descargando ? "Descargando…" : "⬇ Descargar PDF"}
                        </button>
                      </div>
                    </>
                  ) : (
                    /* Edición del informe */
                    <>
                      {emitido && (
                        <div className="alert alert-warning py-2 small">
                          Está corrigiendo un informe ya emitido. Al volver a emitirlo se reemplaza el anterior.{" "}
                          <button type="button" className="btn btn-link btn-sm p-0 align-baseline" onClick={() => pedirConsolidado(activa.solicitudId)}>
                            Cancelar
                          </button>
                        </div>
                      )}

                      <Paso numero={1} titulo="Datos del informe" hecho={false}>
                        <div className="row g-2">
                          <div className="col-sm-7">
                            <label htmlFor="ifNombre" className="form-label small mb-1">Titular (según récord)</label>
                            <input
                              id="ifNombre"
                              className="form-control"
                              value={datos.nombre}
                              onChange={(e) => setDatos((d) => ({ ...d, nombre: e.target.value }))}
                            />
                          </div>
                          <div className="col-sm-5">
                            <label htmlFor="ifCedula" className="form-label small mb-1">Cédula</label>
                            <input
                              id="ifCedula"
                              className="form-control"
                              inputMode="numeric"
                              maxLength={10}
                              value={datos.cedula}
                              onChange={(e) => setDatos((d) => ({ ...d, cedula: e.target.value.replace(/\D/g, "") }))}
                            />
                          </div>
                          <div className="col-sm-5">
                            <label htmlFor="ifMemorando" className="form-label small mb-1">
                              N.º de memorando <span className="text-muted">(opcional)</span>
                            </label>
                            <input
                              id="ifMemorando"
                              className="form-control"
                              value={datos.memorando}
                              placeholder={numeroDe(activa)}
                              onChange={(e) => setDatos((d) => ({ ...d, memorando: e.target.value }))}
                            />
                          </div>
                          <div className="col-sm-7">
                            <label htmlFor="ifFirmante" className="form-label small mb-1">
                              Firmante <span className="text-muted">(opcional)</span>
                            </label>
                            <input
                              id="ifFirmante"
                              className="form-control"
                              value={datos.firmante}
                              placeholder={usuario?.nombre || usuario?.correo || "Coordinador en sesión"}
                              onChange={(e) => setDatos((d) => ({ ...d, firmante: e.target.value }))}
                            />
                          </div>
                        </div>
                      </Paso>

                      <Paso numero={2} titulo="Decisión final por asignatura" hecho={false}>
                        <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2 small">
                          <span>
                            <strong>{homologables}</strong> de {asignaturas.length} se homologan{" "}
                            <span className={`badge rounded-pill ms-1 ${resultado.clase}`}>{resultado.texto}</span>
                          </span>
                          {cambios > 0 && (
                            <span className="text-warning-emphasis">
                              Cambió {cambios} decisión{cambios === 1 ? "" : "es"} respecto al docente
                            </span>
                          )}
                        </div>
                        <div className="table-responsive">
                          <table className="table align-middle mb-0 is-tabla">
                            <thead>
                              <tr>
                                <th>Asignatura</th>
                                <th className="text-center">IA</th>
                                <th className="text-center">Docente</th>
                                <th className="text-center">Decisión final</th>
                                <th>Observación *</th>
                              </tr>
                            </thead>
                            <tbody>
                              {asignaturas.map((a) => {
                                const d = decisiones[a.id] || {};
                                const sug = sugerenciaDocente[a.id];
                                const distinta = sug !== undefined && (d.decision === HOMOLOGABLE) !== sug;
                                return (
                                  <tr key={a.id} className={distinta ? "table-warning" : ""}>
                                    <td>
                                      <div className="fw-semibold">{nombreOrigen(a)}</div>
                                      <div className="small text-muted">
                                        → {a.asignaturaDestino}
                                        {contenidoDe[a.id] ? ` · ${contenidoDe[a.id]}` : ""}
                                      </div>
                                    </td>
                                    <td className="text-center">
                                      <span
                                        className={`badge rounded-pill ${
                                          a.propuestaPor === "DOCENTE"
                                            ? "bg-secondary-subtle text-secondary-emphasis"
                                            : a.propuesta?.decision === "HOMOLOGABLE"
                                              ? "bg-success-subtle text-success-emphasis"
                                              : a.propuesta?.decision === "REQUIERE_REVISION"
                                                ? "bg-warning-subtle text-warning-emphasis"
                                                : "bg-danger-subtle text-danger-emphasis"
                                        }`}
                                        title={a.reglaAplicada || ""}
                                      >
                                        {a.propuestaPor === "DOCENTE"
                                          ? "Docente"
                                          : a.propuesta?.decision === "HOMOLOGABLE"
                                            ? "Sí"
                                            : a.propuesta?.decision === "REQUIERE_REVISION"
                                              ? "Revisar"
                                              : "No"}
                                      </span>
                                    </td>
                                    <td className="text-center">
                                      {sug === undefined ? (
                                        <span className="text-muted">-</span>
                                      ) : (
                                        <span
                                          className={`badge rounded-pill ${
                                            sug ? "bg-success-subtle text-success-emphasis" : "bg-danger-subtle text-danger-emphasis"
                                          }`}
                                        >
                                          {sug ? "Sí" : "No"}
                                        </span>
                                      )}
                                    </td>
                                    <td className="text-center">
                                      <div className="btn-group btn-group-sm" role="group" aria-label={`Decisión para ${a.asignaturaDestino}`}>
                                        <button
                                          type="button"
                                          className={`btn ${d.decision === HOMOLOGABLE ? "btn-success" : "btn-outline-success"}`}
                                          onClick={() => cambiarDecision(a.id, "decision", HOMOLOGABLE)}
                                        >
                                          Homologa
                                        </button>
                                        <button
                                          type="button"
                                          className={`btn ${d.decision === NO_HOMOLOGABLE ? "btn-danger" : "btn-outline-danger"}`}
                                          onClick={() => cambiarDecision(a.id, "decision", NO_HOMOLOGABLE)}
                                        >
                                          No
                                        </button>
                                      </div>
                                    </td>
                                    <td style={{ minWidth: 220 }}>
                                      <input
                                        className={`form-control form-control-sm ${d.observacion?.trim() ? "" : "is-invalid"}`}
                                        value={d.observacion || ""}
                                        maxLength={1000}
                                        onChange={(e) => cambiarDecision(a.id, "observacion", e.target.value)}
                                        aria-label={`Observación para ${a.asignaturaDestino}`}
                                      />
                                    </td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                        </div>
                        <p className="small text-muted mt-2 mb-0">
                          Se precargó lo que decidió el docente. Las filas en amarillo difieren de su decisión: justifique el
                          cambio con sus propias palabras (queda registrado). El informe final incluye solo las asignaturas
                          que recomienda homologar.
                        </p>
                      </Paso>

                      <Paso numero={3} titulo="Revisar y emitir" hecho={false}>
                        {borrador ? (
                          <>
                            <label htmlFor="ifBorrador" className="form-label small mb-1">
                              Texto del informe (puede editarlo antes de emitir)
                            </label>
                            <textarea
                              id="ifBorrador"
                              className="form-control mb-2"
                              rows={12}
                              value={borrador}
                              onChange={(e) => setBorrador(e.target.value)}
                            />
                            <button type="button" className="btn btn-link btn-sm px-0" onClick={previsualizar} disabled={previsualizando}>
                              {previsualizando ? "Regenerando…" : "Regenerar con los datos actuales"}
                            </button>
                          </>
                        ) : (
                          <p className="small text-muted">
                            Genere la vista previa para revisar el texto del informe antes de emitirlo. También puede emitirlo
                            directamente con el texto estándar.
                          </p>
                        )}
                        <div className="d-flex flex-wrap justify-content-end gap-2 mt-2">
                          {!borrador && (
                            <button type="button" className="btn btn-outline-primary" onClick={previsualizar} disabled={previsualizando}>
                              {previsualizando ? "Generando…" : "Ver vista previa"}
                            </button>
                          )}
                          <button type="button" className="btn btn-primary fw-semibold" onClick={emitir} disabled={emitiendo}>
                            {emitiendo ? "Enviando…" : emitido ? "Volver a emitir" : "Emitir y enviar al Comité"}
                          </button>
                        </div>
                      </Paso>
                    </>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      </EstadoCarga>
    </div>
  );
}
