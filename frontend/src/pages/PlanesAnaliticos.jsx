import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { obtenerCarreras } from "../services/solicitudesService";
import { guardarPlanAnalitico, obtenerAsignaturasMemoria } from "../services/memoriaService";
import { leerTextoPdf } from "../services/documentosService";
import { leerPlanAnalitico } from "../utils/planAnalitico";

// Administrador: carga el plan analítico (o sílabo) de una asignatura. El PDF se lee, se muestra
// lo extraído para revisarlo y, al guardar, queda en la memoria que usa la IA y en "Carreras y mallas".

const esGenerico = (t) => !t || /Unidades:\s*fundamentos,\s*conceptos esenciales/i.test(t);
const TAMANO_MAXIMO = 10 * 1024 * 1024;
const normalizar = (s) => (s || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();

export default function PlanesAnaliticos() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [carreras, setCarreras] = useState([]);
  const [carreraId, setCarreraId] = useState(searchParams.get("carrera") || "");
  const [asignaturas, setAsignaturas] = useState([]);
  const [asignaturaId, setAsignaturaId] = useState(searchParams.get("asignatura") || "");
  const [soloFaltantes, setSoloFaltantes] = useState(true);

  const [archivo, setArchivo] = useState(null);
  const [claveArchivo, setClaveArchivo] = useState(0);
  const [leido, setLeido] = useState(null); // resultado de leer el PDF
  const [contenido, setContenido] = useState("");
  const [resultados, setResultados] = useState("");

  const [ocupado, setOcupado] = useState("");
  const [aviso, setAviso] = useState(null);

  useEffect(() => {
    obtenerCarreras()
      .then((l) => setCarreras([...(Array.isArray(l) ? l : [])].sort((a, b) => a.id - b.id)))
      .catch(() => setAviso({ tipo: "danger", texto: "No se pudieron cargar las carreras." }));
  }, []);

  useEffect(() => {
    if (!carreraId) return;
    let activo = true;
    obtenerAsignaturasMemoria(carreraId)
      .then((l) => activo && setAsignaturas(Array.isArray(l) ? l : []))
      .catch((e) => activo && setAviso({ tipo: "danger", texto: e.message }));
    return () => {
      activo = false;
    };
  }, [carreraId]);

  const asignatura = asignaturas.find((a) => String(a.asignaturaId) === String(asignaturaId));
  const faltan = asignaturas.filter((a) => esGenerico(a.contenidosMinimos)).length;
  const visibles = useMemo(
    () => asignaturas.filter((a) => !soloFaltantes || esGenerico(a.contenidosMinimos) || String(a.asignaturaId) === String(asignaturaId)),
    [asignaturas, soloFaltantes, asignaturaId]
  );

  const limpiarFormulario = () => {
    setArchivo(null);
    setClaveArchivo((n) => n + 1);
    setLeido(null);
    setContenido("");
    setResultados("");
  };

  const elegirCarrera = (id) => {
    setCarreraId(id);
    setAsignaturaId("");
    setAsignaturas([]);
    limpiarFormulario();
    setAviso(null);
    setSearchParams(id ? { carrera: id } : {}, { replace: true });
  };

  const elegirAsignatura = (id) => {
    setAsignaturaId(id);
    limpiarFormulario();
    setAviso(null);
    setSearchParams({ carrera: carreraId, asignatura: id }, { replace: true });
    const a = asignaturas.find((x) => String(x.asignaturaId) === String(id));
    // Si ya tiene plan real, se muestra para editarlo.
    if (a && !esGenerico(a.contenidosMinimos)) {
      setContenido(a.contenidosMinimos);
      setResultados(a.resultadosAprendizaje || "");
    }
  };

  const elegirArchivo = (ev) => {
    const f = ev.target.files?.[0];
    setAviso(null);
    if (f && (f.type !== "application/pdf" || f.size > TAMANO_MAXIMO)) {
      setAviso({ tipo: "danger", texto: "El archivo debe ser un PDF de hasta 10 MB." });
      setClaveArchivo((n) => n + 1);
      setArchivo(null);
      return;
    }
    setArchivo(f || null);
  };

  const leerPdf = async () => {
    setOcupado("leer");
    setAviso(null);
    try {
      const texto = await leerTextoPdf(archivo, { diseno: true });
      const plan = leerPlanAnalitico(texto);
      setLeido(plan);
      if (plan.vacio) {
        setAviso({
          tipo: "warning",
          texto: "El PDF no tiene contenidos por unidad (parece una plantilla vacía o escaneada). Puede escribir el plan a mano.",
        });
        return;
      }
      setContenido(plan.contenido);
      setResultados(plan.resultados);
      if (plan.asignatura && asignatura && normalizar(plan.asignatura) !== normalizar(asignatura.nombre))
        setAviso({
          tipo: "warning",
          texto: `El PDF es de «${plan.asignatura}» y eligió «${asignatura.nombre}». Verifique que sea la asignatura correcta antes de guardar.`,
        });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo leer el PDF." });
    } finally {
      setOcupado("");
    }
  };

  const guardar = async () => {
    setOcupado("guardar");
    setAviso(null);
    try {
      await guardarPlanAnalitico(asignatura.asignaturaId, contenido.trim(), resultados.trim());
      const lista = await obtenerAsignaturasMemoria(carreraId);
      setAsignaturas(Array.isArray(lista) ? lista : []);
      setArchivo(null);
      setClaveArchivo((n) => n + 1);
      setLeido(null);
      setAviso({
        tipo: "success",
        texto: `Plan analítico de «${asignatura.nombre}» guardado. La IA lo usará desde el próximo análisis y ya se ve en Carreras y mallas.`,
      });
    } catch (e) {
      setAviso({ tipo: "danger", texto: e.message || "No se pudo guardar el plan." });
    } finally {
      setOcupado("");
    }
  };

  const unidades = contenido.split("\n").filter((l) => /^U\.\d+/.test(l.trim())).length;

  return (
    <div className="container mt-4 pb-5">
      <div className="d-flex flex-wrap justify-content-between align-items-end gap-2 mb-3">
        <div>
          <h1 className="h3 fw-bold mb-1">Planes analíticos</h1>
          <p className="text-muted mb-0">
            Cargue el plan analítico o sílabo (PDF) de una asignatura. Se lee automáticamente, usted lo revisa y al guardarlo
            la IA lo usa para comparar contenidos.
          </p>
        </div>
        <Link to="/carreras" className="btn btn-outline-secondary">Ver Carreras y mallas</Link>
      </div>

      {aviso && <div className={`alert alert-${aviso.tipo} py-2`}>{aviso.texto}</div>}

      <div className="row g-3">
        {/* Elegir asignatura */}
        <div className="col-lg-4">
          <div className="card shadow-sm border-0">
            <div className="card-body">
              <label htmlFor="plCarrera" className="form-label fw-semibold small mb-1">1. Carrera</label>
              <select id="plCarrera" className="form-select mb-3" value={carreraId} onChange={(e) => elegirCarrera(e.target.value)}>
                <option value="">Elija una carrera…</option>
                {carreras.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
              </select>

              {carreraId && (
                <>
                  <div className="d-flex justify-content-between align-items-center mb-1">
                    <span className="form-label fw-semibold small mb-0">2. Asignatura</span>
                    <div className="form-check form-switch small mb-0">
                      <input
                        id="plFaltan"
                        className="form-check-input"
                        type="checkbox"
                        checked={soloFaltantes}
                        onChange={(e) => setSoloFaltantes(e.target.checked)}
                      />
                      <label htmlFor="plFaltan" className="form-check-label">Solo sin plan ({faltan})</label>
                    </div>
                  </div>
                  <div className="list-group small" style={{ maxHeight: 460, overflowY: "auto" }}>
                    {visibles.length === 0 && (
                      <div className="list-group-item text-muted">
                        {asignaturas.length === 0 ? "Cargando…" : "Todas las asignaturas tienen su plan cargado."}
                      </div>
                    )}
                    {visibles.map((a) => {
                      const real = !esGenerico(a.contenidosMinimos);
                      return (
                        <button
                          key={a.asignaturaId}
                          type="button"
                          className={`list-group-item list-group-item-action d-flex justify-content-between align-items-center gap-2 ${String(a.asignaturaId) === String(asignaturaId) ? "active" : ""}`}
                          onClick={() => elegirAsignatura(String(a.asignaturaId))}
                        >
                          <span>
                            {a.nombre}
                            <span className="d-block opacity-75">{a.nivel}</span>
                          </span>
                          <span className={`badge ${real ? "bg-success" : "bg-warning text-dark"}`}>{real ? "Cargado" : "Sin plan"}</span>
                        </button>
                      );
                    })}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Cargar y revisar */}
        <div className="col-lg-8">
          <div className="card shadow-sm border-0">
            <div className="card-body">
              {!asignatura ? (
                <p className="text-muted mb-0 py-5 text-center">Elija la carrera y la asignatura a la que pertenece el plan.</p>
              ) : (
                <>
                  <h2 className="h5 fw-bold mb-0">{asignatura.nombre}</h2>
                  <p className="small text-muted">
                    {asignatura.nivel} · {asignatura.creditos} créditos · {asignatura.horas} horas ·{" "}
                    {esGenerico(asignatura.contenidosMinimos) ? (
                      <span className="text-warning-emphasis">sin plan analítico real</span>
                    ) : (
                      <span className="text-success">plan cargado (puede reemplazarlo)</span>
                    )}
                  </p>

                  <label className="form-label fw-semibold small mb-1">3. PDF del plan analítico o sílabo</label>
                  <div className="d-flex flex-wrap gap-2 mb-1">
                    <input
                      key={claveArchivo}
                      type="file"
                      accept=".pdf,application/pdf"
                      className="form-control"
                      style={{ maxWidth: 420 }}
                      onChange={elegirArchivo}
                      aria-label="PDF del plan analítico"
                    />
                    <button type="button" className="btn btn-primary" onClick={leerPdf} disabled={!archivo || ocupado === "leer"}>
                      {ocupado === "leer" ? "Leyendo…" : "Leer PDF"}
                    </button>
                  </div>
                  <div className="form-text mb-3">
                    Formato de la UTA (plan analítico con «III CONTENIDO» o sílabo con unidades U.1, U.2…). También puede escribir o
                    pegar el plan directamente abajo.
                  </div>

                  {leido && !leido.vacio && (
                    <div className="alert alert-info py-2 small">
                      Se leyeron <strong>{leido.unidades}</strong> unidades
                      {leido.asignatura ? <> de «{leido.asignatura}»</> : null}
                      {leido.carrera ? <> ({leido.carrera})</> : null}. Revise el texto antes de guardar.
                    </div>
                  )}

                  <label htmlFor="plContenido" className="form-label fw-semibold small mb-1">
                    4. Objetivo y contenidos por unidad * <span className="fw-normal text-muted">({unidades} unidades)</span>
                  </label>
                  <textarea
                    id="plContenido"
                    className="form-control font-monospace small mb-1"
                    rows={10}
                    placeholder={"Objetivo general: …\nU.1 Nombre de la unidad. Temas: tema 1; tema 2; …\nU.2 …"}
                    value={contenido}
                    onChange={(e) => setContenido(e.target.value)}
                  />
                  <div className="form-text mb-3">Una línea por unidad, empezando con U.1, U.2… y los temas separados por «;».</div>

                  <label htmlFor="plResultados" className="form-label fw-semibold small mb-1">Resultados de aprendizaje</label>
                  <textarea
                    id="plResultados"
                    className="form-control small mb-1"
                    rows={4}
                    placeholder="Un resultado por línea"
                    value={resultados}
                    onChange={(e) => setResultados(e.target.value)}
                  />
                  <div className="form-text mb-3">Se usan para medir la profundidad (Art. 20). Si no los ingresa, se estima con los contenidos.</div>

                  <div className="d-flex justify-content-end gap-2">
                    <button type="button" className="btn btn-outline-secondary" onClick={() => elegirAsignatura(String(asignatura.asignaturaId))}>
                      Descartar cambios
                    </button>
                    <button
                      type="button"
                      className="btn btn-success fw-semibold px-4"
                      onClick={guardar}
                      disabled={ocupado === "guardar" || contenido.trim().length < 40 || unidades === 0}
                    >
                      {ocupado === "guardar" ? "Guardando…" : "Guardar plan analítico"}
                    </button>
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
