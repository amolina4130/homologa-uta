// Utilidades compartidas por los pasos de la revisión del Coordinador
// (delegar → sugerencias → informe final, que se envía al Comité y cierra el trámite).

export const NOMBRE_SIN_IDENTIFICAR = "Estudiante sin identificar";
export const CEDULA_SIN_IDENTIFICAR = "-";
export const ESTADOS_REVISADOS = ["REVISADO", "YA_REVISADO"];

export const numeroDe = (s) =>
  s?.numeroProceso || `SOL-${String(s?.solicitudId ?? s?.id ?? "").padStart(4, "0")}`;

export const datoReal = (valor, vacio) => (valor && valor !== vacio ? valor : "");

export const nombreTitular = (s) => datoReal(s?.estudiante, NOMBRE_SIN_IDENTIFICAR);
export const cedulaTitular = (s) => datoReal(s?.estudianteCedula, CEDULA_SIN_IDENTIFICAR);

export const esRevisada = (d) => ESTADOS_REVISADOS.includes((d?.estado || "").toUpperCase());

export const fechaCorta = (valor) =>
  valor ? new Date(valor).toLocaleDateString("es-EC", { day: "2-digit", month: "short" }) : "-";

export const diasDesde = (valor) =>
  valor ? Math.max(0, Math.floor((Date.now() - new Date(valor).getTime()) / 864e5)) : null;

export const normalizar = (texto) =>
  String(texto ?? "")
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();

export const coincideBusqueda = (s, texto) => {
  const q = normalizar(texto.trim());
  if (!q) return true;
  return [numeroDe(s), s.estudiante, s.estudianteCedula, s.carreraOrigen, s.carreraDestino].some((c) =>
    normalizar(c).includes(q)
  );
};

// Con incluirEmitidos, la lista del backend trae también las solicitudes con informe final
// (trámite terminado). Su `estado` es el del análisis.
export const tieneInformeFinal = (s) => (s?.estado || "").toUpperCase() === "INFORME_EMITIDO";

/** Etapa de la revisión de una solicitud, con la etiqueta y el color para mostrarla. */
export const etapaDe = (solicitud, delegaciones) => {
  if (tieneInformeFinal(solicitud)) return { clave: "emitido", clase: "primary", texto: "Informe enviado" };
  if (!delegaciones) return { clave: "cargando", clase: "light", texto: "…" };
  if (delegaciones.length === 0) return { clave: "sin", clase: "warning", texto: "Sin delegar" };
  if (delegaciones.some(esRevisada)) return { clave: "con", clase: "success", texto: "Sugerencia recibida" };
  return {
    clave: "esperando",
    clase: "info",
    texto: `Esperando ${delegaciones.length === 1 ? "al docente" : "a los docentes"}`,
  };
};

/** Siguiente paso del trámite para una solicitud, para enlazar desde el detalle o la lista. */
export const siguientePaso = (estadoSolicitud, etapa, id) => {
  const q = `?solicitudId=${id}`;
  // Con el informe final enviado al Comité el trámite terminó.
  if (["INFORME_EMITIDO", "RESUELTA"].includes(estadoSolicitud) || etapa === "emitido") return null;
  if (estadoSolicitud === "REGISTRADA")
    return { ruta: `/nueva-solicitud${q}`, texto: "Completar la solicitud" };
  if (etapa === "con") return { ruta: `/informe-final${q}`, texto: "Emitir informe final" };
  if (etapa === "esperando") return { ruta: `/informes-sugeridos${q}`, texto: "Ver estado de la revisión" };
  return { ruta: `/delegar-docentes${q}`, texto: "Delegar a docentes" };
};

/** "Procede · total / parcial" o "No procede" según cuántas asignaturas se homologan. */
export const resultadoHomologacion = (homologables, total) =>
  total === 0
    ? { clase: "bg-secondary", texto: "Sin asignaturas" }
    : homologables === total
      ? { clase: "bg-success", texto: "Procede · total" }
      : homologables > 0
        ? { clase: "bg-warning text-dark", texto: "Procede · parcial" }
        : { clase: "bg-danger", texto: "No procede" };
