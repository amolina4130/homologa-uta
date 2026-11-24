import { Link } from "react-router-dom";

const ESTADOS_CON_INFORME = ["INFORME_EMITIDO", "RESUELTA"];

const claseEstado = (estado) => {
  switch (estado) {
    case "RESUELTA":
      return "bg-success";
    case "INFORME_EMITIDO":
      return "bg-info text-dark";
    case "PENDIENTE_REVISION":
      return "bg-warning text-dark";
    default:
      return "bg-secondary";
  }
};

const formatoFecha = (valor) =>
  valor ? new Date(valor).toLocaleDateString("es-EC") : "-";

export const TablaSolicitudes = ({ solicitudes, onDescargarInforme }) => {
  if (!solicitudes || solicitudes.length === 0) {
    return (
      <div className="alert alert-info text-center shadow-sm mt-3" role="alert">
        No tienes solicitudes registradas con esos criterios.
      </div>
    );
  }

  return (
    <div className="table-responsive mt-3">
      <table className="table table-hover table-bordered shadow-sm align-middle">
        <thead className="table-primary">
          <tr>
            <th className="text-center">N° de proceso</th>
            <th>Fecha</th>
            <th>Tipo</th>
            <th>Carrera origen</th>
            <th>Carrera destino</th>
            <th className="text-center">Módulo</th>
            <th className="text-center">Estado</th>
            <th className="text-center">Acciones</th>
          </tr>
        </thead>
        <tbody>
          {solicitudes.map((sol) => {
            const numero = sol.numeroProceso || sol.numero || `SOL-${String(sol.id).padStart(4, "0")}`;
            const puedeEditar = sol.puedeEditar ?? sol.estado === "REGISTRADA";
            const tieneInforme = ESTADOS_CON_INFORME.includes(sol.estado);

            return (
              <tr key={sol.id}>
                <td className="text-center fw-bold">{numero}</td>
                <td>{formatoFecha(sol.fechaRegistro || sol.fecha)}</td>
                <td>{sol.tipo || "-"}</td>
                <td>{sol.origen || sol.carreraOrigen || "-"}</td>
                <td>{sol.destino || sol.carreraDestino || "-"}</td>
                <td className="text-center">
                  <span
                    className={`badge ${
                      sol.modulo === "EXTERNO" ? "bg-info text-dark" : "bg-light text-dark border"
                    }`}
                  >
                    {sol.modulo === "EXTERNO" ? "Externo" : "Interno"}
                  </span>
                </td>
                <td className="text-center">
                  <span className={`badge p-2 ${claseEstado(sol.estado)}`}>
                    {sol.estado || "REGISTRADA"}
                  </span>
                </td>
                <td className="text-center">
                  <div className="d-flex flex-wrap gap-1 justify-content-center">
                    <Link
                      to={`/resultado/${sol.id}`}
                      className="btn btn-sm btn-outline-primary fw-bold"
                    >
                      Ver detalle
                    </Link>

                    {puedeEditar && (
                      <Link
                        to={`/editar-solicitud/${sol.id}`}
                        className="btn btn-sm btn-outline-secondary fw-bold"
                      >
                        Editar
                      </Link>
                    )}

                    {tieneInforme && onDescargarInforme && (
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-success fw-bold"
                        onClick={() => onDescargarInforme(sol.id)}
                      >
                        Descargar informe
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
};
