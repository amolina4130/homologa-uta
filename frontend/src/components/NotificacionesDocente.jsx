import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { obtenerSolicitudesDocente } from "../services/docenteService";
import { filtrarPendientes, numeroSolicitud } from "../utils/solicitudesDocente";

export default function NotificacionesDocente() {
  const [pendientes, setPendientes] = useState([]);
  const [abierto, setAbierto] = useState(false);
  const contenedor = useRef(null);

  const cargar = useCallback(() => {
    let activo = true;
    obtenerSolicitudesDocente()
      .then((lista) => {
        if (activo) setPendientes(filtrarPendientes(lista));
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
      if (
        contenedor.current &&
        !contenedor.current.contains(evento.target)
      ) {
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
        aria-label={`Notificaciones: ${total} solicitud(es) pendiente(s) de revisar`}
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
        style={{ minWidth: "300px" }}
      >
        <li>
          <h6 className="dropdown-header fw-bold text-dark">Notificaciones</h6>
        </li>
        {total === 0 ? (
          <li>
            <span className="dropdown-item-text small text-muted">
              No tienes solicitudes pendientes de revisar.
            </span>
          </li>
        ) : (
          pendientes.map((solicitud) => (
            <li key={solicitud.solicitudId}>
              <Link
                className="dropdown-item small"
                to={`/evaluar/${solicitud.solicitudId}`}
                onClick={() => setAbierto(false)}
              >
                <span className="fw-bold d-block">
                  {numeroSolicitud(solicitud)}
                </span>
                <span className="text-muted">
                  Pendiente de revisar ·{" "}
                  {solicitud.carreraOrigen || "Origen"} →{" "}
                  {solicitud.carreraDestino || "Destino"}
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
            to="/solicitudes-docente"
            onClick={() => setAbierto(false)}
          >
            Ver todas mis revisiones
          </Link>
        </li>
      </ul>
    </div>
  );
}
