const REVISADAS = ["REVISADO", "YA_REVISADO"];

export const numeroSolicitud = (solicitud) =>
  solicitud?.numeroProceso ||
  `SOL-${String(solicitud?.solicitudId ?? "").padStart(4, "0")}`;

export const yaRevisada = (solicitud) =>
  REVISADAS.includes(solicitud?.estadoDelegacion);

export const esPendiente = (solicitud) =>
  Boolean(solicitud) && !yaRevisada(solicitud) && solicitud.puedeEvaluar !== false;

export const filtrarPendientes = (lista) =>
  (Array.isArray(lista) ? lista : []).filter(esPendiente);
