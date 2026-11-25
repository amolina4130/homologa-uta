import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { obtenerSolicitudesPendientes } from "../services/coordinadorService";
import { numeroSolicitud } from "../utils/solicitudesDocente";
import { useAuth } from "../context/AuthContext";
import { useNotificacionesLeidas } from "../utils/notificacionesLeidas";

// "Hoy", "Ayer", "Hace 5 días" o la fecha corta si ya pasó un mes.
const fechaRelativa = (valor) => {
  if (!valor) return "";
  const dias = Math.floor((Date.now() - new Date(valor).getTime()) / 864e5);
  if (dias <= 0) return "Hoy";
  if (dias === 1) return "Ayer";
  if (dias < 30) return `Hace ${dias} días`;
  return new Date(valor).toLocaleDateString("es-EC", { day: "2-digit", month: "short" });
};

export default function NotificacionesCoordinador() {
  const { usuario } = useAuth();
  const { esLeida, marcarLeida, marcarTodas, conservarSolo } = useNotificacionesLeidas(usuario?.correo);
  const [pendientes, setPendientes] = useState([]);
  const [abierto, setAbierto] = useState(false);
  const contenedor = useRef(null);
  const { pathname } = useLocation();

  const cargar = useCallback(() => {
    let activo = true;
    obtenerSolicitudesPendientes()
      .then((lista) => {
        if (!activo) return;
        const datos = Array.isArray(lista) ? lista : [];
        setPendientes(datos);
        conservarSolo(datos.map((s) => s.solicitudId));
      })
      .catch(() => {
        // Si falla la consulta se conserva lo último que se mostró.
      });
    return () => {
      activo = false;
    };
  }, [conservarSolo]);

  // Al cambiar de página se vuelve a consultar: las acciones del coordinador (delegar,
  // emitir, resolver) cambian la lista y no conviene esperar al siguiente intervalo.
  useEffect(() => cargar(), [cargar, pathname]);

  useEffect(() => {
    const intervalo = setInterval(cargar, 30000);

    const cerrarAlClicExterior = (evento) => {
      if (contenedor.current && !contenedor.current.contains(evento.target)) {
        setAbierto(false);
      }
    };
    document.addEventListener("mousedown", cerrarAlClicExterior);

    return () => {
      clearInterval(intervalo);
      document.removeEventListener("mousedown", cerrarAlClicExterior);
    };
  }, [cargar]);

  // Las no leídas primero; dentro de cada grupo se mantiene el orden del servidor (más antiguas arriba).
  const noLeidas = pendientes.filter((s) => !esLeida(s.solicitudId));
  const leidas = pendientes.filter((s) => esLeida(s.solicitudId));
  const ordenadas = [...noLeidas, ...leidas];
  const total = noLeidas.length;

  return (
    <div className="dropdown" ref={contenedor}>
      <button
        type="button"
        className="boton-circulo boton-icono"
        aria-label={`Notificaciones: ${total} sin leer`}
        aria-expanded={abierto}
        title="Notificaciones"
        onClick={() => setAbierto((valor) => !valor)}
      >
        <span aria-hidden="true">🔔</span>
        {total > 0 && (
          <span className="contador" aria-hidden="true">{total > 9 ? "9+" : total}</span>
        )}
      </button>

      {abierto && (
        <div className="dropdown-menu menu-desplegable menu-derecha panel-notificaciones show">
          <div className="d-flex justify-content-between align-items-center gap-2 px-3 py-2 border-bottom">
            <span className="fw-bold">
              Notificaciones
              {total > 0 && <span className="badge rounded-pill bg-danger ms-2">{total}</span>}
            </span>
            {total > 0 && (
              <button
                type="button"
                className="btn btn-link btn-sm p-0 text-decoration-none"
                onClick={() => marcarTodas(noLeidas.map((s) => s.solicitudId))}
              >
                Marcar todas como leídas
              </button>
            )}
          </div>

          {pendientes.length === 0 ? (
            <div className="text-center text-muted small px-3 py-4">
              <div className="fs-3 mb-1" aria-hidden="true">✓</div>
              No hay solicitudes esperando su revisión.
            </div>
          ) : (
            <div className="lista">
              {ordenadas.map((s) => {
                const leida = esLeida(s.solicitudId);
                return (
                  <div key={s.solicitudId} className={`notificacion d-flex align-items-start gap-2 ${leida ? "leida" : ""}`}>
                    <span className="punto-no-leida" aria-hidden="true" />
                    <Link
                      className="flex-grow-1 text-reset text-decoration-none min-w-0"
                      to={`/detalle-solicitud/${s.solicitudId}`}
                      onClick={() => {
                        marcarLeida(s.solicitudId);
                        setAbierto(false);
                      }}
                    >
                      <div className="d-flex justify-content-between align-items-baseline gap-2">
                        <span className="fw-bold text-primary">{s.numeroProceso || numeroSolicitud(s)}</span>
                        <span className="small text-muted text-nowrap">{fechaRelativa(s.fecha)}</span>
                      </div>
                      <div className="small fw-semibold text-truncate">
                        {s.estudiante && s.estudiante !== "Estudiante sin identificar"
                          ? s.estudiante
                          : <span className="text-muted fst-italic fw-normal">Titular sin identificar</span>}
                      </div>
                      <div className="small text-muted text-truncate">
                        Pendiente de revisión · {s.carreraOrigen || "Origen"} → {s.carreraDestino || "Destino"}
                      </div>
                    </Link>
                    {!leida && (
                      <button
                        type="button"
                        className="boton-marcar"
                        title="Marcar como leída"
                        aria-label={`Marcar ${s.numeroProceso || numeroSolicitud(s)} como leída`}
                        onClick={() => marcarLeida(s.solicitudId)}
                      >
                        ✓
                      </button>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
