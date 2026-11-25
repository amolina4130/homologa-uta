import { useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import FiltroNivel from "../components/FiltroNivel";
import ListaDocumentos from "../components/ListaDocumentos";
import TablaMateriasAprobadas from "../components/TablaMateriasAprobadas";
import { recomendarPorCarrera } from "../services/docenteService";
import {
  actualizarSolicitud,
  crearSolicitud,
  obtenerAsignaturas,
  obtenerCarreras,
  obtenerSolicitud,
} from "../services/solicitudesService";
import { obtenerDocumentosSolicitud, subirDocumento } from "../services/documentosService";
import { procesarCarpeta, registrarUrlExterna } from "../services/rutaCarpetaService";
import {
  actualizarMateriasAprobadas,
  analizarSolicitud,
  analizarSolicitudExterna,
  extraerRecord,
  obtenerMateriasAprobadas,
} from "../services/homologacionService";
import "./NuevaSolicitud.css";

const PASOS = ["Datos", "Documentos", "Materias aprobadas", "Enviar a revisión"];
const TAMANO_MAXIMO = 10 * 1024 * 1024;
const NOMBRE_SIN_IDENTIFICAR = "Estudiante sin identificar";

const esRecord = (d) => /RECORD|RÉCORD|HISTORIAL/i.test(d?.tipoDocumento || d?.TipoDocumento || "");
const esUrlExterna = (d) => /URL_EXTERNA/i.test(d?.tipoDocumento || d?.TipoDocumento || "");
const numeroDe = (s) => s?.numeroProceso || s?.numero || (s?.id ? `SOL-${String(s.id).padStart(4, "0")}` : "");

const DATOS_VACIOS = {
  modulo: "INTERNO",
  universidadOrigen: "",
  universidadDestino: "",
  estudianteNombre: "",
  estudianteCedula: "",
  origen: "",
  destino: "",
  seleccionadas: [],
};

export default function NuevaSolicitud() {
  const { rol, usuario } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [solicitudId, setSolicitudId] = useState(searchParams.get("solicitudId") || "");

  const [paso, setPaso] = useState(1);
  const [alcanzado, setAlcanzado] = useState(1); // último paso al que se puede saltar
  const [cargandoInicial, setCargandoInicial] = useState(Boolean(solicitudId));
  const [solicitud, setSolicitud] = useState(null);
  const [aviso, setAviso] = useState(null);

  // Paso 1: datos
  const [carreras, setCarreras] = useState([]);
  const [asignaturas, setAsignaturas] = useState([]);
  const [datos, setDatos] = useState(DATOS_VACIOS);
  const [nivel, setNivel] = useState("Todos");
  const [guardandoDatos, setGuardandoDatos] = useState(false);
  const [recomendacion, setRecomendacion] = useState(null);
  const [recomendando, setRecomendando] = useState(false);

  // Paso 2: documentos
  const [documentos, setDocumentos] = useState([]);
  const [archivo, setArchivo] = useState(null);
  const [claveArchivo, setClaveArchivo] = useState(0);
  const [subiendo, setSubiendo] = useState(false);
  const [url, setUrl] = useState("");
  const [ruta, setRuta] = useState("");
  const [registrandoRuta, setRegistrandoRuta] = useState(false);

  // Paso 3: materias
  const [materias, setMaterias] = useState([]);
  const [materiasGuardadas, setMateriasGuardadas] = useState(false);
  const [extrayendo, setExtrayendo] = useState(false);
  const [guardandoMaterias, setGuardandoMaterias] = useState(false);
  const [revisionMaterias, setRevisionMaterias] = useState("");

  // Paso 4
  const [enviando, setEnviando] = useState(false);
  const [enviada, setEnviada] = useState(false);

  const externo = datos.modulo === "EXTERNO";
  const tieneRecord = documentos.some(esRecord);
  const tieneUrl = documentos.some(esUrlExterna);
  const documentosListos = tieneRecord && (!externo || tieneUrl);
  const materiasListas = materiasGuardadas && materias.length > 0;
  const editable = !solicitud || (solicitud.estado || "REGISTRADA") === "REGISTRADA";

  const irA = (n) => {
    setAviso(null);
    setPaso(n);
    setAlcanzado((a) => Math.max(a, n));
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const cambiarDato = (campo, valor) => setDatos((d) => ({ ...d, [campo]: valor }));

  const cargarAsignaturas = useCallback((carreraId) => {
    if (!carreraId) {
      setAsignaturas([]);
      return Promise.resolve();
    }
    return obtenerAsignaturas(carreraId)
      .then((lista) => setAsignaturas(Array.isArray(lista) ? lista : []))
      .catch(() => setAsignaturas([]));
  }, []);

  const recargarDocumentos = useCallback(
    (id) =>
      obtenerDocumentosSolicitud(id)
        .then((lista) => {
          const docs = Array.isArray(lista) ? lista : [];
          setDocumentos(docs);
          return docs;
        })
        .catch(() => []),
    []
  );

  // Al entrar: carreras y, si se retoma una solicitud, todo lo que ya tiene.
  useEffect(() => {
    obtenerCarreras()
      .then((lista) => setCarreras(Array.isArray(lista) ? lista : []))
      .catch(() => setCarreras([]));

    if (!solicitudId) return;
    Promise.all([
      obtenerSolicitud(solicitudId),
      recargarDocumentos(solicitudId),
      obtenerMateriasAprobadas(solicitudId).catch(() => []),
    ])
      .then(async ([s, docs, mats]) => {
        setSolicitud(s);
        const nombre = s.estudianteNombre && s.estudianteNombre !== NOMBRE_SIN_IDENTIFICAR ? s.estudianteNombre : "";
        setDatos({
          modulo: s.modulo || "INTERNO",
          universidadOrigen: s.universidadOrigen || "",
          universidadDestino: s.universidadDestino || "",
          estudianteNombre: nombre,
          estudianteCedula: s.estudianteCedula && s.estudianteCedula !== "-" ? s.estudianteCedula : "",
          origen: String(s.carreraOrigenId || ""),
          destino: String(s.carreraDestinoId || ""),
          seleccionadas: (s.asignaturas || []).map((a) => a.asignaturaId),
        });
        await cargarAsignaturas(s.carreraOrigenId);
        const lista = Array.isArray(mats) ? mats : [];
        setMaterias(lista);
        setMateriasGuardadas(lista.length > 0);
        // Se retoma en el primer paso que falte.
        const ext = s.modulo === "EXTERNO";
        const docsOk = docs.some(esRecord) && (!ext || docs.some(esUrlExterna));
        const siguiente = !docsOk ? 2 : lista.length === 0 ? 3 : 4;
        setPaso(siguiente);
        setAlcanzado(siguiente);
      })
      .catch((e) => setAviso({ tipo: "danger", texto: e.message || "No se pudo cargar la solicitud." }))
      .finally(() => setCargandoInicial(false));
    // Solo al entrar.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /* ---------- Paso 1 ---------- */

  const elegirOrigen = (valor) => {
    setDatos((d) => ({ ...d, origen: valor, destino: valor === d.destino ? "" : d.destino, seleccionadas: [] }));
    setNivel("Todos");
    setRecomendacion(null);
    cargarAsignaturas(valor);
  };

  const alternarAsignatura = (id) =>
    setDatos((d) => ({
      ...d,
      seleccionadas: d.seleccionadas.includes(id) ? d.seleccionadas.filter((x) => x !== id) : [...d.seleccionadas, id],
    }));

  const pedirRecomendacion = async () => {
    setRecomendando(true);
    setRecomendacion(null);
    try {
      const r = await recomendarPorCarrera({
        carreraOrigenId: Number(datos.origen),
        carreraDestinoId: Number(datos.destino),
        asignaturasOrigenIds: datos.seleccionadas,
      });
      setRecomendacion((r?.recomendaciones || []).filter((x) => x.homologable));
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo obtener la recomendación." });
    } finally {
      setRecomendando(false);
    }
  };

  const errorDatos = () => {
    if (!datos.origen || !datos.destino) return "Elija la carrera de origen y la de destino.";
    if (datos.origen === datos.destino) return "La carrera de origen y la de destino deben ser distintas.";
    if (externo && (!datos.universidadOrigen.trim() || !datos.universidadDestino.trim()))
      return "Indique la universidad de origen y la de destino.";
    if (datos.estudianteCedula && !/^\d{10}$/.test(datos.estudianteCedula))
      return "La cédula debe tener 10 dígitos (o déjela vacía).";
    if (datos.seleccionadas.length === 0) return "Seleccione al menos una asignatura a homologar.";
    return "";
  };

  const guardarDatos = async (ev) => {
    ev.preventDefault();
    const error = errorDatos();
    if (error) {
      setAviso({ tipo: "danger", texto: error });
      return;
    }
    const cuerpo = {
      modulo: datos.modulo,
      universidadOrigen: externo ? datos.universidadOrigen.trim() : null,
      universidadDestino: externo ? datos.universidadDestino.trim() : null,
      carreraOrigenId: Number(datos.origen),
      carreraDestinoId: Number(datos.destino),
      asignaturasIds: datos.seleccionadas,
      estudianteNombre: datos.estudianteNombre.trim(),
      estudianteCedula: datos.estudianteCedula.trim(),
    };
    setGuardandoDatos(true);
    setAviso(null);
    try {
      let id = solicitudId;
      if (id) {
        await actualizarSolicitud(id, cuerpo);
      } else {
        const creada = await crearSolicitud(cuerpo);
        id = String(creada?.id ?? creada?.data?.id ?? "");
        if (!id) throw new Error("La solicitud se registró, pero no se recibió su identificador.");
        setSolicitudId(id);
        setSearchParams({ solicitudId: id }, { replace: true });
      }
      setSolicitud(await obtenerSolicitud(id));
      await recargarDocumentos(id);
      irA(2);
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo guardar la solicitud." });
    } finally {
      setGuardandoDatos(false);
    }
  };

  /* ---------- Paso 2 ---------- */

  const elegirArchivo = (ev) => {
    const f = ev.target.files?.[0];
    setAviso(null);
    if (f && (f.type !== "application/pdf" || f.size > TAMANO_MAXIMO)) {
      setAviso({ tipo: "danger", texto: "El archivo debe ser un PDF de hasta 10 MB." });
      setArchivo(null);
      setClaveArchivo((n) => n + 1);
      return;
    }
    setArchivo(f || null);
  };

  const subirRecord = async () => {
    if (!archivo) return;
    setSubiendo(true);
    setAviso(null);
    try {
      await subirDocumento(solicitudId, "HISTORIAL", archivo);
      await recargarDocumentos(solicitudId);
      setArchivo(null);
      setClaveArchivo((n) => n + 1);
      setAviso({ tipo: "success", texto: "Récord académico subido y procesado." });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo subir el archivo." });
    } finally {
      setSubiendo(false);
    }
  };

  const registrarRuta = async (tipo) => {
    const valor = (tipo === "URL" ? url : ruta).trim();
    if (!valor) return;
    setRegistrandoRuta(true);
    setAviso(null);
    try {
      const datosRuta = { solicitudId: Number(solicitudId), usuario: usuario?.correo || "" };
      if (tipo === "URL") await registrarUrlExterna({ ...datosRuta, url: valor });
      else await procesarCarpeta({ ...datosRuta, ruta: valor });
      await recargarDocumentos(solicitudId);
      if (tipo === "URL") setUrl("");
      else setRuta("");
      setAviso({ tipo: "success", texto: tipo === "URL" ? "URL de la carpeta registrada." : "Carpeta procesada." });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo registrar la ruta." });
    } finally {
      setRegistrandoRuta(false);
    }
  };

  /* ---------- Paso 3 ---------- */

  const extraer = async () => {
    setExtrayendo(true);
    setAviso(null);
    setRevisionMaterias("");
    try {
      const r = await extraerRecord(solicitudId);
      setMaterias(r?.materias ?? []);
      setMateriasGuardadas(false);
      if (r?.requiereRevision)
        setRevisionMaterias(r?.motivo || "El récord no se leyó con claridad. Revise y corrija las materias.");
      if (r?.nombreEstudiante || r?.cedulaEstudiante) setSolicitud(await obtenerSolicitud(solicitudId));
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo leer el récord académico." });
    } finally {
      setExtrayendo(false);
    }
  };

  const guardarMaterias = async (lista) => {
    setGuardandoMaterias(true);
    setAviso(null);
    try {
      const guardadas = await actualizarMateriasAprobadas(solicitudId, lista);
      setMaterias(Array.isArray(guardadas) ? guardadas : lista);
      setMateriasGuardadas(true);
      setAviso({ tipo: "success", texto: "Materias aprobadas guardadas." });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudieron guardar las materias." });
    } finally {
      setGuardandoMaterias(false);
    }
  };

  /* ---------- Paso 4 ---------- */

  const enviar = async () => {
    setEnviando(true);
    setAviso(null);
    try {
      if (externo) await analizarSolicitudExterna(solicitudId);
      else await analizarSolicitud(solicitudId);
      setEnviada(true);
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo enviar la solicitud a revisión." });
    } finally {
      setEnviando(false);
    }
  };

  /* ---------- Vista ---------- */

  const carreraNombre = (id) => carreras.find((c) => String(c.id) === String(id))?.nombre || "—";
  const asignaturasVisibles = asignaturas.filter((a) => nivel === "Todos" || a.nivel === nivel);
  const volverA = rol === "COORDINADOR" ? "/inicio" : "/mis-solicitudes";

  if (cargandoInicial) {
    return (
      <div className="container d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
        <div className="spinner-border spinner-border-sm text-primary" role="status" />
        Cargando solicitud…
      </div>
    );
  }

  // Ya enviada (en esta visita o antes): no se puede seguir editando.
  if (enviada || !editable) {
    return (
      <div className="container pb-5">
        <div className="card border-0 shadow-sm ns-final mx-auto">
          <div className="card-body text-center p-5">
            <div className="ns-final-icono mx-auto mb-3" aria-hidden="true">✓</div>
            <h1 className="h4 fw-bold">
              {enviada ? `${numeroDe(solicitud)} enviada a revisión` : `${numeroDe(solicitud)} ya está en revisión`}
            </h1>
            <p className="text-muted mb-4">
              {rol === "COORDINADOR"
                ? "Ya puede delegarla a los docentes para que la revisen."
                : "El Coordinador la delegará a los docentes. Podrá seguir su avance desde sus solicitudes."}
            </p>
            <div className="d-flex flex-wrap justify-content-center gap-2">
              {rol === "COORDINADOR" && (
                <Link to={`/delegar-docentes?solicitudId=${solicitudId}`} className="btn btn-primary fw-semibold">
                  Delegar a docentes →
                </Link>
              )}
              <Link to={volverA} className="btn btn-outline-secondary">
                {rol === "COORDINADOR" ? "Volver al inicio" : "Ver mis solicitudes"}
              </Link>
              <a href="/nueva-solicitud" className="btn btn-link">
                Registrar otra solicitud
              </a>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="container pb-5">
      {/* Encabezado */}
      <div className="d-flex flex-wrap justify-content-between align-items-end gap-2 mb-3">
        <div>
          <p className="text-muted small text-uppercase fw-semibold mb-1 ns-sobretitulo">
            {solicitud ? `${numeroDe(solicitud)} · Por completar` : "Nueva solicitud"}
          </p>
          <h1 className="h3 fw-bold mb-1">Solicitud de homologación</h1>
          <p className="text-muted mb-0">
            Complete los cuatro pasos. Puede salir y continuar después desde el inicio.
          </p>
        </div>
        <Link to={volverA} className="btn btn-link text-decoration-none">
          Guardar y salir
        </Link>
      </div>

      {/* Pasos */}
      <ol className="ns-pasos mb-4" aria-label="Pasos de la solicitud">
        {PASOS.map((nombre, i) => {
          const n = i + 1;
          // Completado: un paso ya superado (aunque se haya vuelto atrás para revisarlo).
          const hecho = n !== paso && n < alcanzado;
          const disponible = n <= alcanzado && (n === 1 || solicitudId);
          return (
            <li key={nombre} className={`${n === paso ? "actual" : ""} ${hecho ? "hecho" : ""}`}>
              <button
                type="button"
                className="ns-paso-boton"
                onClick={() => disponible && irA(n)}
                disabled={!disponible}
                aria-current={n === paso ? "step" : undefined}
              >
                <span className="ns-punto">{hecho ? "✓" : n}</span>
                <span className="ns-nombre">{nombre}</span>
              </button>
            </li>
          );
        })}
      </ol>

      <div className="card border-0 shadow-sm">
        <div className="card-body p-4">
          {aviso && <div className={`alert alert-${aviso.tipo} py-2`}>{aviso.texto}</div>}

          {/* ---------- PASO 1 ---------- */}
          {paso === 1 && (
            <form onSubmit={guardarDatos}>
              <div className="row g-4">
                <div className="col-lg-5">
                  <h2 className="h6 fw-bold mb-3">¿De dónde viene el estudiante?</h2>
                  <div className="btn-group w-100 mb-3" role="group" aria-label="Modalidad">
                    {[
                      { valor: "INTERNO", texto: "De la UTA", ayuda: "Cambio de carrera" },
                      { valor: "EXTERNO", texto: "De otra universidad", ayuda: "Cambio de universidad" },
                    ].map((m) => (
                      <button
                        key={m.valor}
                        type="button"
                        className={`btn ${datos.modulo === m.valor ? "btn-primary" : "btn-outline-primary"}`}
                        onClick={() => cambiarDato("modulo", m.valor)}
                      >
                        <span className="d-block fw-semibold">{m.texto}</span>
                        <span className="d-block small opacity-75">{m.ayuda}</span>
                      </button>
                    ))}
                  </div>

                  {externo && (
                    <div className="row g-2 mb-3">
                      <div className="col-sm-6">
                        <label htmlFor="nsUniOrigen" className="form-label small mb-1">Universidad de origen *</label>
                        <input
                          id="nsUniOrigen"
                          className="form-control"
                          maxLength={150}
                          value={datos.universidadOrigen}
                          onChange={(e) => cambiarDato("universidadOrigen", e.target.value)}
                        />
                      </div>
                      <div className="col-sm-6">
                        <label htmlFor="nsUniDestino" className="form-label small mb-1">Universidad de destino *</label>
                        <input
                          id="nsUniDestino"
                          className="form-control"
                          maxLength={150}
                          placeholder="Universidad Técnica de Ambato"
                          value={datos.universidadDestino}
                          onChange={(e) => cambiarDato("universidadDestino", e.target.value)}
                        />
                      </div>
                    </div>
                  )}

                  <div className="mb-3">
                    <label htmlFor="nsOrigen" className="form-label small mb-1">Carrera de origen *</label>
                    <select id="nsOrigen" className="form-select" value={datos.origen} onChange={(e) => elegirOrigen(e.target.value)}>
                      <option value="">Elija una carrera…</option>
                      {carreras.map((c) => (
                        <option key={c.id} value={c.id}>{c.nombre}</option>
                      ))}
                    </select>
                  </div>
                  <div className="mb-4">
                    <label htmlFor="nsDestino" className="form-label small mb-1">Carrera de destino *</label>
                    <select
                      id="nsDestino"
                      className="form-select"
                      value={datos.destino}
                      onChange={(e) => cambiarDato("destino", e.target.value)}
                    >
                      <option value="">Elija una carrera…</option>
                      {carreras.map((c) => (
                        <option key={c.id} value={c.id} disabled={String(c.id) === String(datos.origen)}>
                          {c.nombre}
                        </option>
                      ))}
                    </select>
                  </div>

                  <h2 className="h6 fw-bold mb-1">Titular <span className="text-muted fw-normal small">(opcional)</span></h2>
                  <p className="small text-muted mb-2">Si no los conoce, se toman del récord académico en el paso 3.</p>
                  <div className="row g-2">
                    <div className="col-sm-7">
                      <label htmlFor="nsNombre" className="visually-hidden">Nombre del estudiante</label>
                      <input
                        id="nsNombre"
                        className="form-control"
                        placeholder="Nombre completo"
                        value={datos.estudianteNombre}
                        onChange={(e) => cambiarDato("estudianteNombre", e.target.value)}
                      />
                    </div>
                    <div className="col-sm-5">
                      <label htmlFor="nsCedula" className="visually-hidden">Cédula del estudiante</label>
                      <input
                        id="nsCedula"
                        className="form-control"
                        inputMode="numeric"
                        maxLength={10}
                        placeholder="Cédula"
                        value={datos.estudianteCedula}
                        onChange={(e) => cambiarDato("estudianteCedula", e.target.value.replace(/\D/g, "").slice(0, 10))}
                      />
                    </div>
                  </div>
                </div>

                <div className="col-lg-7">
                  <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                    <h2 className="h6 fw-bold mb-0">
                      Asignaturas a homologar *{" "}
                      {datos.seleccionadas.length > 0 && (
                        <span className="badge rounded-pill bg-primary ms-1">{datos.seleccionadas.length}</span>
                      )}
                    </h2>
                    {!externo && datos.origen && datos.destino && (
                      <button type="button" className="btn btn-sm btn-link px-0" onClick={pedirRecomendacion} disabled={recomendando}>
                        {recomendando ? "Consultando…" : "✨ Sugerir con el agente"}
                      </button>
                    )}
                  </div>

                  {recomendacion && (
                    <div className="alert alert-info py-2 small d-flex flex-wrap justify-content-between align-items-center gap-2">
                      <span>
                        {recomendacion.length === 0
                          ? "El agente no encontró materias homologables."
                          : `El agente sugiere ${recomendacion.length} materia(s): ${recomendacion.map((r) => r.asignaturaOrigen).join(", ")}.`}
                      </span>
                      {recomendacion.length > 0 && (
                        <button
                          type="button"
                          className="btn btn-sm btn-outline-primary"
                          onClick={() => {
                            const ids = recomendacion
                              .map((r) => Number(r.asignaturaId))
                              .filter((id) => asignaturas.some((a) => Number(a.id) === id));
                            setDatos((d) => ({ ...d, seleccionadas: Array.from(new Set([...d.seleccionadas, ...ids])) }));
                            setRecomendacion(null);
                          }}
                        >
                          Marcarlas
                        </button>
                      )}
                    </div>
                  )}

                  {!datos.origen ? (
                    <div className="ns-vacio">Elija la carrera de origen para ver sus asignaturas.</div>
                  ) : (
                    <>
                      <FiltroNivel nivelSeleccionado={nivel} onChange={setNivel} />
                      <div className="ns-asignaturas">
                        {asignaturasVisibles.length === 0 ? (
                          <p className="text-muted small mb-0">No hay asignaturas en este nivel.</p>
                        ) : (
                          asignaturasVisibles.map((a) => (
                            <label key={a.id} className={`ns-asignatura ${datos.seleccionadas.includes(a.id) ? "marcada" : ""}`}>
                              <input
                                type="checkbox"
                                className="form-check-input mt-0"
                                checked={datos.seleccionadas.includes(a.id)}
                                onChange={() => alternarAsignatura(a.id)}
                              />
                              <span className="flex-grow-1">{a.nombre}</span>
                              <span className="small text-muted text-nowrap">{a.nivel}</span>
                            </label>
                          ))
                        )}
                      </div>
                    </>
                  )}
                  <p className="small text-muted mt-2 mb-0">
                    Desarrollo de Proyectos, Titulación y Prácticas Laborales no se homologan por regla institucional.
                  </p>
                </div>
              </div>

              <div className="ns-acciones">
                <span />
                <button type="submit" className="btn btn-primary fw-semibold px-4" disabled={guardandoDatos}>
                  {guardandoDatos ? "Guardando…" : solicitudId ? "Guardar y continuar →" : "Crear solicitud y continuar →"}
                </button>
              </div>
            </form>
          )}

          {/* ---------- PASO 2 ---------- */}
          {paso === 2 && (
            <>
              <h2 className="h5 fw-bold mb-1">Documentos</h2>
              <p className="text-muted">
                Suba el récord académico del estudiante. La malla y los sílabos de la UTA ya están en el sistema
                {externo ? "; para la otra universidad, registre el enlace a la carpeta con sus documentos." : "."}
              </p>

              <div className={`ns-requisito ${tieneRecord ? "listo" : ""}`}>
                <div className="d-flex justify-content-between align-items-center gap-2 mb-2">
                  <h3 className="h6 fw-bold mb-0">{tieneRecord ? "✓ " : ""}Récord académico (PDF) *</h3>
                </div>
                <div className="d-flex flex-wrap gap-2">
                  <input
                    key={claveArchivo}
                    type="file"
                    accept=".pdf,application/pdf"
                    className="form-control ns-archivo"
                    onChange={elegirArchivo}
                    aria-label="Archivo PDF del récord académico"
                  />
                  <button type="button" className="btn btn-primary" onClick={subirRecord} disabled={!archivo || subiendo}>
                    {subiendo ? "Subiendo…" : tieneRecord ? "Subir otro" : "Subir"}
                  </button>
                </div>
                <div className="form-text">PDF de hasta 10 MB.</div>
              </div>

              {externo && (
                <div className={`ns-requisito ${tieneUrl ? "listo" : ""}`}>
                  <h3 className="h6 fw-bold mb-2">{tieneUrl ? "✓ " : ""}Enlace a la carpeta de la otra universidad *</h3>
                  <div className="d-flex flex-wrap gap-2">
                    <input
                      type="url"
                      className="form-control ns-archivo"
                      placeholder="https://drive.google.com/…"
                      value={url}
                      onChange={(e) => setUrl(e.target.value)}
                      aria-label="URL de la carpeta compartida"
                    />
                    <button type="button" className="btn btn-primary" onClick={() => registrarRuta("URL")} disabled={!url.trim() || registrandoRuta}>
                      Registrar
                    </button>
                  </div>
                </div>
              )}

              <details className="ns-alternativa mb-3">
                <summary>¿Los documentos están en una carpeta del equipo?</summary>
                <div className="d-flex flex-wrap gap-2 mt-2">
                  <input
                    className="form-control ns-archivo"
                    placeholder="C:/Users/Usuario/Downloads/CARPETA_ESTUDIANTE"
                    value={ruta}
                    onChange={(e) => setRuta(e.target.value)}
                    aria-label="Ruta de la carpeta local"
                  />
                  <button type="button" className="btn btn-outline-primary" onClick={() => registrarRuta("LOCAL")} disabled={!ruta.trim() || registrandoRuta}>
                    Procesar carpeta
                  </button>
                </div>
                <div className="form-text">Se leen los PDF de la carpeta (en solo lectura) y se clasifican automáticamente.</div>
              </details>

              {documentos.length > 0 && (
                <>
                  <h3 className="h6 fw-bold">Documentos de la solicitud</h3>
                  <ListaDocumentos documentos={documentos} />
                </>
              )}

              <div className="ns-acciones">
                <button type="button" className="btn btn-outline-secondary" onClick={() => irA(1)}>← Datos</button>
                <button type="button" className="btn btn-primary fw-semibold px-4" onClick={() => irA(3)} disabled={!documentosListos}>
                  Continuar →
                </button>
              </div>
              {!documentosListos && (
                <p className="small text-muted text-end mb-0 mt-2">
                  {!tieneRecord ? "Falta subir el récord académico." : "Falta registrar el enlace a la carpeta."}
                </p>
              )}
            </>
          )}

          {/* ---------- PASO 3 ---------- */}
          {paso === 3 && (
            <>
              <div className="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-2">
                <div>
                  <h2 className="h5 fw-bold mb-1">Materias aprobadas</h2>
                  <p className="text-muted mb-0">
                    El agente lee el récord y lista las materias aprobadas. Revíselas, corrija lo necesario y guárdelas.
                  </p>
                </div>
                <button
                  type="button"
                  className={`btn ${materias.length === 0 ? "btn-primary" : "btn-outline-primary"}`}
                  onClick={extraer}
                  disabled={extrayendo}
                >
                  {extrayendo ? "Leyendo el récord…" : materias.length === 0 ? "Extraer del récord" : "Volver a extraer"}
                </button>
              </div>

              {extrayendo && (
                <div className="alert alert-info py-2 small d-flex align-items-center gap-2">
                  <span className="spinner-border spinner-border-sm" role="status" />
                  El agente está leyendo el récord. Puede tardar unos minutos; también puede agregar las materias a mano.
                </div>
              )}
              {revisionMaterias && <div className="alert alert-warning py-2 small">{revisionMaterias}</div>}
              {materias.length > 0 && !materiasGuardadas && (
                <div className="alert alert-info py-2 small">Revise la lista y pulse <strong>Guardar</strong> para continuar.</div>
              )}

              <TablaMateriasAprobadas
                materias={materias}
                onGuardar={guardarMaterias}
                guardando={guardandoMaterias}
                soloLectura={false}
              />

              <div className="ns-acciones">
                <button type="button" className="btn btn-outline-secondary" onClick={() => irA(2)}>← Documentos</button>
                <button type="button" className="btn btn-primary fw-semibold px-4" onClick={() => irA(4)} disabled={!materiasListas}>
                  Continuar →
                </button>
              </div>
            </>
          )}

          {/* ---------- PASO 4 ---------- */}
          {paso === 4 && (
            <>
              <h2 className="h5 fw-bold mb-1">Revisar y enviar</h2>
              <p className="text-muted">
                Al enviarla, el sistema analiza las asignaturas y la solicitud pasa al Coordinador para delegarla a los
                docentes. Después ya no podrá modificar los datos ni las materias.
              </p>

              <div className="row g-3 mb-2">
                <div className="col-md-4">
                  <div className="ns-resumen">
                    <div className="d-flex justify-content-between">
                      <span className="ns-resumen-titulo">Datos</span>
                      <button type="button" className="btn btn-link btn-sm p-0" onClick={() => irA(1)}>Editar</button>
                    </div>
                    <div className="fw-semibold">{carreraNombre(datos.origen)} → {carreraNombre(datos.destino)}</div>
                    <div className="small text-muted">
                      {externo ? `${datos.universidadOrigen} → ${datos.universidadDestino}` : "Cambio de carrera (UTA)"}
                    </div>
                    <div className="small">{datos.seleccionadas.length} asignatura(s) a homologar</div>
                    <div className="small text-muted">
                      {solicitud?.estudianteNombre && solicitud.estudianteNombre !== NOMBRE_SIN_IDENTIFICAR
                        ? `${solicitud.estudianteNombre}${solicitud.estudianteCedula && solicitud.estudianteCedula !== "-" ? ` · C.I. ${solicitud.estudianteCedula}` : ""}`
                        : "Titular: se toma del récord"}
                    </div>
                  </div>
                </div>
                <div className="col-md-4">
                  <div className="ns-resumen">
                    <div className="d-flex justify-content-between">
                      <span className="ns-resumen-titulo">Documentos</span>
                      <button type="button" className="btn btn-link btn-sm p-0" onClick={() => irA(2)}>Editar</button>
                    </div>
                    <div className="fw-semibold">{documentos.length} documento(s)</div>
                    <div className="small text-success">✓ Récord académico</div>
                    {externo && <div className="small text-success">✓ Enlace a la carpeta</div>}
                  </div>
                </div>
                <div className="col-md-4">
                  <div className="ns-resumen">
                    <div className="d-flex justify-content-between">
                      <span className="ns-resumen-titulo">Materias aprobadas</span>
                      <button type="button" className="btn btn-link btn-sm p-0" onClick={() => irA(3)}>Editar</button>
                    </div>
                    <div className="fw-semibold">{materias.length} materia(s)</div>
                    <div className="small text-muted text-truncate">
                      {materias.slice(0, 3).map((m) => m.nombre || m.materia).filter(Boolean).join(", ")}
                      {materias.length > 3 ? "…" : ""}
                    </div>
                  </div>
                </div>
              </div>

              <div className="ns-acciones">
                <button type="button" className="btn btn-outline-secondary" onClick={() => irA(3)}>← Materias</button>
                <button type="button" className="btn btn-success fw-semibold px-4" onClick={enviar} disabled={enviando}>
                  {enviando ? "Analizando y enviando…" : "Enviar a revisión"}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
