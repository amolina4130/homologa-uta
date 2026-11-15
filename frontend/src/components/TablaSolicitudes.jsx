import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { descargarInforme } from "../services/catalogoService";

const COLOR_ESTADO = {
  REGISTRADA: "bg-secondary",
  PENDIENTE_REVISION: "bg-warning text-dark",
  INFORME_EMITIDO: "bg-info text-dark",
  RESUELTA: "bg-success",
  APROBADA: "bg-success",
  RECHAZADA: "bg-danger",
};

const formatearFecha = (sol) => {
  const valor = sol.fechaRegistro || sol.fecha;
  return valor ? new Date(valor).toLocaleDateString() : "-";
};

export const TablaSolicitudes = ({ solicitudes }) => {
  const navigate = useNavigate();
  const [descargando, setDescargando] = useState(null);

  if (!solicitudes || solicitudes.length === 0) {
    return (
      <div className="alert alert-info text-center shadow-sm mt-3" role="alert">
        No tienes solicitudes registradas por el momento.
      </div>
    );
  }

  const manejarInforme = async (id) => {
    setDescargando(id);
    try {
      await descargarInforme(id);
    } catch (err) {
      window.alert(err.message || "No se pudo descargar el informe.");
    } finally {
      setDescargando(null);
    }
  };

  return (
    <div className="table-responsive mt-3">
      <table className="table table-hover table-bordered shadow-sm align-middle">
        <thead className="table-primary">
          <tr>
            <th className="text-center">N.º</th>
            <th>Fecha</th>
            <th>Tipo</th>
            <th>Carrera Origen</th>
            <th>Carrera Destino</th>
            <th className="text-center">Estado</th>
            <th className="text-center">Acciones</th>
          </tr>
        </thead>
        <tbody>
          {solicitudes.map((sol) => {
            const estado = sol.estado || "REGISTRADA";
            return (
              <tr key={sol.id}>
                <td className="text-center fw-bold">{sol.numeroProceso || sol.numero || `SOL-${String(sol.id).padStart(4, "0")}`}</td>
                <td>{formatearFecha(sol)}</td>
                <td>
                  {sol.tipo || "-"}
                  {sol.modulo && <div className="small text-muted">{sol.modulo}</div>}
                </td>
                <td>
                  {sol.origen || sol.carreraOrigen || "-"}
                  {sol.universidadOrigen && <div className="small text-muted">{sol.universidadOrigen}</div>}
                </td>
                <td>
                  {sol.destino || sol.carreraDestino || "-"}
                  {sol.universidadDestino && <div className="small text-muted">{sol.universidadDestino}</div>}
                </td>
                <td className="text-center">
                  <span className={`badge p-2 ${COLOR_ESTADO[estado] || "bg-secondary"}`}>{estado}</span>
                </td>
                <td className="text-center">
                  <div className="d-flex flex-wrap gap-1 justify-content-center">
                    {sol.editable && (
                      <button
                        className="btn btn-sm btn-outline-primary"
                        onClick={() => navigate(`/solicitudes/${sol.id}/editar`)}
                      >
                        Editar
                      </button>
                    )}
                    {["PENDIENTE_REVISION", "INFORME_EMITIDO", "RESUELTA"].includes(estado) && (
                      <button
                        className="btn btn-sm btn-outline-dark"
                        onClick={() => navigate(`/resultado/${sol.id}`)}
                      >
                        Resultado
                      </button>
                    )}
                    {["INFORME_EMITIDO", "RESUELTA"].includes(estado) && (
                      <button
                        className="btn btn-sm btn-outline-success"
                        disabled={descargando === sol.id}
                        onClick={() => manejarInforme(sol.id)}
                      >
                        {descargando === sol.id ? "..." : "Informe"}
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
