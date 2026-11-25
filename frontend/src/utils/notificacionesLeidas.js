import { useCallback, useState } from "react";

// Las notificaciones leídas se recuerdan en este navegador, por usuario. El backend no
// guarda este estado: si el usuario entra desde otro equipo, las verá como no leídas.
const claveDe = (usuario) => `notificaciones-leidas:${usuario || "anonimo"}`;

const leer = (clave) => {
  try {
    const valor = JSON.parse(localStorage.getItem(clave) || "[]");
    return new Set(Array.isArray(valor) ? valor.map(String) : []);
  } catch {
    return new Set();
  }
};

const guardar = (clave, conjunto) => {
  try {
    localStorage.setItem(clave, JSON.stringify([...conjunto]));
  } catch {
    // Sin almacenamiento disponible (modo privado estricto): solo dura la sesión.
  }
};

export function useNotificacionesLeidas(usuario) {
  const clave = claveDe(usuario);
  const [leidas, setLeidas] = useState(() => leer(clave));

  const actualizar = useCallback(
    (cambiar) =>
      setLeidas((prev) => {
        const siguiente = cambiar(new Set(prev));
        guardar(clave, siguiente);
        return siguiente;
      }),
    [clave]
  );

  const marcarLeida = useCallback((id) => actualizar((s) => s.add(String(id))), [actualizar]);

  const marcarTodas = useCallback(
    (ids) => actualizar((s) => {
      ids.forEach((id) => s.add(String(id)));
      return s;
    }),
    [actualizar]
  );

  // Olvida las que ya no están en la lista (p. ej. solicitudes ya atendidas) para que,
  // si una vuelve a quedar pendiente, aparezca otra vez como nueva.
  const conservarSolo = useCallback(
    (ids) => {
      const vigentes = new Set(ids.map(String));
      actualizar((s) => new Set([...s].filter((id) => vigentes.has(id))));
    },
    [actualizar]
  );

  const esLeida = useCallback((id) => leidas.has(String(id)), [leidas]);

  return { esLeida, marcarLeida, marcarTodas, conservarSolo };
}
