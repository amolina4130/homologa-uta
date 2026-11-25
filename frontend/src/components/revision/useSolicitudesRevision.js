import { useCallback, useEffect, useState } from "react";
import {
  obtenerDelegaciones,
  obtenerDocentes,
  obtenerSolicitudesPendientes,
} from "../../services/coordinadorService";

/**
 * Carga las solicitudes en revisión con sus delegaciones, y la lista de docentes.
 * `cargar()` devuelve { lista, mapa } para que cada página elija qué solicitud abrir.
 */
export function useSolicitudesRevision({ incluirEmitidos = false } = {}) {
  const [pendientes, setPendientes] = useState([]);
  const [delegacionesPorId, setDelegacionesPorId] = useState({});
  const [docentes, setDocentes] = useState([]);
  const [errorDocentes, setErrorDocentes] = useState("");
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");

  const cargar = useCallback(
    () =>
      obtenerSolicitudesPendientes({ incluirEmitidos })
        .then(async (data) => {
          const lista = Array.isArray(data) ? data : [];
          const pares = await Promise.all(
            lista.map((s) =>
              obtenerDelegaciones(s.solicitudId)
                .then((d) => [s.solicitudId, Array.isArray(d) ? d : []])
                .catch(() => [s.solicitudId, []])
            )
          );
          const mapa = Object.fromEntries(pares);
          setPendientes(lista);
          setDelegacionesPorId(mapa);
          setError("");
          return { lista, mapa };
        })
        .catch((e) => {
          setError(e.message || "No se pudieron cargar las solicitudes.");
          return null;
        })
        .finally(() => setCargando(false)),
    [incluirEmitidos]
  );

  const recargar = useCallback(() => {
    setCargando(true);
    return cargar();
  }, [cargar]);

  useEffect(() => {
    obtenerDocentes()
      .then((lista) => setDocentes(Array.isArray(lista) ? lista : []))
      .catch((e) => setErrorDocentes(e.message || "No se pudo cargar la lista de docentes."));
  }, []);

  const nombreDocente = useCallback(
    (correo) => {
      const d = docentes.find((x) => x.correo?.toLowerCase() === correo?.toLowerCase());
      return d?.nombre ? [d.titulo, d.nombre].filter(Boolean).join(" ") : correo || "-";
    },
    [docentes]
  );

  return {
    pendientes,
    setPendientes,
    delegacionesPorId,
    setDelegacionesPorId,
    docentes,
    errorDocentes,
    cargando,
    error,
    cargar,
    recargar,
    nombreDocente,
  };
}
