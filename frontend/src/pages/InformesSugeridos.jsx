import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { descargarInformeSugeridoDocente, obtenerConsolidado } from "../services/coordinadorService";
import { useSolicitudesRevision } from "../components/revision/useSolicitudesRevision";
import {
  CabeceraSolicitud,
  EncabezadoPaso,
  EstadoCarga,
  ListaRevision,
} from "../components/revision/PiezasRevision";
import {
  coincideBusqueda,
  esRevisada,
  etapaDe,
  resultadoHomologacion,
  fechaCorta,
  tieneInformeFinal,
} from "../components/revision/etapas";
import "./InformesSugeridos.css";

const UMBRAL_CONTENIDO = 80; // RN-03: al menos 80 % de la asignatura receptora

const FILTROS = [
  { valor: "con", etiqueta: "Con sugerencia" },
  { valor: "esperando", etiqueta: "Esperando" },
  { valor: "todas", etiqueta: "Todas" },
];

const FILTROS_TABLA = [
  { valor: "todas", etiqueta: "Todas" },
  { valor: "si", etiqueta: "Se homologan" },
  { valor: "no", etiqueta: "No se homologan" },
];

const seHomologa = (fila) => String(fila.seHomologa || "").trim().toUpperCase().startsWith("S");
const porcentaje = (texto) => {
  const n = parseInt(String(texto || "").replace(/[^\d]/g, ""), 10);
  return Number.isFinite(n) ? n : null;
};

export default function InformesSugeridos() {
  const [searchParams, setSearchParams] = useSearchParams();
  const rev = useSolicitudesRevision();
  const { delegacionesPorId, nombreDocente } = rev;

  const [seleccionId, setSeleccionId] = useState(searchParams.get("solicitudId") || "");
  const [filtro, setFiltro] = useState("todas");
  const [texto, setTexto] = useState("");

  const [consolidado, setConsolidado] = useState(null);
  const [cargandoConsolidado, setCargandoConsolidado] = useState(false);
  const [errorConsolidado, setErrorConsolidado] = useState("");
  const [filtroTabla, setFiltroTabla] = useState("todas");
  const [descargando, setDescargando] = useState(false);
  const [aviso, setAviso] = useState(null);

  // Las que ya tienen informe final pasaron esta etapa.
  const enEtapa = useMemo(() => rev.pendientes.filter((s) => !tieneInformeFinal(s)), [rev.pendientes]);

  const pedirConsolidado = useCallback((id) => {
    setConsolidado(null);
    setCargandoConsolidado(true);
    return obtenerConsolidado(id)
      .then((c) => {
        setConsolidado(c || null);
        setErrorConsolidado("");
      })
      .catch((e) => setErrorConsolidado(e.message || "No se pudo cargar la sugerencia."))
      .finally(() => setCargandoConsolidado(false));
  }, []);

  const abrir = (resultado, idPreferido) => {
    if (!resultado) return;
    const { lista, mapa } = resultado;
    const candidatas = lista.filter((s) => !tieneInformeFinal(s));
    const conSugerencia = (s) => (mapa[s.solicitudId] || []).some(esRevisada);
    const elegida =
      candidatas.find((s) => String(s.solicitudId) === String(idPreferido)) ||
      candidatas.find(conSugerencia) ||
      candidatas[0];
    if (!idPreferido && candidatas.some(conSugerencia)) setFiltro("con");
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
    setFiltroTabla("todas");
    pedirConsolidado(id);
  };

  const descargar = async () => {
    setDescargando(true);
    setAviso(null);
    try {
      await descargarInformeSugeridoDocente(seleccionId);
      setAviso({ tipo: "success", texto: "Informe sugerido descargado en PDF." });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo descargar el informe sugerido." });
    } finally {
      setDescargando(false);
    }
  };

  const conteo = useMemo(() => {
    const c = { con: 0, esperando: 0, todas: enEtapa.length };
    for (const s of enEtapa) {
      const clave = etapaDe(s, delegacionesPorId[s.solicitudId]).clave;
      if (clave in c) c[clave]++;
    }
    return c;
  }, [enEtapa, delegacionesPorId]);

  const visibles = enEtapa.filter((s) => {
    const clave = etapaDe(s, delegacionesPorId[s.solicitudId]).clave;
    if (filtro !== "todas" && clave !== filtro) return false;
    return coincideBusqueda(s, texto);
  });

  const activa = enEtapa.find((s) => String(s.solicitudId) === String(seleccionId));
  const delegaciones = activa ? delegacionesPorId[activa.solicitudId] || [] : [];
  const etapa = activa ? etapaDe(activa, delegacionesPorId[activa.solicitudId]) : null;
  const filas = consolidado?.informeSugeridoAsignaturas || [];
  const homologables = filas.filter(seHomologa).length;
  const filasVisibles = filas.filter((f) =>
    filtroTabla === "si" ? seHomologa(f) : filtroTabla === "no" ? !seHomologa(f) : true
  );
  const revision = delegaciones.find(esRevisada);
  const resultado = resultadoHomologacion(homologables, filas.length);

  return (
    <div className="container pb-5">
      <EncabezadoPaso
        paso={2}
        titulo="Sugerencias de docentes"
        descripcion="Revise lo que propuso el docente para cada asignatura antes de emitir el informe final."
        anterior={{ ruta: "/delegar-docentes", texto: "Paso 1" }}
        onActualizar={recargar}
        cargando={rev.cargando}
        siguiente={{ ruta: "/informe-final", texto: "Paso 3: Informe final" }}
      />

      <EstadoCarga
        cargando={rev.cargando && rev.pendientes.length === 0}
        error={rev.error}
        onReintentar={recargar}
        vacio={
          enEtapa.length === 0 && {
            titulo: "No hay solicitudes en revisión",
            texto: "Las solicitudes delegadas aparecerán aquí cuando los docentes las revisen.",
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
              textoVacio={filtro === "con" ? "Aún no hay sugerencias de docentes." : "Ninguna solicitud coincide."}
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

                  {etapa.clave === "sin" ? (
                    <div className="is-estado text-center">
                      <h3 className="h6 fw-bold mb-1">Esta solicitud aún no se ha delegado</h3>
                      <p className="small text-muted mb-3">Asígnela a uno o dos docentes para recibir su sugerencia.</p>
                      <Link to={`/delegar-docentes?solicitudId=${activa.solicitudId}`} className="btn btn-primary btn-sm">
                        Delegar ahora
                      </Link>
                    </div>
                  ) : etapa.clave === "esperando" ? (
                    <div className="is-estado">
                      <h3 className="h6 fw-bold mb-1">Esperando la revisión</h3>
                      <p className="small text-muted mb-3">
                        La sugerencia aparecerá aquí en cuanto uno de los docentes la envíe.
                      </p>
                      <ul className="list-unstyled mb-0">
                        {delegaciones.map((d) => (
                          <li key={d.id ?? d.docenteCorreo} className="py-1">
                            <div className="d-flex justify-content-between align-items-center">
                              <span className="fw-semibold">{nombreDocente(d.docenteCorreo)}</span>
                              <span className="small text-muted">Delegada {fechaCorta(d.fechaDelegacion)}</span>
                            </div>
                            {d.documentacionSolicitada && (
                              <div className="alert alert-warning py-1 px-2 small mt-1 mb-0">
                                <strong>Solicita documentación:</strong> {d.documentacionSolicitada}
                              </div>
                            )}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ) : cargandoConsolidado ? (
                    <div className="d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
                      <div className="spinner-border spinner-border-sm text-primary" role="status" />
                      Cargando sugerencia…
                    </div>
                  ) : errorConsolidado ? (
                    <div className="alert alert-danger">{errorConsolidado}</div>
                  ) : (
                    <>
                      <div className="is-resumen mb-3">
                        <div className="d-flex flex-wrap justify-content-between align-items-start gap-3">
                          <div>
                            <div className="small text-muted">Sugerido por</div>
                            <div className="fw-bold">{nombreDocente(consolidado?.revisadoPor || revision?.docenteCorreo)}</div>
                            <div className="small text-muted">
                              {revision?.fechaRevision ? `Enviado ${fechaCorta(revision.fechaRevision)}` : ""}
                            </div>
                          </div>
                          <div className="text-sm-end">
                            <div className="small text-muted">Homologación sugerida</div>
                            <span className={`badge rounded-pill fs-6 ${resultado.clase}`}>{resultado.texto}</span>
                          </div>
                        </div>
                        {filas.length > 0 && (
                          <div className="mt-3">
                            <div className="d-flex justify-content-between small mb-1">
                              <span>
                                <strong>{homologables}</strong> de {filas.length} asignaturas se homologan
                              </span>
                              <span className="text-muted">{Math.round((homologables / filas.length) * 100)} %</span>
                            </div>
                            <div
                              className="progress is-progreso"
                              role="progressbar"
                              aria-valuenow={homologables}
                              aria-valuemin={0}
                              aria-valuemax={filas.length}
                            >
                              <div className="progress-bar bg-success" style={{ width: `${(homologables / filas.length) * 100}%` }} />
                            </div>
                          </div>
                        )}
                        {delegaciones.length > 1 && (
                          <div className="small text-muted mt-3">
                            {delegaciones
                              .filter((d) => d !== revision)
                              .map(
                                (d) =>
                                  `${nombreDocente(d.docenteCorreo)}: ${esRevisada(d) ? "también revisó" : "no revisó todavía"}`
                              )
                              .join(" · ")}
                            . Se toma la primera revisión enviada.
                          </div>
                        )}
                      </div>

                      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                        <h3 className="h6 fw-bold mb-0">Asignaturas evaluadas</h3>
                        <div className="btn-group btn-group-sm" role="group" aria-label="Filtrar asignaturas">
                          {FILTROS_TABLA.map((f) => (
                            <button
                              key={f.valor}
                              type="button"
                              className={`btn ${filtroTabla === f.valor ? "btn-secondary" : "btn-outline-secondary"}`}
                              onClick={() => setFiltroTabla(f.valor)}
                            >
                              {f.etiqueta}
                            </button>
                          ))}
                        </div>
                      </div>

                      <div className="table-responsive mb-3">
                        <table className="table align-middle mb-0 is-tabla">
                          <thead>
                            <tr>
                              <th>Asignatura de origen → destino</th>
                              <th className="text-center">Contenido</th>
                              <th className="text-center">¿Se homologa?</th>
                              <th>Justificación del docente</th>
                            </tr>
                          </thead>
                          <tbody>
                            {filasVisibles.map((f, i) => {
                              const si = seHomologa(f);
                              const p = porcentaje(f.contenido);
                              return (
                                <tr key={f.asignaturaId ?? i}>
                                  <td>
                                    <div className="fw-semibold">{f.asignaturaOrigen || "-"}</div>
                                    <div className="small text-muted">→ {f.asignaturaDestino || "Sin equivalente en la malla"}</div>
                                  </td>
                                  <td className="text-center is-contenido">
                                    {p === null ? (
                                      "-"
                                    ) : (
                                      <>
                                        <span className={`fw-semibold ${p >= UMBRAL_CONTENIDO ? "text-success" : "text-danger"}`}>
                                          {p} %
                                        </span>
                                        <div className="progress mt-1" style={{ height: 4 }}>
                                          <div
                                            className={`progress-bar ${p >= UMBRAL_CONTENIDO ? "bg-success" : "bg-danger"}`}
                                            style={{ width: `${Math.min(p, 100)}%` }}
                                          />
                                        </div>
                                      </>
                                    )}
                                  </td>
                                  <td className="text-center">
                                    <span
                                      className={`badge rounded-pill ${
                                        si ? "bg-success-subtle text-success-emphasis" : "bg-danger-subtle text-danger-emphasis"
                                      }`}
                                    >
                                      {si ? "Sí" : "No"}
                                    </span>
                                  </td>
                                  <td className="small">{f.justificacion || "-"}</td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                      <p className="small text-muted">
                        El contenido en rojo no alcanza el {UMBRAL_CONTENIDO} % que exige la regla RN-03.
                      </p>

                      {consolidado?.informeSugeridoTexto && (
                        <details className="is-memorando mb-4">
                          <summary className="fw-semibold">Ver memorando completo del docente</summary>
                          <pre className="mt-2 mb-0">{consolidado.informeSugeridoTexto}</pre>
                        </details>
                      )}

                      <div className="is-siguiente d-flex flex-wrap justify-content-between align-items-center gap-2">
                        <button type="button" className="btn btn-outline-secondary" onClick={descargar} disabled={descargando}>
                          {descargando ? "Descargando…" : "⬇ Descargar PDF"}
                        </button>
                        {consolidado?.puedeGenerarInformeFinal && (
                          <Link to={`/informe-final?solicitudId=${activa.solicitudId}`} className="btn btn-primary fw-semibold">
                            Continuar al informe final →
                          </Link>
                        )}
                      </div>
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
