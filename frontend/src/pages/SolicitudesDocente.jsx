import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { obtenerSolicitudesDocente } from "../services/docenteService";
import { filtrarPendientes, numeroSolicitud } from "../utils/solicitudesDocente";

const formatoFecha = (valor) =>
  valor ? new Date(valor).toLocaleDateString("es-EC") : "-";

const YA_REVISADO = ["YA_REVISADO", "REVISADO"];

export default function SolicitudesDocente() {
  const [solicitudes, setSolicitudes] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let activo = true;
    obtenerSolicitudesDocente()
      .then((data) => {
        if (activo) setSolicitudes(Array.isArray(data) ? data : []);
      })
      .catch((e) => {
        if (activo) setError(e.message || "No se pudieron cargar las solicitudes asignadas.");
      })
      .finally(() => {
        if (activo) setCargando(false);
      });

    return () => {
      activo = false;
    };
  }, []);

  const pendientes = filtrarPendientes(solicitudes);

  return (
    <div className="container mt-4">
      <div className="card shadow-sm border-0">
        <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
          <div>
            <h4 className="mb-0 fw-bold">REVISIÓN DOCENTE</h4>
            <small className="d-block fw-normal">
              Genere la sugerencia, revísela y envíe manualmente el informe al Coordinador
            </small>
          </div>
        </div>

        <div className="card-body p-4">
          {error && <div className="alert alert-danger">{error}</div>}

          {!cargando && pendientes.length > 0 && (
            <div className="alert alert-warning">
              <span className="fw-bold">🔔</span> Tienes{" "}
              {pendientes.length} solicitud
              {pendientes.length === 1 ? "" : "es"} pendiente
              {pendientes.length === 1 ? "" : "s"} de revisar. La primera es{" "}
              <Link to={`/evaluar/${pendientes[0].solicitudId}`} className="fw-bold">
                {numeroSolicitud(pendientes[0])}
              </Link>
              . La URL, los documentos y la modalidad ya están guardados en la
              solicitud: no tiene que volver a subirlos.
            </div>
          )}

          <div className="table-responsive">
            <table className="table table-hover align-middle">
              <thead className="table-light">
                <tr>
                  <th>Solicitud</th>
                  <th>Coordinador</th>
                  <th>Titular (récord)</th>
                  <th>Modalidad</th>
                  <th>Carreras</th>
                  <th>Estado</th>
                  <th>Resultado del Docente</th>
                  <th>Revisado por</th>
                  <th className="text-center">Acción</th>
                </tr>
              </thead>
              <tbody>
                {cargando ? (
                  <tr>
                    <td colSpan="9" className="text-center py-4">
                      Cargando solicitudes...
                    </td>
                  </tr>
                ) : solicitudes.length === 0 ? (
                  <tr>
                    <td colSpan="9" className="text-center py-4">
                      No hay solicitudes asignadas para revisión.
                    </td>
                  </tr>
                ) : (
                  solicitudes.map((s) => {
                    const id = s.solicitudId;
                    const revisado = YA_REVISADO.includes(s.estadoDelegacion);
                    const borrador =
                      !revisado && Boolean(s.tieneSugerencia || s.fechaSugerencia);
                    const puedeEvaluar = s.puedeEvaluar !== false && !revisado;

                    return (
                      <tr key={id}>
                        <td>
                          <div className="fw-bold">
                            {s.numeroProceso || `SOL-${String(id).padStart(4, "0")}`}
                          </div>
                          <div className="small text-muted">{formatoFecha(s.fecha)}</div>
                        </td>
                        <td>
                          {s.coordinadorCorreo || s.comiteCorreo || "-"}
                        </td>
                        <td>
                          <span className="fw-semibold">{s.estudiante || "-"}</span>
                          <div className="small text-muted">
                            C.I.: {s.estudianteCedula || "-"}
                          </div>
                        </td>
                        <td>
                          <span
                            className={`badge ${
                              s.modulo === "EXTERNO" ? "bg-info text-dark" : "bg-primary"
                            }`}
                          >
                            {s.modulo || "INTERNO"}
                          </span>
                        </td>
                        <td>
                          {s.carreraOrigen || "-"} → {s.carreraDestino || "-"}
                          <div className="small text-muted">
                            {s.totalAsignaturas ?? 0} asignatura(s)
                          </div>
                        </td>
                        <td>
                          {revisado ? (
                            <span className="badge bg-secondary">Ya revisado</span>
                          ) : borrador ? (
                            <span className="badge bg-info text-dark">
                              Sugerencia generada · sin enviar
                            </span>
                          ) : (
                            <span className="badge bg-warning text-dark">
                              {s.estadoDelegacion || "PENDIENTE"}
                            </span>
                          )}
                        </td>
                        <td>
                          {revisado && s.resultadoDocente ? (
                            <span
                              className={`badge ${
                                s.resultadoDocente === "APROBADO" ? "bg-success" : "bg-danger"
                              }`}
                            >
                              {s.resultadoDocente}
                            </span>
                          ) : (
                            <span className="text-muted">-</span>
                          )}
                        </td>
                        <td>
                          <span>{s.revisadoPor || s.sugerenciaGeneradaPor || "-"}</span>
                          {s.informeSugeridoTexto && (
                            <div className="small text-muted" title={s.informeSugeridoTexto}>
                              {revisado
                                ? "Informe sugerido enviado"
                                : "Sugerencia en revisión"}
                            </div>
                          )}
                        </td>
                        <td className="text-center">
                          <Link
                            to={`/evaluar/${id}`}
                            className={`btn btn-sm ${
                              puedeEvaluar ? "btn-outline-primary" : "btn-outline-secondary"
                            }`}
                          >
                            {revisado
                              ? "Ver informe sugerido"
                              : borrador
                                ? "Continuar revisión"
                                : "Revisar y generar sugerencia IA"}
                          </Link>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
