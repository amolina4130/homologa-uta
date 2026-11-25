// URL del gateway. Por defecto se usa /api en el mismo origen de la página: nginx (Docker) o
// Vite (npm run dev) la reenvían al gateway. Así no depende del puerto 8080 del equipo, que
// otros programas pueden ocupar (Oracle lo usa en 127.0.0.1), ni de CORS.
export const GATEWAY_URL =
  import.meta.env.VITE_GATEWAY_URL || `${window.location.origin}/api`;
