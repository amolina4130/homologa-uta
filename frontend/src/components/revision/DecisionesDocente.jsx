import { useState } from "react";
import { TarjetaSugerencia } from "./SugerenciasIA";
import { decisionIADe, requiereJustificacion } from "./reglasDecision";
import "./revision.css";

// Decisión del docente sobre cada sugerencia de la IA, en filas compactas agrupadas por lo que
// recomendó la IA. Aceptar la sugerencia es un clic; cambiarla o decidir una "en revisión" pide
// justificación. El detalle (temas coincidentes, diferencias, regla) se abre bajo demanda.

const GRUPOS = [
  {
    clave: "HOMOLOGABLE",
    titulo: "La IA recomienda homologar",
    ayuda: "Cumplen al menos el 80 % en contenido, profundidad y carga horaria. Confirme o rechace cada una.",
    clase: "success",
    abierto: true,
  },
  {
    clave: "REQUIERE_REVISION",
    titulo: "Requieren su decisión",
    ayuda: "Están cerca del umbral o falta información. Abra el detalle, decida y justifique.",
    clase: "warning",
    abierto: true,
  },
  {
    clave: "NO_HOMOLOGABLE",
    titulo: "Sin equivalencia suficiente",
    ayuda: "Quedan como «No homologar». Solo ábralas si considera que alguna sí corresponde.",
    clase: "danger",
    abierto: false,
  },
];

function Chip({ etiqueta, valor }) {
  if (valor == null) return null;
  const ok = valor >= 80;
  return (
    <span className={`dd-chip ${ok ? "ok" : valor >= 65 ? "cerca" : "bajo"}`} title={`${etiqueta}: ${valor}%`}>
      {etiqueta[0]} {valor}%
    </span>
  );
}

function Fila({ a, d, onCambiar, bloqueado, onModificarDestino }) {
  const [abierta, setAbierta] = useState(false);
  const p = a.propuesta || a;
  const pide = requiereJustificacion(a, d);
  const sinDecidir = d?.homologable == null;

  return (
    <div className={`dd-fila ${sinDecidir ? "pendiente" : ""}`}>
      <div className="dd-fila-principal">
        <div className="dd-fila-materias">
          <span className="fw-semibold">{a.asignaturaOrigen}</span>
          {a.nivelOrigen && <span className="text-muted small"> · {a.nivelOrigen}</span>}
          <span className="mx-2 text-muted" aria-hidden="true">→</span>
          <span className="fw-semibold">{a.asignaturaDestino}</span>
          {a.nivelDestino && <span className="text-muted small"> · {a.nivelDestino}</span>}
        </div>
        <div className="dd-fila-chips">
          <Chip etiqueta="Contenido" valor={p.porcentajeContenido} />
          <Chip etiqueta="Profundidad" valor={p.porcentajeProfundidad} />
          <Chip etiqueta="Horas" valor={p.porcentajeHoras} />
        </div>
        <div className="btn-group btn-group-sm" role="group" aria-label={`Decisión sobre ${a.asignaturaDestino}`}>
          <button
            type="button"
            className={`btn ${d?.homologable === true ? "btn-success" : "btn-outline-success"}`}
            onClick={() => onCambiar(a.id, "homologable", true)}
            disabled={bloqueado}
          >
            Homologar
          </button>
          <button
            type="button"
            className={`btn ${d?.homologable === false ? "btn-danger" : "btn-outline-danger"}`}
            onClick={() => onCambiar(a.id, "homologable", false)}
            disabled={bloqueado}
          >
            No
          </button>
        </div>
        <button
          type="button"
          className="btn btn-link btn-sm p-0 text-decoration-none"
          onClick={() => setAbierta((v) => !v)}
          aria-expanded={abierta}
        >
          {abierta ? "Ocultar ▴" : "Detalle ▾"}
        </button>
      </div>

      {pide && (
        <input
          className={`form-control form-control-sm mt-2 ${!bloqueado && !(d?.justificacion || "").trim() ? "border-warning" : ""}`}
          placeholder={
            decisionIADe(a) === "REQUIERE_REVISION"
              ? "Justifique su decisión (obligatorio)"
              : "Usted cambió la sugerencia de la IA: justifique por qué (obligatorio)"
          }
          value={d?.justificacion || ""}
          onChange={(e) => onCambiar(a.id, "justificacion", e.target.value)}
          disabled={bloqueado}
          aria-label={`Justificación para ${a.asignaturaDestino}`}
        />
      )}

      {abierta && (
        <div className="mt-2">
          <TarjetaSugerencia a={a}>
            {!pide && !bloqueado && (
              <input
                className="form-control form-control-sm mt-2"
                placeholder="Observación (opcional). Si la deja vacía se registra «Acepta la sugerencia de la IA»."
                value={d?.justificacion || ""}
                onChange={(e) => onCambiar(a.id, "justificacion", e.target.value)}
                aria-label={`Observación para ${a.asignaturaDestino}`}
              />
            )}
            {!bloqueado && onModificarDestino && (
              <button type="button" className="btn btn-link btn-sm px-0 mt-1" onClick={() => onModificarDestino(a)}>
                La materia sí es homologable pero a otra asignatura de destino →
              </button>
            )}
          </TarjetaSugerencia>
        </div>
      )}
    </div>
  );
}

/** Barra para decidir todas las filas de un grupo de una vez, con una justificación común. */
function AccionesGrupo({ grupo, lista, decisiones, onCambiarVarios }) {
  const [texto, setTexto] = useState("");
  const aplicar = (homologable) => {
    // Pide justificación si alguna fila cambia lo que dijo la IA o si la IA pidió revisión.
    const necesita = lista.some((a) => requiereJustificacion(a, { homologable }));
    const sinTexto = lista.filter((a) => !(decisiones[a.id]?.justificacion || "").trim());
    if (necesita && !texto.trim() && sinTexto.length > 0) return false;
    onCambiarVarios(
      lista.map((a) => a.id),
      (a, d) => ({
        homologable,
        justificacion: (d?.justificacion || "").trim() || (requiereJustificacion(a, { homologable }) ? texto.trim() : ""),
      })
    );
    return true;
  };
  const [falta, setFalta] = useState(false);
  const pulsar = (h) => setFalta(!aplicar(h));
  const esRevision = grupo.clave === "REQUIERE_REVISION";

  return (
    <div className="dd-acciones-grupo">
      <span className="small fw-semibold">Todas las de este grupo:</span>
      <button type="button" className="btn btn-sm btn-outline-success" onClick={() => pulsar(true)}>Homologar</button>
      <button type="button" className="btn btn-sm btn-outline-danger" onClick={() => pulsar(false)}>No homologar</button>
      <input
        className={`form-control form-control-sm ${falta && !texto.trim() ? "is-invalid" : ""}`}
        placeholder={esRevision ? "Justificación común (obligatoria)" : "Justificación común (si cambia lo que sugirió la IA)"}
        value={texto}
        onChange={(e) => {
          setTexto(e.target.value);
          setFalta(false);
        }}
        aria-label={`Justificación común para ${grupo.titulo}`}
      />
      {falta && !texto.trim() && <span className="small text-danger w-100">Escriba la justificación común para aplicar la decisión a todas.</span>}
    </div>
  );
}

export default function DecisionesDocente({ filas, decisiones, onCambiar, onCambiarVarios, bloqueado, onModificarDestino }) {
  const [abiertos, setAbiertos] = useState(() => Object.fromEntries(GRUPOS.map((g) => [g.clave, g.abierto])));

  return (
    <div className="d-flex flex-column gap-3">
      {GRUPOS.map((g) => {
        const lista = filas.filter((a) => (decisionIADe(a) || "NO_HOMOLOGABLE") === g.clave);
        if (lista.length === 0) return null;
        const homologa = lista.filter((a) => decisiones[a.id]?.homologable === true).length;
        const pendientes = lista.filter((a) => decisiones[a.id]?.homologable == null).length;
        const abierto = abiertos[g.clave];
        return (
          <section key={g.clave} className={`dd-grupo border-${g.clase}`}>
            <button
              type="button"
              className="dd-grupo-cabecera"
              onClick={() => setAbiertos((s) => ({ ...s, [g.clave]: !s[g.clave] }))}
              aria-expanded={abierto}
            >
              <span className={`badge rounded-pill bg-${g.clase}`}>{lista.length}</span>
              <span className="fw-semibold">{g.titulo}</span>
              <span className="small text-muted ms-auto">
                {pendientes > 0 ? `${pendientes} sin decidir · ` : ""}
                usted homologa {homologa}
              </span>
              <span className="text-muted" aria-hidden="true">{abierto ? "▴" : "▾"}</span>
            </button>
            {abierto && (
              <div className="dd-grupo-cuerpo">
                <p className="small text-muted mb-2">{g.ayuda}</p>
                {!bloqueado && onCambiarVarios && lista.length > 1 && (
                  <AccionesGrupo grupo={g} lista={lista} decisiones={decisiones} onCambiarVarios={onCambiarVarios} />
                )}
                {lista.map((a) => (
                  <Fila
                    key={a.id}
                    a={a}
                    d={decisiones[a.id]}
                    onCambiar={onCambiar}
                    bloqueado={bloqueado}
                    onModificarDestino={onModificarDestino}
                  />
                ))}
              </div>
            )}
          </section>
        );
      })}
      {filas.length === 0 && <p className="text-muted small mb-0">La IA no generó sugerencias para esta solicitud.</p>}
    </div>
  );
}
