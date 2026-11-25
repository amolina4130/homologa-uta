import { useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import ListaDocumentos from "../components/ListaDocumentos";
import TablaMateriasAprobadas from "../components/TablaMateriasAprobadas";
import {
  actualizarSolicitud,
  crearSolicitud,
  obtenerCarreras,
  obtenerSolicitud,
} from "../services/solicitudesService";
import { leerTextoPdf, obtenerDocumentosSolicitud, subirDocumento } from "../services/documentosService";
import { procesarCarpeta, registrarUrlExterna } from "../services/rutaCarpetaService";
import {
  actualizarMateriasAprobadas,
  extraerRecord,
  obtenerMateriasAprobadas,
} from "../services/homologacionService";
import { delegarDocentes, obtenerDocentes } from "../services/coordinadorService";
import "./NuevaSolicitud.css";

// Primero los documentos que entrega el estudiante (solicitud y récord). Con ellos se prellenan
// sus datos, que el coordinador revisa antes de iniciar el proceso. Las asignaturas candidatas salen
// de las materias APROBADAS del récord; luego la IA compara con las mallas y sílabos y sugiere.
const PASOS = ["Documentos", "Datos del estudiante", "Asignar docente"];
const TAMANO_MAXIMO = 10 * 1024 * 1024;
const NOMBRE_SIN_IDENTIFICAR = "Estudiante sin identificar";
const UTA = "Universidad Técnica de Ambato";

const sinTildes = (s) => (s || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
const tipoTitulo = (s) =>
  (s || "").toLowerCase().replace(/(^|\s)(\p{L})/gu, (m, esp, letra) => esp + letra.toUpperCase()).replace(/\s(De|Del|La|Las|Los|Y)\s/g, (m) => m.toLowerCase());

/** Carrera nombrada como "Carrera de X" (o "CarreraX") en el texto, a partir de una posición. */
function carreraEn(texto, carreras, desde = 0, excluir = "") {
  const t = sinTildes(texto).slice(desde);
  const ordenadas = [...carreras].sort((a, b) => b.nombre.length - a.nombre.length);
  let mejor = null;
  for (const c of ordenadas) {
    if (String(c.id) === String(excluir)) continue;
    const m = new RegExp(`carrera\\s*(de\\s*)?${sinTildes(c.nombre).replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}`).exec(t);
    if (m && (!mejor || m.index < mejor.indice)) mejor = { id: String(c.id), indice: m.index };
  }
  return mejor?.id || "";
}

/**
 * Datos del estudiante leídos del récord y de su solicitud. Solo se prellena lo que aparece en el
 * texto; el coordinador los verifica antes de iniciar el proceso.
 */
function detectarDatos(textoRecord, textoCarta, carreras) {
  const nombre =
    textoRecord.match(/Estudiante\s*:?\s*([A-Za-zÁÉÍÓÚÑáéíóúñ ]{5,120}?)\s*C[ée]dula/i)?.[1] ||
    textoCarta.match(/Yo,?\s+([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑ ]{4,120}?)\s*,/)?.[1] ||
    "";
  const cedula =
    textoRecord.match(/C[ée]dula[^\d]{0,30}(\d{10})(?!\d)/i)?.[1] ||
    textoCarta.match(/(?:C\.?\s?I\.?|C[ée]dula)\s*:?\s*(\d{10})(?!\d)/i)?.[1] ||
    "";
  const universidad = textoRecord.match(/UNIVERSIDAD(?:\s+[A-ZÁÉÍÓÚÑ]+)+(?![a-záéíóúñ])/)?.[0] || "";
  const esUta = !universidad || sinTildes(universidad).includes("ambato");
  const origen = carreraEn(textoRecord, carreras) || carreraEn(textoCarta, carreras);
  const posHomologar = sinTildes(textoCarta).indexOf("homologaci");
  const destino = posHomologar >= 0 ? carreraEn(textoCarta, carreras, posHomologar, origen) : "";
  return {
    estudianteNombre: nombre.trim() === nombre.trim().toUpperCase() ? tipoTitulo(nombre.trim()) : nombre.trim(),
    estudianteCedula: cedula,
    modulo: esUta ? "INTERNO" : "EXTERNO",
    universidadOrigen: esUta ? "" : tipoTitulo(universidad),
    universidadDestino: esUta ? "" : UTA,
    origen,
    destino,
  };
}

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

  // Paso 2: datos del estudiante
  const [carreras, setCarreras] = useState([]);
  const [datos, setDatos] = useState(DATOS_VACIOS);
  const [guardandoDatos, setGuardandoDatos] = useState(false);
  const [leyendo, setLeyendo] = useState(false);
  const [leidos, setLeidos] = useState(null); // campos que se prellenaron desde los documentos

  // Paso 1: documentos
  const [documentos, setDocumentos] = useState([]);
  const [archivo, setArchivo] = useState(null);
  const [claveArchivo, setClaveArchivo] = useState(0);
  const [subiendo, setSubiendo] = useState(false);
  const [carta, setCarta] = useState(null);
  const [claveCarta, setClaveCarta] = useState(0);
  const [url, setUrl] = useState("");
  const [ruta, setRuta] = useState("");
  const [silabos, setSilabos] = useState([]);
  const [claveSilabos, setClaveSilabos] = useState(0);

  // Paso 3: materias
  const [materias, setMaterias] = useState([]);
  const [, setMateriasGuardadas] = useState(false);
  const [extrayendo, setExtrayendo] = useState(false);
  const [guardandoMaterias, setGuardandoMaterias] = useState(false);
  const [revisionMaterias, setRevisionMaterias] = useState("");

  // Paso 3: asignar docente
  const [docentes, setDocentes] = useState([]);
  const [elegidos, setElegidos] = useState([]);
  const [enviando, setEnviando] = useState(false);
  const [enviada, setEnviada] = useState(false);

  const externo = datos.modulo === "EXTERNO";
  const tieneRecord = documentos.some(esRecord);
  const tieneCarta = documentos.some((d) => /^SOLICITUD$/i.test(d?.tipoDocumento || d?.TipoDocumento || ""));
  const tieneUrl = documentos.some(esUrlExterna);
  const silabosGuardados = documentos.filter((d) => /SILABO|PLAN_ANALITICO/i.test(d?.tipoDocumento || d?.TipoDocumento || "")).length;

  const elegirSilabos = (ev) => {
    const lista = [...(ev.target.files || [])];
    setAviso(null);
    if (lista.some((f) => f.type !== "application/pdf" || f.size > TAMANO_MAXIMO)) {
      setAviso({ tipo: "danger", texto: "Cada sílabo debe ser un PDF de hasta 10 MB." });
      setSilabos([]);
      setClaveSilabos((n) => n + 1);
      return;
    }
    setSilabos(lista);
  };
  const editable = !solicitud || (solicitud.estado || "REGISTRADA") === "REGISTRADA";

  const irA = (n) => {
    setAviso(null);
    setPaso(n);
    setAlcanzado((a) => Math.max(a, n));
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const cambiarDato = (campo, valor) => setDatos((d) => ({ ...d, [campo]: valor }));

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
    obtenerDocentes()
      .then((lista) => setDocentes(Array.isArray(lista) ? lista : []))
      .catch(() => setDocentes([]));
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
        const lista = Array.isArray(mats) ? mats : [];
        setMaterias(lista);
        setMateriasGuardadas(lista.length > 0);
        // Se retoma en el primer paso que falte.
        const ext = s.modulo === "EXTERNO";
        const siguiente = !docs.some(esRecord) ? 1 : ext && !docs.some(esUrlExterna) ? 2 : 3;
        setPaso(siguiente);
        setAlcanzado(siguiente);
      })
      .catch((e) => setAviso({ tipo: "danger", texto: e.message || "No se pudo cargar la solicitud." }))
      .finally(() => setCargandoInicial(false));
    // Solo al entrar.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /* ---------- Paso 1: documentos ---------- */

  // Lee la solicitud y el récord (sin guardarlos todavía) y prellena los datos del estudiante.
  const leerDocumentos = async () => {
    if (!carta || !archivo) {
      setAviso({ tipo: "danger", texto: "Seleccione la solicitud del estudiante y el récord académico (PDF)." });
      return;
    }
    setLeyendo(true);
    setAviso(null);
    try {
      const [textoCarta, textoRecord] = await Promise.all([leerTextoPdf(carta), leerTextoPdf(archivo)]);
      if (textoRecord.trim().length < 20)
        setAviso({
          tipo: "warning",
          texto: "El récord no tiene texto legible (puede ser un PDF escaneado). Ingrese los datos a mano.",
        });
      const detectados = detectarDatos(textoRecord, textoCarta, carreras);
      setDatos((d) => {
        const nuevo = { ...d };
        for (const [campo, valor] of Object.entries(detectados)) if (valor) nuevo[campo] = valor;
        return nuevo;
      });
      setLeidos(Object.fromEntries(Object.entries(detectados).filter(([, v]) => v).map(([k]) => [k, true])));
      irA(2);
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudieron leer los documentos." });
    } finally {
      setLeyendo(false);
    }
  };

  /* ---------- Paso 2: datos del estudiante ---------- */

  const elegirOrigen = (valor) =>
    setDatos((d) => ({ ...d, origen: valor, destino: valor === d.destino ? "" : d.destino }));

  const errorDatos = () => {
    if (!datos.estudianteNombre.trim()) return "Ingrese el nombre del estudiante.";
    if (!/^\d{10}$/.test(datos.estudianteCedula)) return "La cédula del estudiante debe tener 10 dígitos.";
    if (!datos.origen || !datos.destino) return "Elija la carrera de origen y la de destino.";
    if (datos.origen === datos.destino) return "La carrera de origen y la de destino deben ser distintas.";
    if (externo && (!datos.universidadOrigen.trim() || !datos.universidadDestino.trim()))
      return "Indique la universidad de origen y la de destino.";
    if (externo && silabos.length === 0 && silabosGuardados === 0 && !ruta.trim())
      return "Agregue los sílabos de la universidad de origen: súbalos en PDF o indique la carpeta del equipo donde están.";
    return "";
  };

  // Crea la solicitud con los datos revisados, guarda los documentos y lee las materias del récord.
  const iniciarProceso = async (ev) => {
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
    let id = solicitudId;
    try {
      if (id) {
        await actualizarSolicitud(id, cuerpo);
      } else {
        const creada = await crearSolicitud(cuerpo);
        id = String(creada?.id ?? creada?.data?.id ?? "");
        if (!id) throw new Error("La solicitud se registró, pero no se recibió su identificador.");
        setSolicitudId(id);
        setSearchParams({ solicitudId: id }, { replace: true });
      }
      // Documentos elegidos en el paso 1 que aún no se guardaron.
      if (carta) await subirDocumento(id, "SOLICITUD", carta);
      if (archivo) await subirDocumento(id, "HISTORIAL", archivo);
      setCarta(null);
      setArchivo(null);
      const datosRuta = { solicitudId: Number(id), usuario: usuario?.correo || "" };
      if (externo && url.trim()) await registrarUrlExterna({ ...datosRuta, url: url.trim() });
      if (externo) for (const f of silabos) await subirDocumento(id, "SILABO", f);
      setSilabos([]);
      setClaveSilabos((n) => n + 1);
      if (ruta.trim()) await procesarCarpeta({ ...datosRuta, ruta: ruta.trim() });
      setUrl("");
      setRuta("");
      setSolicitud(await obtenerSolicitud(id));
      const docs = await recargarDocumentos(id);
      if (!docs.some(esRecord)) {
        irA(1);
        setAviso({ tipo: "warning", texto: "Solicitud guardada. Falta subir el récord académico." });
        return;
      }
      irA(3);
      if (materias.length === 0) extraer(id);
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo iniciar el proceso." });
      if (id) recargarDocumentos(id);
    } finally {
      setGuardandoDatos(false);
    }
  };

  /* ---------- Paso 1 (solicitud retomada): subir documentos directamente ---------- */

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

  const elegirCarta = (ev) => {
    const f = ev.target.files?.[0];
    setAviso(null);
    if (f && (f.type !== "application/pdf" || f.size > TAMANO_MAXIMO)) {
      setAviso({ tipo: "danger", texto: "El archivo debe ser un PDF de hasta 10 MB." });
      setCarta(null);
      setClaveCarta((n) => n + 1);
      return;
    }
    setCarta(f || null);
  };

  const subirCarta = async () => {
    if (!carta) return;
    setSubiendo(true);
    setAviso(null);
    try {
      await subirDocumento(solicitudId, "SOLICITUD", carta);
      await recargarDocumentos(solicitudId);
      setCarta(null);
      setClaveCarta((n) => n + 1);
      setAviso({ tipo: "success", texto: "Solicitud del estudiante subida." });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo subir el archivo." });
    } finally {
      setSubiendo(false);
    }
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

  /* ---------- Paso 3 ---------- */

  const extraer = async (id = solicitudId) => {
    setExtrayendo(true);
    setRevisionMaterias("");
    try {
      const r = await extraerRecord(id);
      setMaterias(r?.materias ?? []);
      setMateriasGuardadas(false);
      if (r?.requiereRevision)
        setRevisionMaterias(r?.motivo || "El récord no se leyó con claridad. Revise y corrija las materias.");
      if (r?.nombreEstudiante || r?.cedulaEstudiante) setSolicitud(await obtenerSolicitud(id));
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

  /* ---------- Paso 3: asignar docente ---------- */

  const asignar = async () => {
    setEnviando(true);
    setAviso(null);
    try {
      await delegarDocentes(solicitudId, elegidos);
      setSolicitud(await obtenerSolicitud(solicitudId).catch(() => solicitud));
      setEnviada(true);
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo asignar el docente." });
    } finally {
      setEnviando(false);
    }
  };

  /* ---------- Vista ---------- */

  const carreraNombre = (id) => carreras.find((c) => String(c.id) === String(id))?.nombre || "—";
  const volverA = rol === "COORDINADOR" ? "/inicio" : "/mis-solicitudes";

  if (cargandoInicial) {
    return (
      <div className="container d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
        <div className="spinner-border spinner-border-sm text-primary" role="status" />
        Cargando solicitud…
      </div>
    );
  }

  // Ya asignada (en esta visita o antes): la revisión está en manos del docente.
  if (enviada || !editable) {
    const nombres = elegidos
      .map((c) => docentes.find((d) => d.correo === c)?.nombre || c)
      .join(" y ");
    return (
      <div className="container pb-5">
        <div className="card border-0 shadow-sm ns-final mx-auto">
          <div className="card-body text-center p-5">
            <div className="ns-final-icono mx-auto mb-3" aria-hidden="true">✓</div>
            <h1 className="h4 fw-bold">
              {enviada ? `${numeroDe(solicitud)} enviada al docente` : `${numeroDe(solicitud)} ya está en revisión`}
            </h1>
            <p className="text-muted mb-4">
              {enviada && nombres ? `${nombres} recibió la solicitud. ` : ""}
              El docente genera el análisis con la IA, decide qué se homologa y le envía su informe técnico. Cuando
              llegue, la solicitud aparecerá en su inicio para emitir el informe final al Comité.
            </p>
            <div className="d-flex flex-wrap justify-content-center gap-2">
              <Link to={volverA} className="btn btn-primary fw-semibold">
                {rol === "COORDINADOR" ? "Volver al inicio" : "Ver mis solicitudes"}
              </Link>
              <a href="/nueva-solicitud" className="btn btn-outline-secondary">
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
            Documentos del estudiante → datos del estudiante → asignar docente. El docente hace el análisis con la IA.
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
          const disponible = n <= alcanzado && (n <= 2 || solicitudId);
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

          {/* ---------- PASO 1: DOCUMENTOS ---------- */}
          {paso === 1 && (
            <>
              <h2 className="h5 fw-bold mb-1">Documentos que entrega el estudiante</h2>
              <p className="text-muted">
                Suba la solicitud de homologación y el récord académico. Con ellos se completan los datos del
                estudiante en el siguiente paso; la malla y los sílabos de la UTA ya están en el sistema.
              </p>

              <div className={`ns-requisito ${tieneCarta || carta ? "listo" : ""}`}>
                <h3 className="h6 fw-bold mb-2">{tieneCarta || carta ? "✓ " : ""}Solicitud de homologación (PDF) *</h3>
                <div className="d-flex flex-wrap gap-2">
                  <input
                    key={claveCarta}
                    type="file"
                    accept=".pdf,application/pdf"
                    className="form-control ns-archivo"
                    onChange={elegirCarta}
                    aria-label="Archivo PDF de la solicitud del estudiante"
                  />
                  {solicitudId && (
                    <button type="button" className="btn btn-primary" onClick={subirCarta} disabled={!carta || subiendo}>
                      {subiendo ? "Subiendo…" : tieneCarta ? "Subir otra" : "Subir"}
                    </button>
                  )}
                </div>
                <div className="form-text">Carta firmada por el estudiante pidiendo la homologación.</div>
              </div>

              <div className={`ns-requisito ${tieneRecord || archivo ? "listo" : ""}`}>
                <h3 className="h6 fw-bold mb-2">{tieneRecord || archivo ? "✓ " : ""}Récord académico (PDF) *</h3>
                <div className="d-flex flex-wrap gap-2">
                  <input
                    key={claveArchivo}
                    type="file"
                    accept=".pdf,application/pdf"
                    className="form-control ns-archivo"
                    onChange={elegirArchivo}
                    aria-label="Archivo PDF del récord académico"
                  />
                  {solicitudId && (
                    <button type="button" className="btn btn-primary" onClick={subirRecord} disabled={!archivo || subiendo}>
                      {subiendo ? "Subiendo…" : tieneRecord ? "Subir otro" : "Subir"}
                    </button>
                  )}
                </div>
                <div className="form-text">PDF de hasta 10 MB. De aquí salen las materias aprobadas que se analizan.</div>
              </div>

              {documentos.length > 0 && (
                <>
                  <h3 className="h6 fw-bold">Documentos de la solicitud</h3>
                  <ListaDocumentos documentos={documentos} />
                </>
              )}

              <div className="ns-acciones">
                <span />
                {solicitudId ? (
                  <button type="button" className="btn btn-primary fw-semibold px-4" onClick={() => irA(2)} disabled={!tieneRecord}>
                    Continuar →
                  </button>
                ) : (
                  <button
                    type="button"
                    className="btn btn-primary fw-semibold px-4"
                    onClick={leerDocumentos}
                    disabled={!carta || !archivo || leyendo}
                  >
                    {leyendo ? "Leyendo documentos…" : "Leer documentos y continuar →"}
                  </button>
                )}
              </div>
              {!solicitudId && (!carta || !archivo) && (
                <p className="small text-muted text-end mb-0 mt-2">
                  {!carta && !archivo
                    ? "Seleccione la solicitud y el récord académico."
                    : !carta
                    ? "Falta la solicitud de homologación."
                    : "Falta el récord académico."}
                </p>
              )}
              {solicitudId && !tieneRecord && (
                <p className="small text-muted text-end mb-0 mt-2">Falta subir el récord académico.</p>
              )}
            </>
          )}

          {/* ---------- PASO 2: DATOS DEL ESTUDIANTE ---------- */}
          {paso === 2 && (
            <form onSubmit={iniciarProceso}>
              {leidos && Object.keys(leidos).length > 0 && (
                <div className="alert alert-info py-2 small">
                  Se completaron con los documentos los campos marcados como <span className="badge text-bg-info">leído</span>.
                  Verifíquelos y corrija lo necesario antes de iniciar el proceso.
                </div>
              )}
              <div className="row g-4">
                <div className="col-lg-6">
                  <h2 className="h6 fw-bold mb-2">Estudiante</h2>
                  <div className="mb-2">
                    <label htmlFor="nsNombre" className="form-label small mb-1">
                      Nombre completo * {leidos?.estudianteNombre && <span className="badge text-bg-info ms-1">leído</span>}
                    </label>
                    <input
                      id="nsNombre"
                      className="form-control"
                      maxLength={200}
                      value={datos.estudianteNombre}
                      onChange={(e) => cambiarDato("estudianteNombre", e.target.value)}
                    />
                  </div>
                  <div className="mb-3">
                    <label htmlFor="nsCedula" className="form-label small mb-1">
                      Cédula * {leidos?.estudianteCedula && <span className="badge text-bg-info ms-1">leído</span>}
                    </label>
                    <input
                      id="nsCedula"
                      className="form-control"
                      inputMode="numeric"
                      maxLength={10}
                      value={datos.estudianteCedula}
                      onChange={(e) => cambiarDato("estudianteCedula", e.target.value.replace(/\D/g, "").slice(0, 10))}
                    />
                  </div>

                  <h2 className="h6 fw-bold mb-2">¿De dónde viene el estudiante?</h2>
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
                        <label htmlFor="nsUniOrigen" className="form-label small mb-1">
                          Universidad de origen * {leidos?.universidadOrigen && <span className="badge text-bg-info ms-1">leído</span>}
                        </label>
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
                          placeholder={UTA}
                          value={datos.universidadDestino}
                          onChange={(e) => cambiarDato("universidadDestino", e.target.value)}
                        />
                      </div>
                    </div>
                  )}

                  <div className="mb-2">
                    <label htmlFor="nsOrigen" className="form-label small mb-1">
                      Carrera de origen * {leidos?.origen && <span className="badge text-bg-info ms-1">leído</span>}
                    </label>
                    <select id="nsOrigen" className="form-select" value={datos.origen} onChange={(e) => elegirOrigen(e.target.value)}>
                      <option value="">Elija una carrera…</option>
                      {carreras.map((c) => (
                        <option key={c.id} value={c.id}>{c.nombre}</option>
                      ))}
                    </select>
                  </div>
                  <div className="mb-3">
                    <label htmlFor="nsDestino" className="form-label small mb-1">
                      Carrera de destino * {leidos?.destino && <span className="badge text-bg-info ms-1">leído</span>}
                    </label>
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

                  {externo && (
                    <div className="mb-3">
                      <label htmlFor="nsSilabos" className="form-label small mb-1">
                        Sílabos de la universidad de origen (PDF) *{" "}
                        {silabosGuardados > 0 && <span className="badge text-bg-success ms-1">{silabosGuardados} guardados</span>}
                      </label>
                      <input
                        id="nsSilabos"
                        key={claveSilabos}
                        type="file"
                        multiple
                        accept=".pdf,application/pdf"
                        className="form-control"
                        onChange={elegirSilabos}
                      />
                      <div className="form-text">
                        Un PDF por asignatura aprobada, con el nombre de la asignatura en el archivo (p. ej. «Cálculo I.pdf»).
                        La IA compara estos sílabos con la malla de la UTA.
                        {silabos.length > 0 && <strong> {silabos.length} seleccionados.</strong>}
                      </div>
                    </div>
                  )}

                  {externo && (
                    <div className="mb-3">
                      <label htmlFor="nsUrl" className="form-label small mb-1">
                        Enlace a la carpeta de la otra universidad{" "}
                        {tieneUrl && <span className="badge text-bg-success ms-1">registrado</span>}
                      </label>
                      <input
                        id="nsUrl"
                        type="url"
                        className="form-control"
                        placeholder={tieneUrl ? "Ya registrado (puede registrar otro)" : "https://drive.google.com/…"}
                        value={url}
                        onChange={(e) => setUrl(e.target.value)}
                      />
                      <div className="form-text">
                        Se guarda como referencia en el expediente. La IA no puede abrir enlaces (Drive, OneDrive): suba
                        los sílabos en PDF o indique la carpeta del equipo para que los pueda leer.
                      </div>
                    </div>
                  )}

                  <details className="ns-alternativa mb-2">
                    <summary>¿Hay más documentos en una carpeta del equipo?</summary>
                    <input
                      className="form-control mt-2"
                      placeholder="C:/Users/Usuario/Downloads/CARPETA_ESTUDIANTE"
                      value={ruta}
                      onChange={(e) => setRuta(e.target.value)}
                      aria-label="Ruta de la carpeta local"
                    />
                    <div className="form-text">Al iniciar el proceso se leen sus PDF (solo lectura) y se clasifican.</div>
                  </details>
                </div>

                <div className="col-lg-6">
                  <h2 className="h6 fw-bold mb-2">¿Qué pasa al iniciar el proceso?</h2>
                  <div className="ns-vacio text-start">
                    <ol className="small mb-2 ps-3">
                      <li>Se registra la solicitud y se guardan la carta y el récord en el expediente.</li>
                      <li>Se leen las materias del récord; usted las revisa y confirma (solo cuentan las <strong>aprobadas</strong>).</li>
                      <li>
                        {externo
                          ? "La IA compara con los sílabos de la universidad de origen (carpeta registrada) usando criterios generales."
                          : "La IA toma el sílabo y el semestre de cada materia de la malla de origen y aplica las reglas de FISEI: 80 % de contenido, profundidad y carga horaria; máximo 10 años desde la aprobación."}
                      </li>
                      <li>La IA sugiere; el docente asignado decide y el coordinador emite el informe final.</li>
                    </ol>
                    <p className="small text-muted mb-0">
                      Desarrollo de Proyectos, Titulación y Prácticas Laborales no se homologan por regla institucional.
                    </p>
                  </div>
                </div>
              </div>

              <div className="ns-acciones">
                <button type="button" className="btn btn-outline-secondary" onClick={() => irA(1)}>← Documentos</button>
                <button type="submit" className="btn btn-primary fw-semibold px-4" disabled={guardandoDatos}>
                  {guardandoDatos ? "Iniciando…" : solicitudId ? "Guardar y continuar →" : "Iniciar proceso →"}
                </button>
              </div>
            </form>
          )}

          {/* ---------- PASO 3: ASIGNAR DOCENTE ---------- */}
          {paso === 3 && (
            <>
              <h2 className="h5 fw-bold mb-1">Asignar el docente responsable</h2>
              <p className="text-muted">
                El docente recibirá los datos del estudiante, la solicitud y el récord. Él genera el análisis con la IA,
                decide qué se homologa y le envía su informe técnico; usted emite después el informe final al Comité.
              </p>

              <div className="row g-3 mb-3">
                <div className="col-md-6">
                  <div className="ns-resumen h-100">
                    <div className="d-flex justify-content-between">
                      <span className="ns-resumen-titulo">Estudiante</span>
                      <button type="button" className="btn btn-link btn-sm p-0" onClick={() => irA(2)}>Editar</button>
                    </div>
                    <div className="fw-semibold">{datos.estudianteNombre || "—"}</div>
                    <div className="small text-muted">C.I. {datos.estudianteCedula || "—"}</div>
                    <div className="small">{carreraNombre(datos.origen)} → {carreraNombre(datos.destino)}</div>
                    {externo && <div className="small text-muted">{datos.universidadOrigen} → {datos.universidadDestino}</div>}
                  </div>
                </div>
                <div className="col-md-6">
                  <div className="ns-resumen h-100">
                    <div className="d-flex justify-content-between">
                      <span className="ns-resumen-titulo">Documentos</span>
                      <button type="button" className="btn btn-link btn-sm p-0" onClick={() => irA(1)}>Editar</button>
                    </div>
                    {tieneCarta && <div className="small text-success">✓ Solicitud del estudiante</div>}
                    <div className={`small ${tieneRecord ? "text-success" : "text-danger"}`}>
                      {tieneRecord ? "✓" : "✗"} Récord académico
                    </div>
                    {externo && (
                      <div className={`small ${silabosGuardados > 0 ? "text-success" : "text-danger"}`}>
                        {silabosGuardados > 0 ? `✓ ${silabosGuardados} sílabos de la universidad de origen` : "✗ Sin sílabos de la universidad de origen"}
                      </div>
                    )}
                    {externo && tieneUrl && <div className="small text-success">✓ Enlace a la carpeta (referencia)</div>}
                    <div className="small mt-1">
                      {extrayendo ? (
                        <span className="text-muted"><span className="spinner-border spinner-border-sm me-1" role="status" />Leyendo el récord…</span>
                      ) : (
                        <span className={materias.length > 0 ? "text-success" : "text-danger"}>
                          {materias.length > 0 ? `✓ ${materias.length} materias leídas del récord` : "✗ No se leyeron materias del récord"}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              {revisionMaterias && <div className="alert alert-warning py-2 small">{revisionMaterias}</div>}
              <details className="ns-alternativa mb-3" open={!extrayendo && materias.length === 0}>
                <summary>Ver o corregir las materias leídas del récord</summary>
                <div className="d-flex justify-content-end my-2">
                  <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => extraer()} disabled={extrayendo}>
                    {extrayendo ? "Leyendo…" : "Volver a leer el récord"}
                  </button>
                </div>
                <TablaMateriasAprobadas
                  materias={materias}
                  onGuardar={guardarMaterias}
                  guardando={guardandoMaterias}
                  soloLectura={false}
                />
              </details>

              <h3 className="h6 fw-bold mb-2">Docente responsable *</h3>
              {docentes.length === 0 ? (
                <p className="small text-muted">No hay docentes registrados.</p>
              ) : (
                <div className="list-group mb-1" style={{ maxWidth: 560 }}>
                  {docentes.map((d) => {
                    const marcado = elegidos.includes(d.correo);
                    return (
                      <label key={d.correo} className={`list-group-item d-flex align-items-center gap-2 ${marcado ? "active" : ""}`}>
                        <input
                          type="checkbox"
                          className="form-check-input m-0"
                          checked={marcado}
                          disabled={!marcado && elegidos.length >= 2}
                          onChange={() =>
                            setElegidos((l) => (marcado ? l.filter((c) => c !== d.correo) : [...l, d.correo]))
                          }
                        />
                        <span>
                          <span className="fw-semibold">{d.nombre || d.correo}</span>
                          {d.nombre && <span className="small d-block opacity-75">{d.correo}</span>}
                        </span>
                      </label>
                    );
                  })}
                </div>
              )}
              <div className="form-text mb-2">Puede asignar uno o dos docentes; el primero que envíe su informe cierra la revisión.</div>

              <div className="ns-acciones">
                <button type="button" className="btn btn-outline-secondary" onClick={() => irA(2)}>← Datos</button>
                <button
                  type="button"
                  className="btn btn-success fw-semibold px-4"
                  onClick={asignar}
                  disabled={enviando || extrayendo || elegidos.length === 0 || materias.length === 0}
                >
                  {enviando ? "Asignando…" : "Asignar y enviar al docente →"}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}