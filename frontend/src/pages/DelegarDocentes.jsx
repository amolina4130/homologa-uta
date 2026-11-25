import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { delegarDocentes, obtenerDelegaciones } from "../services/coordinadorService";
import { actualizarTitularSolicitud } from "../services/solicitudesService";
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
  cedulaTitular,
  coincideBusqueda,
  datoReal,
  esRevisada,
  fechaCorta,
  nombreTitular,
  numeroDe,
  tieneInformeFinal,
} from "../components/revision/etapas";

const FILTROS = [
  { valor: "sin", etiqueta: "Sin delegar" },
  { valor: "delegadas", etiqueta: "Delegadas" },
  { valor: "todas", etiqueta: "Todas" },
];

const titularDe = (s) => ({ nombre: nombreTitular(s), cedula: cedulaTitular(s) });

export default function DelegarDocentes() {
  const [searchParams, setSearchParams] = useSearchParams();
  const rev = useSolicitudesRevision();
  const { delegacionesPorId, docentes, nombreDocente } = rev;

  const [seleccionId, setSeleccionId] = useState(searchParams.get("solicitudId") || "");
  const [filtro, setFiltro] = useState("todas");
  const [texto, setTexto] = useState("");
  const [aviso, setAviso] = useState(null);

  const [titular, setTitular] = useState({ nombre: "", cedula: "" });
  const [editandoTitular, setEditandoTitular] = useState(false);
  const [guardandoTitular, setGuardandoTitular] = useState(false);

  const [docenteUno, setDocenteUno] = useState("");
  const [docenteDos, setDocenteDos] = useState("");
  const [reasignando, setReasignando] = useState(false);
  const [delegando, setDelegando] = useState(false);

  // Las que ya tienen informe final pasaron esta etapa.
  const enEtapa = useMemo(() => rev.pendientes.filter((s) => !tieneInformeFinal(s)), [rev.pendientes]);

  const prepararPanel = (solicitud, delegaciones) => {
    const t = titularDe(solicitud);
    setTitular(t);
    setEditandoTitular(!t.nombre);
    const correos = (delegaciones || []).map((d) => d.docenteCorreo);
    setDocenteUno(correos[0] || "");
    setDocenteDos(correos[1] || "");
    setReasignando(false);
  };

  const abrir = (resultado, idPreferido) => {
    if (!resultado) return;
    const { lista, mapa } = resultado;
    const candidatas = lista.filter((s) => !tieneInformeFinal(s));
    const elegida =
      candidatas.find((s) => String(s.solicitudId) === String(idPreferido)) ||
      candidatas.find((s) => (mapa[s.solicitudId] || []).length === 0) ||
      candidatas[0];
    setSeleccionId(elegida ? String(elegida.solicitudId) : "");
    if (elegida) prepararPanel(elegida, mapa[elegida.solicitudId]);
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
    prepararPanel(s, delegacionesPorId[s.solicitudId]);
  };

  const conteo = useMemo(() => {
    let sin = 0;
    for (const s of enEtapa) if ((delegacionesPorId[s.solicitudId] || []).length === 0) sin++;
    return { sin, delegadas: enEtapa.length - sin, todas: enEtapa.length };
  }, [enEtapa, delegacionesPorId]);

  const visibles = enEtapa.filter((s) => {
    const delegada = (delegacionesPorId[s.solicitudId] || []).length > 0;
    if (filtro === "sin" && delegada) return false;
    if (filtro === "delegadas" && !delegada) return false;
    return coincideBusqueda(s, texto);
  });

  const activa = enEtapa.find((s) => String(s.solicitudId) === String(seleccionId));
  const delegacionesActiva = activa ? delegacionesPorId[activa.solicitudId] || [] : [];
  const yaDelegada = delegacionesActiva.length > 0;
  const revisadas = delegacionesActiva.filter(esRevisada).length;
  const titularGuardado = titularDe(activa);
  const titularListo = Boolean(titularGuardado.nombre);

  const guardarTitular = async (ev) => {
    ev.preventDefault();
    if (!activa) return;
    const cedula = titular.cedula.trim();
    if (cedula && !/^\d{10}$/.test(cedula)) {
      setAviso({ tipo: "danger", texto: "La cédula debe tener 10 dígitos." });
      return;
    }
    setGuardandoTitular(true);
    setAviso(null);
    try {
      const r = await actualizarTitularSolicitud(activa.solicitudId, titular.nombre.trim(), cedula);
      const nombre = r.estudianteNombre || titular.nombre.trim();
      const ced = r.estudianteCedula || cedula;
      rev.setPendientes((prev) =>
        prev.map((s) =>
          s.solicitudId === activa.solicitudId ? { ...s, estudiante: nombre, estudianteCedula: ced } : s
        )
      );
      setTitular({
        nombre: datoReal(nombre, NOMBRE_SIN_IDENTIFICAR),
        cedula: datoReal(ced, CEDULA_SIN_IDENTIFICAR),
      });
      setEditandoTitular(false);
      setAviso({ tipo: "success", texto: "Datos del titular guardados." });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudieron guardar los datos del titular." });
    } finally {
      setGuardandoTitular(false);
    }
  };

  const delegar = async (ev) => {
    ev.preventDefault();
    if (!activa) return;
    const correos = [docenteUno, docenteDos].map((c) => c.trim()).filter(Boolean);
    if (correos.length === 0) {
      setAviso({ tipo: "danger", texto: "Elija al menos un docente." });
      return;
    }
    if (correos.length === 2 && correos[0].toLowerCase() === correos[1].toLowerCase()) {
      setAviso({ tipo: "danger", texto: "Elija dos docentes distintos." });
      return;
    }
    if (correos.some((c) => !/^\S+@\S+\.\S+$/.test(c))) {
      setAviso({ tipo: "danger", texto: "Ingrese correos electrónicos válidos." });
      return;
    }
    if (
      revisadas > 0 &&
      !window.confirm(
        "Un docente ya envió su sugerencia para esta solicitud. Si reasigna, esa revisión se descarta y los docentes tendrán que revisarla de nuevo. ¿Desea continuar?"
      )
    ) {
      return;
    }

    setDelegando(true);
    setAviso(null);
    try {
      await delegarDocentes(activa.solicitudId, correos);
      const nuevas = await obtenerDelegaciones(activa.solicitudId).catch(() => []);
      rev.setDelegacionesPorId((prev) => ({
        ...prev,
        [activa.solicitudId]: Array.isArray(nuevas) ? nuevas : [],
      }));
      setReasignando(false);
      setAviso({
        tipo: "success",
        texto: `${numeroDe(activa)} delegada a ${correos.map(nombreDocente).join(" y ")}.`,
      });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo registrar la delegación." });
    } finally {
      setDelegando(false);
    }
  };

  const selectorDocente = (id, valor, cambiar, opcional) =>
    docentes.length > 0 ? (
      <select id={id} className="form-select" value={valor} onChange={(e) => cambiar(e.target.value)}>
        <option value="">{opcional ? "Ninguno" : "Elija un docente"}</option>
        {docentes.map((d) => (
          <option key={d.correo} value={d.correo}>
            {d.nombre ? `${[d.titulo, d.nombre].filter(Boolean).join(" ")} — ${d.correo}` : d.correo}
          </option>
        ))}
      </select>
    ) : (
      <input
        id={id}
        type="email"
        className="form-control"
        value={valor}
        placeholder="correo@uta.edu.ec"
        onChange={(e) => cambiar(e.target.value)}
      />
    );

  return (
    <div className="container pb-5">
      <EncabezadoPaso
        paso={1}
        titulo="Delegar a docentes"
        descripcion="Asigne cada solicitud pendiente a uno o dos docentes para que la revisen."
        onActualizar={recargar}
        cargando={rev.cargando}
        siguiente={{ ruta: "/informes-sugeridos", texto: "Paso 2: Sugerencias" }}
      />

      <EstadoCarga
        cargando={rev.cargando && rev.pendientes.length === 0}
        error={rev.error}
        onReintentar={recargar}
        vacio={
          enEtapa.length === 0 && {
            titulo: "No hay solicitudes pendientes de revisión",
            texto: "Cuando una solicitud tenga su récord y su análisis, aparecerá aquí para delegarla.",
          }
        }
      >
        <div className="row g-4">
          <div className="col-lg-5">
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
              mostrarEspera
            />
          </div>

          <div className="col-lg-7">
            {!activa ? (
              <div className="card border-0 shadow-sm">
                <div className="card-body text-center text-muted py-5">Elija una solicitud de la lista.</div>
              </div>
            ) : (
              <div className="card border-0 shadow-sm">
                <div className="card-body p-4">
                  <CabeceraSolicitud
                    solicitud={activa}
                    extra={
                      <div className="d-flex flex-wrap gap-3 small text-muted mt-1">
                        <span>Registrada {fechaCorta(activa.fecha)}</span>
                        <span>{activa.totalAsignaturas ?? 0} asignatura(s)</span>
                        <span>
                          {activa.modulo === "EXTERNO"
                            ? `Externo${activa.universidadOrigen ? ` · ${activa.universidadOrigen}` : ""}`
                            : "Interno"}
                        </span>
                      </div>
                    }
                  />

                  {aviso && <div className={`alert alert-${aviso.tipo} py-2`}>{aviso.texto}</div>}

                  <Paso numero={1} titulo="Titular del trámite" hecho={titularListo && !editandoTitular}>
                    {!editandoTitular && titularListo ? (
                      <div className="d-flex justify-content-between align-items-center dd-caja">
                        <div>
                          <div className="fw-semibold">{titularGuardado.nombre}</div>
                          <div className="small text-muted">
                            {titularGuardado.cedula ? `C.I. ${titularGuardado.cedula}` : "Sin cédula registrada"}
                          </div>
                        </div>
                        <button type="button" className="btn btn-sm btn-link" onClick={() => setEditandoTitular(true)}>
                          Editar
                        </button>
                      </div>
                    ) : (
                      <form onSubmit={guardarTitular}>
                        {!titularListo && (
                          <p className="small text-muted mb-2">
                            Copie el nombre y la cédula del récord académico para que el docente los vea en su
                            revisión. Si los deja vacíos, el agente intentará extraerlos del PDF.
                          </p>
                        )}
                        <div className="row g-2 align-items-end">
                          <div className="col-sm-6">
                            <label htmlFor="ddNombre" className="form-label small mb-1">Nombre completo</label>
                            <input
                              id="ddNombre"
                              className="form-control"
                              value={titular.nombre}
                              onChange={(e) => setTitular((t) => ({ ...t, nombre: e.target.value }))}
                              placeholder="Según el récord académico"
                            />
                          </div>
                          <div className="col-sm-3">
                            <label htmlFor="ddCedula" className="form-label small mb-1">Cédula</label>
                            <input
                              id="ddCedula"
                              className="form-control"
                              inputMode="numeric"
                              maxLength={10}
                              value={titular.cedula}
                              onChange={(e) =>
                                setTitular((t) => ({ ...t, cedula: e.target.value.replace(/\D/g, "") }))
                              }
                              placeholder="10 dígitos"
                            />
                          </div>
                          <div className="col-sm-3 d-flex gap-1">
                            <button type="submit" className="btn btn-outline-primary w-100" disabled={guardandoTitular}>
                              {guardandoTitular ? "Guardando…" : "Guardar"}
                            </button>
                            {titularListo && (
                              <button
                                type="button"
                                className="btn btn-link px-1"
                                aria-label="Cancelar edición"
                                onClick={() => {
                                  setTitular(titularGuardado);
                                  setEditandoTitular(false);
                                }}
                              >
                                ✕
                              </button>
                            )}
                          </div>
                        </div>
                      </form>
                    )}
                  </Paso>

                  <Paso numero={2} titulo="Docentes revisores" hecho={yaDelegada && !reasignando}>
                    {yaDelegada && !reasignando ? (
                      <>
                        <ul className="list-unstyled mb-2">
                          {delegacionesActiva.map((d) => {
                            const revisado = esRevisada(d);
                            return (
                              <li
                                key={d.id ?? d.docenteCorreo}
                                className="dd-caja d-flex justify-content-between align-items-center mb-2"
                              >
                                <div className="text-truncate">
                                  <div className="fw-semibold text-truncate">{nombreDocente(d.docenteCorreo)}</div>
                                  <div className="small text-muted">
                                    Delegada {fechaCorta(d.fechaDelegacion)}
                                    {revisado && d.fechaRevision && ` · revisó ${fechaCorta(d.fechaRevision)}`}
                                  </div>
                                </div>
                                <span
                                  className={`badge rounded-pill ${
                                    revisado
                                      ? "bg-success-subtle text-success-emphasis"
                                      : "bg-secondary-subtle text-secondary-emphasis"
                                  }`}
                                >
                                  {revisado ? "Revisado" : "Pendiente"}
                                </span>
                              </li>
                            );
                          })}
                        </ul>
                        <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
                          <button type="button" className="btn btn-sm btn-link px-0" onClick={() => setReasignando(true)}>
                            Cambiar docentes
                          </button>
                          {revisadas > 0 && (
                            <Link
                              to={`/informes-sugeridos?solicitudId=${activa.solicitudId}`}
                              className="btn btn-sm btn-success fw-semibold"
                            >
                              Ver sugerencia →
                            </Link>
                          )}
                        </div>
                      </>
                    ) : (
                      <form onSubmit={delegar}>
                        {rev.errorDocentes && (
                          <p className="small text-warning-emphasis mb-2">
                            {rev.errorDocentes} Escriba los correos manualmente.
                          </p>
                        )}
                        <div className="row g-2">
                          <div className="col-sm-6">
                            <label htmlFor="ddDocenteUno" className="form-label small mb-1">Docente 1</label>
                            {selectorDocente("ddDocenteUno", docenteUno, setDocenteUno, false)}
                          </div>
                          <div className="col-sm-6">
                            <label htmlFor="ddDocenteDos" className="form-label small mb-1">
                              Docente 2 <span className="text-muted">(opcional)</span>
                            </label>
                            {selectorDocente("ddDocenteDos", docenteDos, setDocenteDos, true)}
                          </div>
                        </div>
                        {yaDelegada && (
                          <p className="small text-muted mt-2 mb-0">Al reasignar se reemplazan los docentes actuales.</p>
                        )}
                        <div className="d-flex justify-content-end gap-2 mt-3">
                          {yaDelegada && (
                            <button
                              type="button"
                              className="btn btn-outline-secondary"
                              onClick={() => prepararPanel(activa, delegacionesActiva)}
                            >
                              Cancelar
                            </button>
                          )}
                          <button type="submit" className="btn btn-primary fw-semibold" disabled={delegando || !docenteUno}>
                            {delegando ? "Delegando…" : yaDelegada ? "Reasignar" : "Delegar revisión"}
                          </button>
                        </div>
                      </form>
                    )}
                  </Paso>
                </div>
              </div>
            )}
          </div>
        </div>
      </EstadoCarga>
    </div>
  );
}
