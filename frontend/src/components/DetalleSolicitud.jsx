import { useState } from "react";

const DECISIONES_VALIDAS = ["HOMOLOGABLE", "NO_HOMOLOGABLE"];

const decisionesIniciales = (solicitud) => {
  const resultado = {};
  for (const asig of solicitud?.asignaturas ?? []) {
    resultado[asig.id] = DECISIONES_VALIDAS.includes(asig.decisionCoordinador)
      ? asig.decisionCoordinador
      : "";
  }
  return resultado;
};

const observacionesIniciales = (solicitud) => {
  const resultado = {};
  for (const asig of solicitud?.asignaturas ?? []) {
    resultado[asig.id] = asig.observacionCoordinador || "";
  }
  return resultado;
};

const claseDecision = (decision) => {
  if (decision === "HOMOLOGABLE") return "bg-success";
  if (decision === "NO_HOMOLOGABLE") return "bg-danger";
  return "bg-warning text-dark";
};

const textoDecision = (decision) => {
  if (decision === "HOMOLOGABLE") return "Homologable";
  if (decision === "NO_HOMOLOGABLE") return "No homologable";
  if (decision === "REQUIERE_REVISION") return "Requiere revisión";
  return decision || "Pendiente";
};

const formatoFecha = (valor) =>
  valor ? new Date(valor).toLocaleString("es-EC") : "-";

const textoMateriasOrigen = (asig) => {
  if ((asig.materiasOrigen?.length ?? 0) > 0) {
    return asig.materiasOrigen
      .map((m) =>
        m.nota !== null && m.nota !== undefined
          ? `${m.nombre} (nota ${m.nota})`
          : m.nombre
      )
      .join(" + ");
  }
  return asig.asignaturaOrigen || "-";
};

export default function DetalleSolicitud({
  solicitud,
  onVolver,
  onGuardar,
  onDescargarInforme,
  descargando,
  cargando,
}) {
  const [decisiones, setDecisiones] = useState(() => decisionesIniciales(solicitud));
  const [observaciones, setObservaciones] = useState(() => observacionesIniciales(solicitud));
  const [errorLocal, setErrorLocal] = useState("");

  if (!solicitud) return null;

  const asignaturas = solicitud.asignaturas || [];
  const soloLectura = solicitud.estado !== "PENDIENTE_REVISION";
  const resuelta = solicitud.estado === "RESUELTA";
  const numero = solicitud.numeroProceso || `SOL-${String(solicitud.solicitudId).padStart(4, "0")}`;
  const tieneResolucion = Boolean(solicitud.resolucion);

  const handleDecisionChange = (id, valor) => {
    setDecisiones((prev) => ({ ...prev, [id]: valor }));
  };

  const handleObservacionChange = (id, valor) => {
    setObservaciones((prev) => ({ ...prev, [id]: valor }));
  };

  const handleGuardar = () => {
    setErrorLocal("");

    if (asignaturas.length === 0) {
      setErrorLocal("La solicitud no tiene asignaturas para revisar.");
      return;
    }

    const faltantes = asignaturas.filter((asig) => {
      const decision = decisiones[asig.id];
      const observacion = (observaciones[asig.id] || "").trim();
      return !DECISIONES_VALIDAS.includes(decision) || observacion.length === 0;
    });

    if (faltantes.length > 0) {
      setErrorLocal(
        "Debe seleccionar una decisión y escribir una observación para cada asignatura."
      );
      return;
    }

    const informe = {
      asignaturas: asignaturas.map((asig) => ({
        asignaturaId: asig.id,
        decision: decisiones[asig.id],
        observacion: observaciones[asig.id].trim(),
      })),
    };

    onGuardar(informe);
  };

  return (
    <div className="card shadow-sm border-0 mt-3">
      <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center">
        <h5 className="mb-0">Detalle de la solicitud {numero}</h5>
        <div className="d-flex gap-2">
          {onDescargarInforme && (
            <button
              className="btn btn-sm btn-outline-light"
              onClick={onDescargarInforme}
              disabled={descargando}
            >
              {descargando ? "Descargando..." : "Descargar informe"}
            </button>
          )}
          <button className="btn btn-sm btn-outline-light" onClick={onVolver}>
            Volver a la lista
          </button>
        </div>
      </div>

      <div className="card-body">
        <div className="row mb-3">
          <div className="col-md-6">
            <p className="mb-1">
              <strong>Titular del trámite (récord):</strong> {solicitud.estudiante || "-"}
            </p>
            <p className="mb-1">
              <strong>Cédula del titular:</strong> {solicitud.estudianteCedula || "-"}
            </p>
            <p className="mb-1">
              <strong>Registrado por:</strong> {solicitud.comiteCorreo || "-"}
            </p>
            <p className="mb-1">
              <strong>Coordinador:</strong> {solicitud.coordinadorCorreo || "Pendiente de informe"}
            </p>
            <p className="mb-1"><strong>Tipo:</strong> {solicitud.tipo}</p>
            <p className="mb-1">
              <strong>Modalidad:</strong>{" "}
              <span
                className={`badge ${
                  solicitud.modulo === "EXTERNO" ? "bg-info text-dark" : "bg-primary"
                }`}
              >
                {solicitud.modulo || "INTERNO"}
              </span>
            </p>
            {solicitud.modulo === "EXTERNO" && (
              <p className="mb-1">
                <strong>Universidades:</strong>{" "}
                {solicitud.universidadOrigen || "Origen"} → {solicitud.universidadDestino || "Destino"}
              </p>
            )}
            <p className="mb-1"><strong>Carrera origen:</strong> {solicitud.carreraOrigen}</p>
            <p className="mb-0"><strong>Carrera destino:</strong> {solicitud.carreraDestino}</p>
          </div>
          <div className="col-md-6">
            <p className="mb-1">
              <strong>Estado:</strong>{" "}
              <span className={`badge ${soloLectura ? "bg-success" : "bg-warning text-dark"}`}>
                {solicitud.estado}
              </span>
            </p>
            <p className="mb-1">
              <strong>Fecha de registro:</strong> {formatoFecha(solicitud.fecha)}
            </p>
            <p className="mb-1">
              <strong>Fecha del análisis:</strong> {formatoFecha(solicitud.fechaAnalisis)}
            </p>
            {solicitud.fechaInforme && (
              <p className="mb-0">
                <strong>Fecha del informe:</strong> {formatoFecha(solicitud.fechaInforme)}
              </p>
            )}
          </div>
        </div>

        {soloLectura && (
          <div className={`alert ${resuelta ? "alert-success" : "alert-info"}`}>
            {resuelta
              ? "La solicitud fue resuelta por el Consejo Directivo. Los valores se muestran en modo lectura."
              : `El informe técnico ya fue emitido por ${solicitud.coordinadorCorreo || "el Coordinador"}. Los valores se muestran en modo lectura.`}
          </div>
        )}

        {errorLocal && <div className="alert alert-danger">{errorLocal}</div>}

        <h6 className="mt-4 fw-bold">Propuesta del agente y decisión del Coordinador</h6>

        {asignaturas.length === 0 ? (
          <p className="text-muted">No hay asignaturas registradas en este análisis.</p>
        ) : (
          <div className="table-responsive">
            <table className="table table-bordered align-middle">
              <thead className="table-light">
                <tr>
                  <th>Asignatura</th>
                  <th>Materias de origen</th>
                  <th>Propuesta del agente</th>
                  <th>Decisión del Coordinador *</th>
                  <th>Observación *</th>
                  {tieneResolucion && <th>Decisión final</th>}
                </tr>
              </thead>
              <tbody>
                {asignaturas.map((asig) => (
                  <tr key={asig.id}>
                    <td>
                      <div className="fw-bold">{asig.asignaturaDestino}</div>
                      {asig.combinada && (
                        <span className="badge bg-info text-dark mt-1">Combinada</span>
                      )}
                    </td>
                    <td className="small">{textoMateriasOrigen(asig)}</td>
                    <td>
                      <span className={`badge ${claseDecision(asig.propuesta?.decision)}`}>
                        {textoDecision(asig.propuesta?.decision)}
                      </span>
                      <div className="small text-muted mt-1">
                        Contenido {asig.propuesta?.porcentajeContenido ?? 0} % · Profundidad{" "}
                        {asig.propuesta?.porcentajeProfundidad ?? 0} % · Horas{" "}
                        {asig.propuesta?.porcentajeHoras ?? 0} %
                      </div>
                      {asig.propuesta?.justificacion && (
                        <div className="small mt-1" title={asig.propuesta.justificacion}>
                          {asig.propuesta.justificacion}
                        </div>
                      )}
                    </td>
                    <td style={{ minWidth: "170px" }}>
                      {soloLectura ? (
                        <span className={`badge ${claseDecision(asig.decisionCoordinador)}`}>
                          {textoDecision(asig.decisionCoordinador)}
                        </span>
                      ) : (
                        <select
                          className="form-select form-select-sm"
                          value={decisiones[asig.id] || ""}
                          onChange={(e) => handleDecisionChange(asig.id, e.target.value)}
                          disabled={cargando}
                        >
                          <option value="">Seleccione...</option>
                          <option value="HOMOLOGABLE">HOMOLOGABLE</option>
                          <option value="NO_HOMOLOGABLE">NO HOMOLOGABLE</option>
                        </select>
                      )}
                    </td>
                    <td style={{ minWidth: "220px" }}>
                      {soloLectura ? (
                        <span>{asig.observacionCoordinador || "-"}</span>
                      ) : (
                        <input
                          type="text"
                          className="form-control form-control-sm"
                          placeholder="Escriba la observación..."
                          value={observaciones[asig.id] || ""}
                          onChange={(e) => handleObservacionChange(asig.id, e.target.value)}
                          disabled={cargando}
                        />
                      )}
                    </td>
                    {tieneResolucion && (
                      <td style={{ minWidth: "150px" }}>
                        {asig.decisionFinal ? (
                          <span className={`badge ${claseDecision(asig.decisionFinal)}`}>
                            {textoDecision(asig.decisionFinal)}
                          </span>
                        ) : (
                          <span className="text-muted">Sin decisión</span>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!soloLectura && (
          <>
            <p className="text-muted small mb-0">
              * Todos los campos son obligatorios para emitir el informe.
            </p>
            <div className="text-end mt-3">
              <button
                className="btn btn-success fw-bold"
                onClick={handleGuardar}
                disabled={cargando || asignaturas.length === 0}
              >
                {cargando ? "Emitiendo..." : "Emitir Informe Técnico"}
              </button>
            </div>
          </>
        )}

        {solicitud.resolucion && (
          <div className="card border-dark mt-4">
            <div className="card-header bg-dark text-white">
              <h6 className="mb-0 fw-bold">Resolución registrada</h6>
            </div>
            <div className="card-body">
              <div className="row mb-2">
                <div className="col-md-3 fw-bold">Número</div>
                <div className="col-md-9">{solicitud.resolucion.numeroResolucion}</div>
              </div>
              <div className="row mb-2">
                <div className="col-md-3 fw-bold">Fecha</div>
                <div className="col-md-9">{formatoFecha(solicitud.resolucion.fecha)}</div>
              </div>
              <div className="row mb-2">
                <div className="col-md-3 fw-bold">Registrada por</div>
                <div className="col-md-9">{solicitud.resolucion.registradoPor || "-"}</div>
              </div>
              {solicitud.resolucion.observacion && (
                <div className="row mb-0">
                  <div className="col-md-3 fw-bold">Observación</div>
                  <div className="col-md-9">{solicitud.resolucion.observacion}</div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
