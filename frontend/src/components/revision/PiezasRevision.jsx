import { Link } from "react-router-dom";
import { diasDesde, etapaDe, nombreTitular, numeroDe } from "./etapas";
import "./revision.css";

// Delegar → sugerencias → informe final. El trámite termina al enviar el informe al Comité.
export const TOTAL_PASOS = 3;

/** Encabezado común: "Revisión · Paso N de 3", título, descripción y navegación entre pasos. */
export function EncabezadoPaso({ paso, titulo, descripcion, anterior, siguiente, onActualizar, cargando }) {
  return (
    <div className="d-flex flex-wrap justify-content-between align-items-end gap-3 mb-4">
      <div>
        <p className="text-muted small text-uppercase fw-semibold mb-1 dd-sobretitulo">
          Revisión · Paso {paso} de {TOTAL_PASOS}
        </p>
        <h1 className="h3 fw-bold mb-1">{titulo}</h1>
        <p className="text-muted mb-0">{descripcion}</p>
      </div>
      <div className="d-flex flex-wrap gap-2">
        {anterior && (
          <Link to={anterior.ruta} className="btn btn-outline-secondary">
            ← {anterior.texto}
          </Link>
        )}
        {onActualizar && (
          <button type="button" className="btn btn-outline-secondary" onClick={onActualizar} disabled={cargando}>
            ↻ Actualizar
          </button>
        )}
        {siguiente && (
          <Link to={siguiente.ruta} className="btn btn-outline-primary">
            {siguiente.texto} →
          </Link>
        )}
      </div>
    </div>
  );
}

/** Estados de carga, error y lista vacía comunes a los pasos. */
export function EstadoCarga({ cargando, error, vacio, onReintentar, children }) {
  if (error) {
    return (
      <div className="alert alert-danger d-flex justify-content-between align-items-center">
        <span>{error}</span>
        <button type="button" className="btn btn-sm btn-outline-danger" onClick={onReintentar}>
          Reintentar
        </button>
      </div>
    );
  }
  if (cargando) {
    return (
      <div className="d-flex justify-content-center align-items-center gap-2 py-5 text-muted">
        <div className="spinner-border spinner-border-sm text-primary" role="status" />
        Cargando solicitudes…
      </div>
    );
  }
  if (vacio) {
    return (
      <div className="card border-0 shadow-sm">
        <div className="text-center py-5 px-3">
          <div className="dd-vacio-icono mx-auto mb-3" aria-hidden="true">✓</div>
          <h2 className="h5 fw-bold">{vacio.titulo}</h2>
          <p className="text-muted mb-3">{vacio.texto}</p>
          <Link to="/inicio" className="btn btn-outline-primary">Volver al inicio</Link>
        </div>
      </div>
    );
  }
  return children;
}

/** Lista de solicitudes con filtros, buscador y la etapa de cada una. */
export function ListaRevision({
  filtros,
  conteo,
  filtro,
  onFiltro,
  texto,
  onTexto,
  items,
  delegacionesPorId,
  seleccionId,
  onSeleccionar,
  mostrarEspera = false,
  textoVacio = "Ninguna solicitud coincide.",
}) {
  return (
    <div className="card border-0 shadow-sm dd-lista">
      <div className="card-body pb-2">
        <div className="btn-group w-100 mb-2" role="group" aria-label="Filtrar solicitudes">
          {filtros.map((f) => (
            <button
              key={f.valor}
              type="button"
              className={`btn btn-sm ${filtro === f.valor ? "btn-primary" : "btn-outline-primary"}`}
              onClick={() => onFiltro(f.valor)}
            >
              {f.etiqueta} <span className="opacity-75">({conteo[f.valor] ?? 0})</span>
            </button>
          ))}
        </div>
        <input
          type="search"
          className="form-control form-control-sm"
          placeholder="Buscar por número, estudiante o carrera"
          value={texto}
          onChange={(e) => onTexto(e.target.value)}
          aria-label="Buscar solicitudes"
        />
      </div>

      <div className="dd-items" role="listbox" aria-label="Solicitudes">
        {items.length === 0 ? (
          <p className="text-muted small text-center py-4 mb-0">{textoVacio}</p>
        ) : (
          items.map((s) => {
            const etapa = etapaDe(s, delegacionesPorId[s.solicitudId]);
            const dias = diasDesde(s.fecha);
            const elegida = String(s.solicitudId) === String(seleccionId);
            return (
              <button
                key={s.solicitudId}
                type="button"
                role="option"
                aria-selected={elegida}
                className={`dd-item ${elegida ? "activa" : ""}`}
                onClick={() => onSeleccionar(s)}
              >
                <div className="d-flex justify-content-between align-items-baseline gap-2">
                  <span className="fw-bold text-primary">{numeroDe(s)}</span>
                  {mostrarEspera && dias !== null && (
                    <span className={`small text-nowrap ${dias >= 15 ? "text-danger fw-semibold" : "text-muted"}`}>
                      {dias === 0 ? "Hoy" : dias === 1 ? "1 día" : `${dias} días`}
                    </span>
                  )}
                </div>
                <div className="small fw-semibold text-truncate">
                  {nombreTitular(s) || <span className="text-muted fst-italic fw-normal">Titular sin identificar</span>}
                </div>
                <div className="small text-muted text-truncate">
                  {s.carreraOrigen} → {s.carreraDestino}
                </div>
                <span
                  className={`badge rounded-pill mt-1 bg-${etapa.clase}-subtle text-${etapa.clase}-emphasis border border-${etapa.clase}-subtle`}
                >
                  {etapa.texto}
                </span>
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}

/** Paso numerado dentro del panel de una solicitud. */
export function Paso({ numero, titulo, hecho, children }) {
  return (
    <section className="dd-paso">
      <div className="d-flex align-items-center gap-2 mb-3">
        <span className={`dd-paso-numero ${hecho ? "hecho" : ""}`} aria-hidden="true">
          {hecho ? "✓" : numero}
        </span>
        <h3 className="h6 fw-bold mb-0">{titulo}</h3>
      </div>
      {children}
    </section>
  );
}

/** Cabecera del panel derecho: número, titular, carreras y enlace al detalle. */
export function CabeceraSolicitud({ solicitud, extra }) {
  return (
    <div className="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3">
      <div>
        <h2 className="h4 fw-bold mb-0">{numeroDe(solicitud)}</h2>
        <div className="text-muted">
          {nombreTitular(solicitud) || "Titular sin identificar"} · {solicitud.carreraOrigen} →{" "}
          {solicitud.carreraDestino}
        </div>
        {extra}
      </div>
      <Link to={`/detalle-solicitud/${solicitud.solicitudId}`} className="btn btn-sm btn-outline-secondary">
        Ver detalle
      </Link>
    </div>
  );
}
