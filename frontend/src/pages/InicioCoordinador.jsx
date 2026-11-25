import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { obtenerCarreras, obtenerMisSolicitudes } from "../services/solicitudesService";
import { descargarInformeFinal, obtenerSolicitudesPendientes } from "../services/coordinadorService";
import "./InicioCoordinador.css";

// Estados en el orden en que avanza una solicitud. Los que no tienen `siempre`
// solo aparecen como pestaña cuando hay solicitudes en ese estado.
const ESTADOS = [
  { valor: "REGISTRADA", etiqueta: "Por completar", plural: "Por completar", clase: "secondary", siempre: true },
  { valor: "PENDIENTE_REVISION", etiqueta: "Pendiente de revisión", plural: "En revisión", clase: "warning", siempre: true },
  // El trámite termina cuando el informe final se envía al Comité.
  { valor: "INFORME_EMITIDO", etiqueta: "Informe enviado", plural: "Informe enviado", clase: "success", siempre: true },
  { valor: "RESUELTA", etiqueta: "Resuelta", plural: "Resueltas", clase: "success" },
  { valor: "APROBADA", etiqueta: "Aprobada", plural: "Aprobadas", clase: "success" },
  { valor: "RECHAZADA", etiqueta: "Rechazada", plural: "Rechazadas", clase: "danger" },
];

const ESTADOS_CON_INFORME = ["INFORME_EMITIDO", "RESUELTA"];

// Por defecto se ven solo las solicitudes en curso; las finalizadas tienen su propia sección.
const SECCIONES = [
  { valor: "pendientes", etiqueta: "Pendientes", estados: ["REGISTRADA", "PENDIENTE_REVISION"] },
  { valor: "finalizadas", etiqueta: "Finalizadas", estados: ESTADOS_CON_INFORME },
];
const seccionDe = (s) => (ESTADOS_CON_INFORME.includes(s.estado) ? "finalizadas" : "pendientes");

const FILTROS_VACIOS = { texto: "", estado: "", carreraId: "", desde: "", hasta: "" };

const POR_PAGINA = 20;
const INTERVALO_ACTUALIZACION_MS = 30000; // igual que la campanita de notificaciones

// Números de página a mostrar: siempre la primera, la última y las vecinas de la actual.
const paginasVisibles = (actual, total) => {
  const paginas = [];
  for (let p = 1; p <= total; p++) {
    if (p === 1 || p === total || Math.abs(p - actual) <= 1) paginas.push(p);
    else if (paginas[paginas.length - 1] !== "…") paginas.push("…");
  }
  return paginas;
};

const infoEstado = (valor) =>
  ESTADOS.find((e) => e.valor === valor) ?? { valor, etiqueta: valor || "Registrada", clase: "secondary" };

const numeroDe = (s) => s.numeroProceso || s.numero || `SOL-${String(s.id).padStart(4, "0")}`;

// Las que están por completar se retoman en el asistente. (El detalle del coordinador genera
// el análisis si falta, así que no se abre para ellas: las movería de estado antes de tiempo.)
const rutaDetalle = (s) =>
  (s.estado || "REGISTRADA") === "REGISTRADA" ? `/nueva-solicitud?solicitudId=${s.id}` : `/detalle-solicitud/${s.id}`;

const fechaCorta = (valor) =>
  valor ? new Date(valor).toLocaleDateString("es-EC", { day: "2-digit", month: "short", year: "numeric" }) : "-";

// yyyy-mm-dd en hora local, para comparar con los <input type="date">.
const fechaISO = (valor) => (valor ? new Date(valor).toLocaleDateString("en-CA") : "");

// El servicio de Solicitudes guarda estos valores cuando aún no se conoce al titular.
const NOMBRE_SIN_IDENTIFICAR = "Estudiante sin identificar";
const titularIdentificado = (s) =>
  Boolean(s.estudianteNombre?.trim()) && s.estudianteNombre !== NOMBRE_SIN_IDENTIFICAR;

const normalizar = (texto) =>
  String(texto ?? "")
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();

function EstadoBadge({ estado }) {
  const info = infoEstado(estado);
  return (
    <span className={`badge rounded-pill bg-${info.clase}-subtle text-${info.clase}-emphasis border border-${info.clase}-subtle`}>
      {info.etiqueta}
    </span>
  );
}

function EstadoVacio({ titulo, texto, children }) {
  return (
    <div className="text-center py-5 px-3">
      <div className="ic-vacio-icono mx-auto mb-3" aria-hidden="true">📄</div>
      <h3 className="h5 fw-bold mb-1">{titulo}</h3>
      <p className="text-muted mb-3">{texto}</p>
      {children}
    </div>
  );
}

export default function InicioCoordinador() {
  const { usuario } = useAuth();
  const [solicitudes, setSolicitudes] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");
  const [aviso, setAviso] = useState("");
  const [carreras, setCarreras] = useState([]);
  const [pendientes, setPendientes] = useState(0);
  const [filtros, setFiltros] = useState(FILTROS_VACIOS);
  const [seccion, setSeccion] = useState("pendientes");
  const [pagina, setPagina] = useState(1);
  const [actualizadoEn, setActualizadoEn] = useState(null);
  const [actualizando, setActualizando] = useState(false);
  const tarjetaRef = useRef(null);

  const irAPagina = (p) => {
    setPagina(p);
    tarjetaRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  // `silencioso`: actualización automática. Si falla, se conservan los datos que
  // ya se ven en lugar de reemplazarlos por un mensaje de error.
  const pedirSolicitudes = useCallback(
    (silencioso = false) =>
      obtenerMisSolicitudes({})
        .then((data) => {
          setSolicitudes(Array.isArray(data) ? data : data?.data || []);
          setError("");
          setActualizadoEn(new Date());
        })
        .catch((e) => {
          if (!silencioso) setError(e.message || "No se pudieron cargar sus solicitudes.");
        })
        .finally(() => setCargando(false)),
    []
  );

  const pedirPendientes = useCallback(
    () =>
      obtenerSolicitudesPendientes()
        .then((lista) => setPendientes(Array.isArray(lista) ? lista.length : 0))
        .catch(() => {}),
    []
  );

  const reintentar = () => {
    setCargando(true);
    setError("");
    pedirSolicitudes();
  };

  const actualizarAhora = () => {
    setActualizando(true);
    Promise.all([pedirSolicitudes(true), pedirPendientes()]).finally(() => setActualizando(false));
  };

  useEffect(() => {
    pedirSolicitudes();
    pedirPendientes();
    obtenerCarreras()
      .then((data) => setCarreras(Array.isArray(data) ? data : []))
      .catch(() => setCarreras([]));
  }, [pedirSolicitudes, pedirPendientes]);

  // Actualización automática. Se pausa mientras la pestaña del navegador está
  // oculta y se pone al día apenas el usuario vuelve a ella.
  useEffect(() => {
    const refrescar = () => {
      if (document.hidden) return;
      pedirSolicitudes(true);
      pedirPendientes();
    };
    const intervalo = setInterval(refrescar, INTERVALO_ACTUALIZACION_MS);
    document.addEventListener("visibilitychange", refrescar);
    return () => {
      clearInterval(intervalo);
      document.removeEventListener("visibilitychange", refrescar);
    };
  }, [pedirSolicitudes, pedirPendientes]);

  const cambiarFiltro = (campo, valor) => {
    setFiltros((prev) => ({ ...prev, [campo]: valor }));
    setPagina(1);
  };
  const limpiarFiltros = () => {
    setFiltros(FILTROS_VACIOS);
    setPagina(1);
  };
  const cambiarSeccion = (valor) => {
    setSeccion(valor);
    setFiltros((prev) => ({ ...prev, estado: "" }));
    setPagina(1);
  };
  const hayFiltros = Object.values(filtros).some(Boolean);

  // Conteo por estado sobre todas las solicitudes (sin filtros), para las pestañas.
  const conteo = useMemo(() => {
    const mapa = {};
    for (const s of solicitudes) {
      const estado = s.estado || "REGISTRADA";
      mapa[estado] = (mapa[estado] || 0) + 1;
    }
    return mapa;
  }, [solicitudes]);

  const seccionActual = SECCIONES.find((x) => x.valor === seccion);
  const enSeccion = useMemo(() => solicitudes.filter((s) => seccionDe(s) === seccion), [solicitudes, seccion]);
  const totalSeccion = (valor) => solicitudes.filter((s) => seccionDe(s) === valor).length;
  // Pestañas de estado solo de la sección actual (y las que tengan solicitudes si no son fijas).
  const pestanas = ESTADOS.filter(
    (e) => seccionActual.estados.includes(e.valor) && (e.siempre || conteo[e.valor])
  );

  const visibles = useMemo(() => {
    const texto = normalizar(filtros.texto.trim());
    const carreraId = Number(filtros.carreraId) || null;
    return enSeccion.filter((s) => {
      if (filtros.estado && (s.estado || "REGISTRADA") !== filtros.estado) return false;
      if (carreraId && s.carreraOrigenId !== carreraId && s.carreraDestinoId !== carreraId) return false;
      const fecha = fechaISO(s.fechaRegistro || s.fecha);
      if (filtros.desde && fecha < filtros.desde) return false;
      if (filtros.hasta && fecha > filtros.hasta) return false;
      if (texto) {
        const campos = [numeroDe(s), s.estudianteNombre, s.estudianteCedula, s.origen, s.destino, s.universidadOrigen];
        if (!campos.some((c) => normalizar(c).includes(texto))) return false;
      }
      return true;
    });
  }, [enSeccion, filtros]);

  // Si una actualización deja menos páginas, se muestra la última disponible.
  const totalPaginas = Math.max(1, Math.ceil(visibles.length / POR_PAGINA));
  const paginaActual = Math.min(pagina, totalPaginas);
  const inicio = (paginaActual - 1) * POR_PAGINA;
  const enPagina = visibles.slice(inicio, inicio + POR_PAGINA);

  const descargar = async (s) => {
    setError("");
    setAviso("");
    try {
      await descargarInformeFinal(s.id);
      setAviso(`Informe de ${numeroDe(s)} descargado.`);
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe.");
    }
  };

  const nombre = usuario?.nombre?.trim();

  return (
    <div className="container pb-5 ic-pagina">
      {/* Encabezado */}
      <div className="d-flex flex-wrap justify-content-between align-items-end gap-3 mb-4">
        <div>
          <p className="text-muted small text-uppercase fw-semibold mb-1 ic-sobretitulo">Coordinación</p>
          <h1 className="h3 fw-bold mb-1">{nombre ? `Hola, ${nombre}` : "Bienvenido"}</h1>
          <p className="text-muted mb-0">Siga el avance de las solicitudes de homologación que registró.</p>
        </div>
        <Link to="/nueva-solicitud" className="btn btn-primary fw-semibold px-3">
          + Nueva solicitud
        </Link>
      </div>

      {/* Trabajo pendiente: lo más importante para el coordinador */}
      {pendientes > 0 && (
        <div className="ic-pendientes d-flex flex-wrap align-items-center justify-content-between gap-3 p-3 mb-4 rounded-3">
          <div className="d-flex align-items-center gap-3">
            <span className="ic-pendientes-numero" aria-hidden="true">{pendientes > 99 ? "99+" : pendientes}</span>
            <div>
              <div className="fw-bold">
                {pendientes === 1 ? "1 solicitud en revisión" : `${pendientes} solicitudes en revisión`}
              </div>
              <div className="small text-muted">Delegue, revise las sugerencias y envíe el informe final al Comité.</div>
            </div>
          </div>
          <Link to="/delegar-docentes" className="btn btn-warning fw-semibold">
            Revisar →
          </Link>
        </div>
      )}

      <div className="card border-0 shadow-sm ic-tarjeta" ref={tarjetaRef}>
        {/* Pestañas y filtros: solo tienen sentido cuando ya hay solicitudes */}
        {(cargando || solicitudes.length > 0) && (
        <>
        <div className="card-header bg-white border-bottom-0 pt-3 px-3">
          <div className="ic-secciones mb-3" role="tablist" aria-label="Sección de solicitudes">
            {SECCIONES.map((x) => (
              <button
                key={x.valor}
                type="button"
                role="tab"
                aria-selected={seccion === x.valor}
                className={`ic-seccion ${seccion === x.valor ? "activa" : ""}`}
                onClick={() => cambiarSeccion(x.valor)}
              >
                {x.etiqueta}
                <span className="ic-seccion-cuenta">{cargando ? "…" : totalSeccion(x.valor)}</span>
              </button>
            ))}
          </div>
          {/* Las pestañas por estado solo aportan si la sección tiene más de un estado. */}
          {pestanas.length > 1 && (
          <ul className="nav nav-pills ic-pestanas gap-1 flex-nowrap overflow-auto" role="tablist">
            {[{ valor: "", plural: "Todas" }, ...pestanas].map((e) => {
              const activa = filtros.estado === e.valor;
              const cantidad = e.valor ? conteo[e.valor] || 0 : enSeccion.length;
              return (
                <li className="nav-item" key={e.valor || "todas"} role="presentation">
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activa}
                    className={`nav-link d-flex align-items-center gap-2 text-nowrap ${activa ? "active" : ""}`}
                    onClick={() => cambiarFiltro("estado", e.valor)}
                  >
                    {e.plural}
                    <span className={`badge rounded-pill ${activa ? "bg-white text-primary" : "bg-body-secondary text-body-secondary"}`}>
                      {cargando ? "…" : cantidad}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
          )}
        </div>

        {/* Barra de búsqueda y filtros */}
        <div className="px-3 pb-3 border-bottom">
          <div className="row g-2 align-items-end">
            <div className="col-lg-4 col-md-12">
              <label htmlFor="icBuscar" className="form-label small text-muted mb-1">Buscar</label>
              <input
                id="icBuscar"
                type="search"
                className="form-control"
                placeholder="N° de proceso, estudiante o cédula"
                value={filtros.texto}
                onChange={(e) => cambiarFiltro("texto", e.target.value)}
              />
            </div>
            <div className="col-lg-3 col-md-6">
              <label htmlFor="icCarrera" className="form-label small text-muted mb-1">Carrera</label>
              <select
                id="icCarrera"
                className="form-select"
                value={filtros.carreraId}
                onChange={(e) => cambiarFiltro("carreraId", e.target.value)}
              >
                <option value="">Todas las carreras</option>
                {carreras.map((c) => (
                  <option key={c.id} value={c.id}>{c.nombre}</option>
                ))}
              </select>
            </div>
            <div className="col-lg-2 col-md-3 col-6">
              <label htmlFor="icDesde" className="form-label small text-muted mb-1">Desde</label>
              <input
                id="icDesde"
                type="date"
                className="form-control"
                value={filtros.desde}
                max={filtros.hasta || undefined}
                onChange={(e) => cambiarFiltro("desde", e.target.value)}
              />
            </div>
            <div className="col-lg-2 col-md-3 col-6">
              <label htmlFor="icHasta" className="form-label small text-muted mb-1">Hasta</label>
              <input
                id="icHasta"
                type="date"
                className="form-control"
                value={filtros.hasta}
                min={filtros.desde || undefined}
                onChange={(e) => cambiarFiltro("hasta", e.target.value)}
              />
            </div>
            <div className="col-lg-1 col-md-12 text-lg-end">
              {hayFiltros && (
                <button type="button" className="btn btn-link text-decoration-none px-0" onClick={limpiarFiltros}>
                  Limpiar
                </button>
              )}
            </div>
          </div>
        </div>
        </>
        )}

        {aviso && <div className="alert alert-success m-3 mb-0 py-2">{aviso}</div>}

        {/* Contenido */}
        {cargando ? (
          <div className="d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
            <div className="spinner-border spinner-border-sm text-primary" role="status" />
            Cargando solicitudes…
          </div>
        ) : error ? (
          <div className="alert alert-danger d-flex flex-wrap justify-content-between align-items-center gap-2 m-3">
            <span>{error}</span>
            <button type="button" className="btn btn-sm btn-outline-danger" onClick={reintentar}>
              Reintentar
            </button>
          </div>
        ) : solicitudes.length === 0 ? (
          <EstadoVacio
            titulo="Aún no ha registrado solicitudes"
            texto="Cuando registre una solicitud de homologación, aquí podrá seguir su avance."
          >
            <Link to="/nueva-solicitud" className="btn btn-primary fw-semibold">
              Registrar la primera solicitud
            </Link>
          </EstadoVacio>
        ) : enSeccion.length === 0 ? (
          seccion === "pendientes" ? (
            <EstadoVacio titulo="No tiene solicitudes pendientes" texto="Todas sus solicitudes ya finalizaron.">
              <button type="button" className="btn btn-outline-primary" onClick={() => cambiarSeccion("finalizadas")}>
                Ver finalizadas
              </button>
            </EstadoVacio>
          ) : (
            <EstadoVacio titulo="Aún no hay solicitudes finalizadas" texto="Aparecerán aquí cuando envíe el informe final al Comité." />
          )
        ) : visibles.length === 0 ? (
          <EstadoVacio
            titulo="Ninguna solicitud coincide"
            texto="Pruebe con otra búsqueda o quite algunos filtros."
          >
            <button type="button" className="btn btn-outline-primary" onClick={limpiarFiltros}>
              Limpiar filtros
            </button>
          </EstadoVacio>
        ) : (
          <>
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0 ic-tabla">
                <thead>
                  <tr>
                    <th>Solicitud</th>
                    <th>Estudiante</th>
                    <th>Carreras</th>
                    <th>Estado</th>
                    <th className="text-end">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {enPagina.map((s) => {
                    const porCompletar = (s.estado || "REGISTRADA") === "REGISTRADA";
                    const tieneInforme = ESTADOS_CON_INFORME.includes(s.estado);
                    const externo = s.modulo === "EXTERNO";
                    return (
                      <tr key={s.id}>
                        <td className="text-nowrap">
                          <Link to={rutaDetalle(s)} className="fw-bold text-decoration-none">
                            {numeroDe(s)}
                          </Link>
                          <div className="small text-muted">{fechaCorta(s.fechaRegistro || s.fecha)}</div>
                        </td>
                        <td className="ic-estudiante">
                          {titularIdentificado(s) ? (
                            <>
                              <div className="fw-semibold">{s.estudianteNombre}</div>
                              <div className="small text-muted">C.I. {s.estudianteCedula}</div>
                            </>
                          ) : (
                            <>
                              <div className="text-muted fst-italic">Titular sin identificar</div>
                              <div className="small text-muted">Se toma del récord académico</div>
                            </>
                          )}
                        </td>
                        <td className="ic-carreras">
                          <div className="text-truncate" title={s.origen || ""}>{s.origen || s.carreraOrigen || "-"}</div>
                          <div className="text-truncate small text-muted" title={s.destino || ""}>
                            → {s.destino || s.carreraDestino || "-"}
                          </div>
                          <div className="d-flex align-items-center gap-2 mt-1 small">
                            <span className={`badge fw-normal ${externo ? "bg-info-subtle text-info-emphasis" : "bg-light text-secondary border"}`}>
                              {externo ? "Externo" : "Interno"}
                            </span>
                            {externo && s.universidadOrigen && (
                              <span className="text-muted text-truncate" title={s.universidadOrigen}>{s.universidadOrigen}</span>
                            )}
                          </div>
                        </td>
                        <td><EstadoBadge estado={s.estado} /></td>
                        <td className="text-end text-nowrap">
                          {porCompletar ? (
                            <Link to={`/nueva-solicitud?solicitudId=${s.id}`} className="btn btn-sm btn-primary">
                              Continuar
                            </Link>
                          ) : (
                            <Link to={rutaDetalle(s)} className="btn btn-sm btn-outline-primary">
                              Ver
                            </Link>
                          )}
                          {tieneInforme && (
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-success ms-1"
                              onClick={() => descargar(s)}
                            >
                              Informe
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 px-3 py-2 small text-muted border-top">
              <span>
                {visibles.length > POR_PAGINA
                  ? `${inicio + 1}–${inicio + enPagina.length} de ${visibles.length}`
                  : `${visibles.length} solicitud${visibles.length === 1 ? "" : "es"}`}
                {visibles.length !== enSeccion.length && ` (filtradas de ${enSeccion.length})`}
              </span>

              {totalPaginas > 1 && (
                <nav aria-label="Páginas de solicitudes">
                  <ul className="pagination pagination-sm mb-0">
                    <li className={`page-item ${paginaActual === 1 ? "disabled" : ""}`}>
                      <button type="button" className="page-link" onClick={() => irAPagina(paginaActual - 1)} aria-label="Página anterior">
                        ‹
                      </button>
                    </li>
                    {paginasVisibles(paginaActual, totalPaginas).map((p, i) =>
                      p === "…" ? (
                        <li key={`sep-${i}`} className="page-item disabled"><span className="page-link">…</span></li>
                      ) : (
                        <li key={p} className={`page-item ${p === paginaActual ? "active" : ""}`}>
                          <button
                            type="button"
                            className="page-link"
                            onClick={() => irAPagina(p)}
                            aria-current={p === paginaActual ? "page" : undefined}
                          >
                            {p}
                          </button>
                        </li>
                      )
                    )}
                    <li className={`page-item ${paginaActual === totalPaginas ? "disabled" : ""}`}>
                      <button type="button" className="page-link" onClick={() => irAPagina(paginaActual + 1)} aria-label="Página siguiente">
                        ›
                      </button>
                    </li>
                  </ul>
                </nav>
              )}

              <span className="d-flex align-items-center gap-2">
                {actualizadoEn && (
                  <span title="La lista se actualiza sola cada 30 segundos">
                    Actualizado {actualizadoEn.toLocaleTimeString("es-EC", { hour: "2-digit", minute: "2-digit" })}
                  </span>
                )}
                <button
                  type="button"
                  className="btn btn-link btn-sm p-0 text-decoration-none"
                  onClick={actualizarAhora}
                  disabled={actualizando}
                >
                  {actualizando ? "Actualizando…" : "↻ Actualizar"}
                </button>
              </span>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
