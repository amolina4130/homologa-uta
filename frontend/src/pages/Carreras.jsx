import { Fragment, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

import { GATEWAY_URL } from "../services/config";

// Mallas y planes analíticos que usa el agente. Por cada asignatura muestra si tiene su plan
// analítico real cargado (objetivo, unidades y resultados de aprendizaje) o solo un texto genérico,
// y permite abrirlo. Los datos son los mismos que lee la IA al comparar.

const esGenerico = (texto) => !texto || /Unidades:\s*fundamentos,\s*conceptos esenciales/i.test(texto);

function estadoPlan(a) {
  if (!a.contenidosMinimos) return { clave: "falta", texto: "Sin plan", clase: "secondary" };
  if (esGenerico(a.contenidosMinimos)) return { clave: "generico", texto: "Genérico", clase: "warning" };
  return { clave: "real", texto: "Cargado", clase: "success" };
}

function PlanAnalitico({ a }) {
  const lineas = (a.contenidosMinimos || "").split("\n").map((l) => l.trim()).filter(Boolean);
  const objetivo = lineas.find((l) => /^Objetivo general/i.test(l));
  const unidades = lineas.filter((l) => l !== objetivo);
  const resultados = (a.resultadosAprendizaje || "")
    .split(/\n|(?<=\.)\s*(?=[A-ZÁÉÍÓÚ])/)
    .map((r) => r.trim())
    .filter(Boolean);

  if (esGenerico(a.contenidosMinimos)) {
    return (
      <div className="small text-muted">
        No se cargó el plan analítico real de esta asignatura; el sistema tiene solo un texto genérico, así que la IA no
        puede comparar sus contenidos (las sugerencias que la involucren quedarán para revisión del docente).
      </div>
    );
  }

  return (
    <div className="row g-3 small">
      <div className="col-lg-7">
        {objetivo && <p className="mb-2"><strong>Objetivo general:</strong> {objetivo.replace(/^Objetivo general:\s*/i, "")}</p>}
        <div className="fw-semibold mb-1">Contenidos por unidad</div>
        <ul className="mb-0 ps-3">
          {unidades.map((u, i) => {
            const m = u.match(/^(U\.\d+)\s*(.*)$/);
            return (
              <li key={i} className="mb-1">
                {m ? (<><strong>{m[1]}</strong> {m[2].replace(/\s*;\s*/g, "; ")}</>) : u}
              </li>
            );
          })}
        </ul>
      </div>
      <div className="col-lg-5">
        <div className="fw-semibold mb-1">Resultados de aprendizaje</div>
        {resultados.length === 0 ? (
          <span className="text-muted">No registrados.</span>
        ) : (
          <ul className="mb-0 ps-3">
            {resultados.map((r, i) => <li key={i} className="mb-1">{r}</li>)}
          </ul>
        )}
      </div>
    </div>
  );
}

export default function Carreras() {
  const { rol } = useAuth();
  const esAdmin = rol === "ADMINISTRADOR";
  const [carreras, setCarreras] = useState([]);
  const [carreraSeleccionada, setCarreraSeleccionada] = useState(null);
  const [asignaturas, setAsignaturas] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [cargandoMalla, setCargandoMalla] = useState(false);
  const [error, setError] = useState("");
  const [abierta, setAbierta] = useState(null);
  const [filtro, setFiltro] = useState("todas");

  useEffect(() => {
    const token = sessionStorage.getItem("token");
    fetch(`${GATEWAY_URL}/carreras`, { headers: { Authorization: `Bearer ${token}` } })
      .then((res) => res.json())
      .then((data) => setCarreras([...data].sort((a, b) => a.id - b.id)))
      .catch(() => setError("No se pudieron cargar las carreras."))
      .finally(() => setCargando(false));
  }, []);

  const verMalla = (carrera) => {
    setCarreraSeleccionada(carrera);
    setAbierta(null);
    setAsignaturas([]);
    setCargandoMalla(true);
    setError("");
    const token = sessionStorage.getItem("token");
    fetch(`${GATEWAY_URL}/memoria/carreras/${carrera.id}/asignaturas`, { headers: { Authorization: `Bearer ${token}` } })
      .then((res) => {
        if (!res.ok) throw new Error();
        return res.json();
      })
      .then((data) => setAsignaturas(Array.isArray(data) ? data : data.asignaturas || []))
      .catch(() => setError("No se pudo cargar la malla de la carrera."))
      .finally(() => setCargandoMalla(false));
  };

  const conPlan = asignaturas.filter((a) => estadoPlan(a).clave === "real").length;
  const visibles = asignaturas.filter((a) => filtro === "todas" || estadoPlan(a).clave !== "real");

  return (
    <div className="container mt-4 pb-5">
      <div className="mb-3">
        <h1 className="h3 fw-bold mb-1">Mallas y planes analíticos</h1>
        <p className="text-muted mb-0">
          Asignaturas de cada carrera con su plan analítico (objetivo, contenidos y resultados de aprendizaje). Es la
          información que usa la IA para comparar sílabos.
        </p>
      </div>
      {error && <div className="alert alert-danger py-2">{error}</div>}
      <div className="row g-3">
        <div className="col-md-4 col-lg-3">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h2 className="h6 mb-0 fw-bold">Carreras</h2>
            </div>
            <div className="list-group list-group-flush">
              {cargando && <div className="p-3 text-center">Cargando…</div>}
              {carreras.map((c) => (
                <button
                  key={c.id}
                  className={`list-group-item list-group-item-action ${carreraSeleccionada?.id === c.id ? "active" : ""}`}
                  onClick={() => verMalla(c)}
                >
                  <span className="fw-semibold">{c.nombre}</span>
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="col-md-8 col-lg-9">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white d-flex flex-wrap justify-content-between align-items-center gap-2">
              <h2 className="h6 mb-0 fw-bold">
                {carreraSeleccionada ? `Malla: ${carreraSeleccionada.nombre}` : "Seleccione una carrera"}
              </h2>
              {carreraSeleccionada && asignaturas.length > 0 && (
                <span className="small">
                  <strong>{conPlan}</strong> de {asignaturas.length} asignaturas con plan analítico cargado
                </span>
              )}
            </div>
            <div className="card-body p-0">
              {!carreraSeleccionada ? (
                <div className="p-5 text-center text-muted">
                  <p className="mb-0">Elija una carrera para ver su malla y el estado de sus planes analíticos.</p>
                </div>
              ) : cargandoMalla ? (
                <div className="p-4 text-center text-muted">Cargando asignaturas…</div>
              ) : (
                <>
                  <div className="d-flex flex-wrap align-items-center gap-2 px-3 py-2 border-bottom small">
                    <span className="text-muted">Mostrar:</span>
                    <div className="btn-group btn-group-sm">
                      <button className={`btn ${filtro === "todas" ? "btn-primary" : "btn-outline-primary"}`} onClick={() => setFiltro("todas")}>
                        Todas ({asignaturas.length})
                      </button>
                      <button className={`btn ${filtro === "faltan" ? "btn-primary" : "btn-outline-primary"}`} onClick={() => setFiltro("faltan")}>
                        Sin plan real ({asignaturas.length - conPlan})
                      </button>
                    </div>
                    <span className="text-muted ms-auto">Haga clic en una asignatura para ver su plan analítico.</span>
                  </div>
                  <div className="table-responsive">
                    <table className="table table-hover align-middle mb-0">
                      <thead className="table-light">
                        <tr>
                          <th>Nivel</th>
                          <th>Asignatura</th>
                          <th className="text-center">Créditos</th>
                          <th className="text-center">Horas</th>
                          <th>Plan analítico</th>
                        </tr>
                      </thead>
                      <tbody>
                        {visibles.length === 0 && (
                          <tr><td colSpan="5" className="text-center py-4 text-muted">No hay asignaturas para mostrar.</td></tr>
                        )}
                        {visibles.map((a) => {
                          const estado = estadoPlan(a);
                          const abiertaFila = abierta === a.asignaturaId;
                          return (
                            <Fragment key={a.asignaturaId}>
                              <tr
                                role="button"
                                onClick={() => setAbierta(abiertaFila ? null : a.asignaturaId)}
                                className={abiertaFila ? "table-active" : ""}
                              >
                                <td>{a.nivel}</td>
                                <td>{a.nombre}</td>
                                <td className="text-center">{a.creditos}</td>
                                <td className="text-center">{a.horas}</td>
                                <td>
                                  <span className={`badge bg-${estado.clase}-subtle text-${estado.clase}-emphasis border border-${estado.clase}-subtle`}>
                                    {estado.texto}
                                  </span>
                                  <span className="text-muted ms-2 small">{abiertaFila ? "▴" : "▾"}</span>
                                </td>
                              </tr>
                              {abiertaFila && (
                                <tr>
                                  <td colSpan="5" className="bg-body-tertiary">
                                    <PlanAnalitico a={a} />
                                    {esAdmin && (
                                      <Link
                                        to={`/planes-analiticos?carrera=${carreraSeleccionada.id}&asignatura=${a.asignaturaId}`}
                                        className="btn btn-sm btn-outline-primary mt-2"
                                      >
                                        {estado.clave === "real" ? "Reemplazar plan analítico" : "Cargar plan analítico"}
                                      </Link>
                                    )}
                                  </td>
                                </tr>
                              )}
                            </Fragment>
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
