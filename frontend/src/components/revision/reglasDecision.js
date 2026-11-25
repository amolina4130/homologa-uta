// Reglas de la decisión del docente sobre una sugerencia de la IA.

export const decisionIADe = (a) => (a?.propuesta?.decision || a?.decision || "").toUpperCase();

/** true si la decisión del docente necesita justificación propia (cambia la IA o la IA pidió revisión). */
export const requiereJustificacion = (a, d) => {
  const ia = decisionIADe(a);
  if (ia === "REQUIERE_REVISION") return true;
  if (d?.homologable == null) return false;
  return (ia === "HOMOLOGABLE") !== Boolean(d.homologable);
};

export const JUSTIFICACION_ACEPTA = "Acepta la sugerencia de la IA.";
