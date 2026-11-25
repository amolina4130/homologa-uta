import "./revision.css";

// Muestra la sugerencia ORIGINAL de la IA por asignatura, con todo lo que pide el proceso:
// origen y semestre, destino y semestre, coincidencias, diferencias, porcentajes, horas y
// créditos, regla aplicada, recomendación y justificación. Es solo lectura: la decisión del
// docente y la del coordinador se registran aparte.

const DECISIONES = {
  HOMOLOGABLE: { texto: "Recomienda homologar", clase: "success" },
  NO_HOMOLOGABLE: { texto: "No recomienda homologar", clase: "danger" },
  REQUIERE_REVISION: { texto: "Requiere revisión", clase: "warning" },
};

const UMBRAL = 80;

const decisionIA = (d) =>
  DECISIONES[(d || "").toUpperCase()] || { texto: d || "Sin decisión", clase: "secondary" };

const lista = (texto) =>
  String(texto || "")
    .split(";")
    .map((t) => t.trim())
    .filter(Boolean);

function Porcentaje({ etiqueta, valor, sinDato }) {
  if (sinDato) {
    return (
      <div className="sg-pct">
        <span className="small text-muted">{etiqueta}</span>
        <span className="small text-muted">sin dato</span>
      </div>
    );
  }
  const ok = valor >= UMBRAL;
  return (
    <div className="sg-pct">
      <span className="small text-muted">{etiqueta}</span>
      <span className={`fw-semibold ${ok ? "text-success" : "text-danger"}`}>{valor}%</span>
      <div className="progress" style={{ height: 4 }}>
        <div className={`progress-bar ${ok ? "bg-success" : "bg-danger"}`} style={{ width: `${Math.min(valor, 100)}%` }} />
      </div>
    </div>
  );
}

/** Una fila: `a` puede venir del detalle del coordinador/docente (con `propuesta`) o del resultado. */
export function TarjetaSugerencia({ a, children, compacta = false }) {
  const p = a.propuesta || a;
  const decision = decisionIA(p.decision);
  const coincidencias = lista(a.contenidosCoincidentes);
  const diferencias = lista(a.diferencias);
  const sinHoras = !a.horasOrigen || !a.horasDestino;
  const deDocente = (a.propuestaPor || "IA") === "DOCENTE";

  return (
    <div className={`sg-tarjeta border-start border-4 border-${decision.clase}`}>
      <div className="d-flex flex-wrap justify-content-between align-items-start gap-2">
        <div className="sg-par">
          <div>
            <div className="small text-muted">Origen{a.nivelOrigen ? ` · ${a.nivelOrigen} semestre` : ""}</div>
            <div className="fw-semibold">{a.asignaturaOrigen}</div>
          </div>
          <span className="sg-flecha" aria-hidden="true">→</span>
          <div>
            <div className="small text-muted">Destino{a.nivelDestino ? ` · ${a.nivelDestino} semestre` : ""}</div>
            <div className="fw-semibold">{a.asignaturaDestino}</div>
          </div>
        </div>
        <div className="text-end">
          <span className={`badge rounded-pill bg-${decision.clase}-subtle text-${decision.clase}-emphasis border border-${decision.clase}-subtle`}>
            {deDocente ? "Propuesta del docente" : `IA: ${decision.texto}`}
          </span>
        </div>
      </div>

      {!deDocente && (
        <div className="sg-pcts my-2">
          <Porcentaje etiqueta="Contenido" valor={p.porcentajeContenido ?? 0} />
          <Porcentaje etiqueta="Profundidad" valor={p.porcentajeProfundidad ?? 0} />
          <Porcentaje etiqueta="Carga horaria" valor={p.porcentajeHoras ?? 0} sinDato={sinHoras && !p.porcentajeHoras} />
          <div className="sg-pct">
            <span className="small text-muted">Horas · créditos</span>
            <span className="small">
              {a.horasOrigen ?? "—"} h / {a.horasDestino ?? "—"} h · {a.creditosOrigen ?? "—"} / {a.creditosDestino ?? "—"} cr.
            </span>
          </div>
        </div>
      )}

      <p className="small mb-2">{p.justificacion}</p>

      {!compacta && (coincidencias.length > 0 || diferencias.length > 0) && (
        <div className="row g-2 mb-2">
          {coincidencias.length > 0 && (
            <div className="col-md-6">
              <div className="small fw-semibold text-success mb-1">Contenidos coincidentes</div>
              <ul className="small mb-0 ps-3 sg-lista">
                {coincidencias.map((c) => <li key={c}>{c}</li>)}
              </ul>
            </div>
          )}
          {diferencias.length > 0 && (
            <div className="col-md-6">
              <div className="small fw-semibold text-danger mb-1">Diferencias e información faltante</div>
              <ul className="small mb-0 ps-3 sg-lista">
                {diferencias.map((c) => <li key={c}>{c}</li>)}
              </ul>
            </div>
          )}
        </div>
      )}

      {a.reglaAplicada && (
        <div className="small text-muted"><strong>Regla aplicada:</strong> {a.reglaAplicada}</div>
      )}

      {children}
    </div>
  );
}

/** Lista completa con un resumen arriba. */
export default function SugerenciasIA({ asignaturas = [], compacta = false }) {
  const homologables = asignaturas.filter((a) => (a.propuesta || a).decision === "HOMOLOGABLE").length;
  const revision = asignaturas.filter((a) => (a.propuesta || a).decision === "REQUIERE_REVISION").length;
  return (
    <div>
      <div className="d-flex flex-wrap gap-3 small mb-3">
        <span><strong>{asignaturas.length}</strong> comparaciones</span>
        <span className="text-success"><strong>{homologables}</strong> recomendadas para homologar</span>
        <span className="text-warning-emphasis"><strong>{revision}</strong> requieren revisión</span>
        <span className="text-danger"><strong>{asignaturas.length - homologables - revision}</strong> sin equivalencia suficiente</span>
      </div>
      <div className="d-flex flex-column gap-2">
        {asignaturas.map((a, i) => (
          <TarjetaSugerencia key={a.id ?? i} a={a} compacta={compacta} />
        ))}
      </div>
      <p className="small text-muted mt-3 mb-0">
        La IA solo sugiere: no aprueba asignaturas. Umbral de la normativa: {UMBRAL}% en contenido, profundidad y carga horaria.
      </p>
    </div>
  );
}
