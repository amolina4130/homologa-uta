import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { obtenerSolicitudesPendientes } from "../services/coordinadorService";
import { numeroSolicitud } from "../utils/solicitudesDocente";

export default function NotificacionesCoordinador() {
  const [pendientes, setPendientes] = useState([]);
  const [abierto, setAbierto] = useState(false);
  const contenedor = useRef(null);

  const cargar = useCallback(() => {
    let activo = true;
    obtenerSolicitudesPendientes()
      .then((lista) => {
        if (activo) setPendientes(Array.isArray(lista) ? lista : []);
      })
      .catch(() => {
        if (activo) setPendientes([]);
      });
    return () => {
      activo = false;
    };
  }, []);

  useEffect(() => {
    const cancelar = cargar();
    const intervalo = setInterval(cargar, 30000);

    const cerrarAlClicExterior = (evento) => {
      if (contenedor.current && !contenedor.current.contains(evento.target)) {
        setAbierto(false);
      }
    };
    document.addEventListener("mousedown", cerrarAlClicExterior);

    return () => {
      cancelar?.();
      clearInterval(intervalo);
      document.removeEventListener("mousedown", cerrarAlClicExterior);
    };
  }, [cargar]);

  const total = pendientes.length;

  return (
    <div className="dropdown" ref={contenedor}>
      <button
        type="button"
        className="btn btn-outline-light btn-sm position-relative"
        aria-label={`Notificaciones: ${total} solicitud(es) pendiente(s) de revisión`}
        aria-expanded={abierto}
        title="Notificaciones"
        onClick={() => setAbierto((valor) => !valor)}
      >
        <span aria-hidden="true">🔔</span>
        {total > 0 && (
          <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">
            {total > 9 ? "9+" : total}
            <span className="visually-hidden">solicitudes pendientes</span>
          </span>
        )}
      </button>

      <ul
        className={`dropdown-menu dropdown-menu-end shadow ${abierto ? "show" : ""}`}
        style={{ minWidth: "320px" }}
      >
        <li>
          <h6 className="dropdown-header fw-bold text-dark">Solicitudes pendientes de revisión</h6>
        </li>
        {total === 0 ? (
          <li>
            <span className="dropdown-item-text small text-muted">
              No hay solicitudes pendientes de revisión.
            </span>
          </li>
        ) : (
          pendientes.slice(0, 8).map((s) => (
            <li key={s.solicitudId}>
              <Link
                className="dropdown-item small"
                to={`/detalle-solicitud/${s.solicitudId}`}
                onClick={() => setAbierto(false)}
              >
                <span className="fw-bold d-block">
                  {s.numeroProceso || numeroSolicitud(s)}
                </span>
                <span className="text-muted">
                  Pendiente de revisión · {s.carreraOrigen || "Origen"} → {s.carreraDestino || "Destino"}
                </span>
              </Link>
            </li>
          ))
        )}
        <li>
          <hr className="dropdown-divider" />
        </li>
        <li>
          <Link
            className="dropdown-item small text-center fw-semibold"
            to="/delegar-docentes"
            onClick={() => setAbierto(false)}
          >
            Ver solicitudes pendientes de revisión
          </Link>
        </li>
      </ul>
    </div>
  );
}