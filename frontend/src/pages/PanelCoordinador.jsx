import { useState, useEffect, useCallback } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import {
  obtenerSolicitudesPendientes,
  obtenerDetalleSolicitud,
  emitirInforme,
  registrarResolucion,
  delegarDocentes,
  obtenerDelegaciones,
  obtenerConsolidado,
  emitirInformeFinal,
  descargarInformeFinal,
  descargarInformeSugeridoDocente,
} from "../services/coordinadorService";
import { descargarInforme } from "../services/homologacionService";
import { actualizarTitularSolicitud } from "../services/solicitudesService";
import ListaSolicitudes from "../components/ListaSolicitudes";
import DetalleSolicitud from "../components/DetalleSolicitud";
import FormularioResolucion from "../components/FormularioResolucion";

const DECISIONES_FINALES = ["HOMOLOGABLE", "NO_HOMOLOGABLE", "REQUIERE_REVISION"];

const TITULAR_SIN_IDENTIFICAR = "Estudiante sin identificar";

const datoDelRecord = (valor, valorVacio) =>
  valor && valor !== valorVacio ? valor : "";

const correoDelegado = (delegacion) =>
  delegacion?.docenteCorreo ||
  delegacion?.correoDocente ||
  delegacion?.correo ||
  delegacion?.docente?.correo ||
  delegacion?.email ||
  "-";

const revisadoPor = (delegacion) =>
  delegacion?.revisadoPor ||
  delegacion?.revisadoPorCorreo ||
  delegacion?.revisadoPor?.correo ||
  delegacion?.revisadoPorNombre ||
  "-";

const estadoDelegacionBadge = (valor) => {
  const estado = (valor || "PENDIENTE").toUpperCase();
  if (estado === "REVISADO") return { clase: "bg-success", texto: "REVISADO" };
  if (estado === "YA_REVISADO") return { clase: "bg-warning text-dark", texto: "YA REVISADO" };
  if (estado === "PENDIENTE") return { clase: "bg-secondary", texto: "PENDIENTE" };
  return { clase: "bg-info text-dark", texto: estado };
};

const textoInformeSugerido = (consolidado, delegaciones) => {
  if (consolidado?.informeSugeridoTexto) return consolidado.informeSugeridoTexto;
  if (consolidado?.informeSugerido) return consolidado.informeSugerido;

  const textos = delegaciones
    .map((d) => d?.informeTexto || d?.informeSugeridoTexto || d?.informeSugerido || d?.informe?.texto)
    .filter(Boolean);
  return textos.join("\n\n");
};

const normalizarDecision = (valor) => {
  if (typeof valor !== "string") return "REQUIERE_REVISION";
  const decision = valor.trim().toUpperCase().replace(/[\s-]+/g, "_");
  if (DECISIONES_FINALES.includes(decision)) return decision;
  if (decision === "HOMOLOGABLE") return "HOMOLOGABLE";
  if (decision === "NO_HOMOLOGABLE") return "NO_HOMOLOGABLE";
  return "REQUIERE_REVISION";
};

const asignaturasParaInforme = (consolidado, solicitud) => {
  const evaluaciones = consolidado?.evaluacionesConsolidadas;
  if (Array.isArray(evaluaciones) && evaluaciones.length > 0) {
    return evaluaciones.map((e) => ({
      asignaturaId: e.asignaturaId ?? e.id,
      decision: normalizarDecision(
        e.decision ||
          (typeof e.homologable === "boolean"
            ? e.homologable
              ? "HOMOLOGABLE"
              : "NO_HOMOLOGABLE"
            : "REQUIERE_REVISION")
      ),
      observacion: e.observacion || e.justificacion || "",
    }));
  }

  const asignaturas = consolidado?.detalle?.asignaturas || solicitud?.asignaturas || [];
  return asignaturas.map((a) => ({
    asignaturaId: a.asignaturaId ?? a.id,
    decision: normalizarDecision(
      a.decisionFinal ||
        a.decisionCoordinador ||
        a.decision ||
        a.propuesta?.decision ||
        "REQUIERE_REVISION"
    ),
    observacion: a.observacion || a.observacionCoordinador || a.justificacion || "",
  }));
};

export default function PanelCoordinador({ vista = "completo" }) {
  const { rol } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // Cada opción del menú del Coordinador muestra solo la etapa correspondiente.
  const mostrarDelegacion = vista === "completo" || vista === "delegar";
  const mostrarSugeridos = vista === "completo" || vista === "sugeridos" || vista === "final";
  const mostrarFinal = vista === "completo" || vista === "final";
  const mostrarDetalle = vista === "completo";

  const [solicitudes, setSolicitudes] = useState([]);
  const [solicitudSeleccionada, setSolicitudSeleccionada] = useState(null);
  // Id de la solicitud sobre la que se delegan docentes y se consolida el informe.
  const [solicitudActivaId, setSolicitudActivaId] = useState(
    searchParams.get("solicitudId") || ""
  );
  const [cargando, setCargando] = useState(false);
  const [descargando, setDescargando] = useState(false);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");

  const [correoUno, setCorreoUno] = useState("docente@uta.edu.ec");
  const [correoDos, setCorreoDos] = useState("docente2@uta.edu.ec");
  const [delegando, setDelegando] = useState(false);
  const [consolidado, setConsolidado] = useState(null);
  const [delegaciones, setDelegaciones] = useState([]);
  const [cargandoConsolidado, setCargandoConsolidado] = useState(false);

  const [asignaturasFinales, setAsignaturasFinales] = useState([]);
  const [camposFinales, setCamposFinales] = useState({
    nombreEstudiante: "",
    cedulaEstudiante: "",
    numeroMemorando: "",
    nombreFirmante: "",
  });
  const [informeFinalTexto, setInformeFinalTexto] = useState("");
  const [informeFinalGenerado, setInformeFinalGenerado] = useState(false);
  const [generandoFinal, setGenerandoFinal] = useState(false);
  const [descargandoFinal, setDescargandoFinal] = useState(false);
  const [descargandoSugerido, setDescargandoSugerido] = useState(false);

  // El titular del trámite no es un usuario: sus datos vienen del récord académico
  // que entregó el Coordinador y se precargan aquí para que este los verifique.
  const prefillTitular = useCallback((id, lista = []) => {
    const sol = lista.find((s) => String(s.solicitudId) === String(id));
    setCamposFinales((prev) => ({
      ...prev,
      nombreEstudiante: datoDelRecord(sol?.estudiante, TITULAR_SIN_IDENTIFICAR),
      cedulaEstudiante: datoDelRecord(sol?.estudianteCedula, "-"),
    }));
  }, []);

  const cargarPendientes = useCallback(async () => {
    setCargando(true);
    setError("");
    try {
      const data = await obtenerSolicitudesPendientes();
      setSolicitudes(data);
      setSolicitudActivaId((actual) => {
        const inicial =
          actual ||
          (Array.isArray(data) && data[0]?.solicitudId ? String(data[0].solicitudId) : "");
        if (inicial) prefillTitular(inicial, data);
        return inicial;
      });
    } catch (e) {
      setError(e.message || "No se pudieron cargar las solicitudes pendientes.");
    } finally {
      setCargando(false);
    }
  }, [prefillTitular]);

  const cargarConsolidado = useCallback(async () => {
    if (!solicitudActivaId) return;
    setCargandoConsolidado(true);
    setError("");
    try {
      const [datosConsolidado, datosDelegaciones] = await Promise.all([
        obtenerConsolidado(solicitudActivaId),
        obtenerDelegaciones(solicitudActivaId),
      ]);
      setConsolidado(datosConsolidado || {});
      setDelegaciones(
        Array.isArray(datosDelegaciones)
          ? datosDelegaciones
          : Array.isArray(datosConsolidado?.delegaciones)
            ? datosConsolidado.delegaciones
            : []
      );
      setAsignaturasFinales(asignaturasParaInforme(datosConsolidado, null));
      const textoFinal = datosConsolidado?.informeFinalTexto || "";
      setInformeFinalTexto(textoFinal);
      setInformeFinalGenerado(Boolean(textoFinal));

      const titular = datosConsolidado?.detalle;
      if (titular) {
        setCamposFinales((prev) => ({
          ...prev,
          nombreEstudiante:
            datoDelRecord(titular.estudiante, TITULAR_SIN_IDENTIFICAR) || prev.nombreEstudiante,
          cedulaEstudiante: datoDelRecord(titular.estudianteCedula, "-") || prev.cedulaEstudiante,
        }));
      }
    } catch (e) {
      setError(e.message || "No se pudo cargar el consolidado de delegaciones.");
    } finally {
      setCargandoConsolidado(false);
    }
  }, [solicitudActivaId]);

  useEffect(() => {
    if (rol !== "COORDINADOR") return;
    const timer = setTimeout(() => cargarPendientes(), 0);
    return () => clearTimeout(timer);
  }, [rol, cargarPendientes]);

  useEffect(() => {
    const timer = setTimeout(() => {
      if (!solicitudActivaId) {
        setConsolidado(null);
        setDelegaciones([]);
        setAsignaturasFinales([]);
        setInformeFinalTexto("");
        setInformeFinalGenerado(false);
        return;
      }
      cargarConsolidado();
    }, 0);
    return () => clearTimeout(timer);
  }, [solicitudActivaId, cargarConsolidado]);

  // "Ver detalle" lleva a una página propia: el detalle ya no aparece debajo de la lista.
  const abrirDetalle = (id) => {
    navigate(`/detalle-solicitud/${id}`);
  };

  // En las vistas divididas, elegir una fila solo cambia la solicitud activa.
  const seleccionarSolicitud = (id) => {
    setSolicitudActivaId(String(id));
    prefillTitular(id, solicitudes);
    setError("");
    setExito("");
  };

  // El componente hijo arma el informe con la decisión y la observación de cada asignatura.
  const guardarInforme = async (informeArmado) => {
    if (!solicitudSeleccionada) return;

    const confirmar = window.confirm("¿Está seguro de emitir el informe técnico?");
    if (!confirmar) return;

    setCargando(true);
    setError("");
    setExito("");

    try {
      const resultado = await emitirInforme(solicitudSeleccionada.solicitudId, informeArmado);
      setExito(
        resultado?.estadoSolicitudActualizado === false
          ? "Informe técnico guardado. No se pudo actualizar el estado de la solicitud en el servicio de Solicitudes."
          : "Informe técnico emitido correctamente."
      );
      setSolicitudSeleccionada(null);
      await cargarPendientes();
    } catch (e) {
      setError(e.message || "Error al emitir el informe.");
    } finally {
      setCargando(false);
    }
  };

  // HU-17: registra la resolución del Consejo Directivo.
  const guardarResolucion = async (resolucion) => {
    if (!solicitudSeleccionada) return;

    const confirmar = window.confirm(
      "¿Está seguro de registrar la resolución del Consejo Directivo? Esta acción no se puede repetir."
    );
    if (!confirmar) return;

    setCargando(true);
    setError("");
    setExito("");

    try {
      const resultado = await registrarResolucion(
        solicitudSeleccionada.solicitudId,
        resolucion
      );
      setExito(
        `Resolución ${resultado?.numeroResolucion || ""} registrada. La solicitud quedó en estado ${
          resultado?.estadoSolicitud || "RESUELTA"
        }.`
      );
      const detalle = await obtenerDetalleSolicitud(solicitudSeleccionada.solicitudId);
      setSolicitudSeleccionada(detalle);
      await cargarPendientes();
    } catch (e) {
      setError(e.message || "No se pudo registrar la resolución.");
    } finally {
      setCargando(false);
    }
  };

  // HU-18: descarga el informe PDF con el token de la sesión.
  const descargar = async (id) => {
    setDescargando(true);
    setError("");
    setExito("");
    try {
      await descargarInforme(id);
      setExito(`Informe de SOL-${String(id).padStart(4, "0")} descargado.`);
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe.");
    } finally {
      setDescargando(false);
    }
  };

  const delegar = async (ev) => {
    ev.preventDefault();
    setError("");
    setExito("");

    if (!solicitudActivaId) {
      setError("Seleccione una solicitud para continuar.");
      return;
    }

    const correos = [correoUno.trim(), correoDos.trim()].filter(Boolean);
    if (correos.length < 1 || correos.length > 2) {
      setError("Debe indicar entre uno y dos correos de docentes.");
      return;
    }
    if (correos.some((correo) => !/^\S+@\S+\.\S+$/.test(correo))) {
      setError("Ingrese correos electrónicos válidos.");
      return;
    }

    setDelegando(true);
    try {
      const resultado = await delegarDocentes(solicitudActivaId, correos);
      setExito(
        `Delegación registrada para ${Array.isArray(resultado) ? resultado.length : correos.length} docente(s).`
      );
      await cargarConsolidado();
    } catch (e) {
      setError(e.message || "No se pudieron registrar las delegaciones.");
    } finally {
      setDelegando(false);
    }
  };

  const cambiarCampoFinal = (campo, valor) => {
    setCamposFinales((prev) => ({ ...prev, [campo]: valor }));
  };

  // Guarda el titular en la solicitud sin cambiar su estado ni generar informes.
  const guardarTitular = async () => {
    if (!solicitudActivaId) {
      setError("Seleccione una solicitud antes de guardar el titular.");
      return;
    }

    setCargando(true);
    setError("");
    setExito("");
    try {
      const resultado = await actualizarTitularSolicitud(
        solicitudActivaId,
        camposFinales.nombreEstudiante,
        camposFinales.cedulaEstudiante
      );
      setCamposFinales((prev) => ({
        ...prev,
        nombreEstudiante: resultado.estudianteNombre || prev.nombreEstudiante,
        cedulaEstudiante: resultado.estudianteCedula || prev.cedulaEstudiante,
      }));
      setSolicitudes((prev) =>
        prev.map((s) =>
          String(s.solicitudId) === String(solicitudActivaId)
            ? {
                ...s,
                estudiante: resultado.estudianteNombre || s.estudiante,
                estudianteCedula: resultado.estudianteCedula || s.estudianteCedula,
              }
            : s
        )
      );
      setExito(resultado.mensaje || "Datos del titular guardados.");
    } catch (e) {
      setError(e.message || "No se pudo guardar el titular del trámite.");
    } finally {
      setCargando(false);
    }
  };

  const cambiarAsignaturaFinal = (indice, campo, valor) => {
    setAsignaturasFinales((prev) =>
      prev.map((a, i) => (i === indice ? { ...a, [campo]: valor } : a))
    );
  };

  const generarInformeFinal = async () => {
    setError("");
    setExito("");

    if (!solicitudActivaId) {
      setError("Seleccione una solicitud para continuar.");
      return;
    }

    if (!puedeGenerarFinal) {
      setError("El informe final solo se habilita después de que el Docente haya revisado y emitido su informe sugerido.");
      return;
    }

    const base =
      asignaturasFinales.length > 0
        ? asignaturasFinales
        : asignaturasParaInforme(consolidado, solicitudSeleccionada);

    if (base.length === 0) {
      setError("No hay asignaturas consolidadas para generar el informe final.");
      return;
    }

    setGenerandoFinal(true);
    try {
      const resultado = await emitirInformeFinal(solicitudActivaId, {
        asignaturas: base.map((a) => ({
          asignaturaId: a.asignaturaId,
          decision: a.decision,
          observacion: (a.observacion || "").trim(),
        })),
        nombreEstudiante: (camposFinales.nombreEstudiante || "").trim(),
        cedulaEstudiante: (camposFinales.cedulaEstudiante || "").trim(),
        numeroMemorando: (camposFinales.numeroMemorando || "").trim(),
        nombreFirmante: (camposFinales.nombreFirmante || "").trim(),
      });

      const texto =
        resultado?.informeFinalTexto ||
        resultado?.texto ||
        resultado?.informeFinal ||
        resultado?.informe?.texto ||
        "";
      setInformeFinalTexto(texto || informeFinalTexto);
      setInformeFinalGenerado(true);
      setConsolidado((prev) => ({
        ...(prev || {}),
        informeFinalTexto: texto || prev?.informeFinalTexto,
      }));
      setExito(
        "Informe final generado. Será enviado al Consejo Directivo."
      );
    } catch (e) {
      setError(e.message || "No se pudo generar el informe final.");
    } finally {
      setGenerandoFinal(false);
    }
  };

  const descargarSugerido = async () => {
    if (!solicitudActivaId) {
      setError("Seleccione una solicitud antes de descargar el informe sugerido.");
      return;
    }
    setError("");
    setExito("");
    setDescargandoSugerido(true);
    try {
      await descargarInformeSugeridoDocente(solicitudActivaId);
      setExito("Informe sugerido del Docente descargado en PDF.");
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe sugerido del Docente.");
    } finally {
      setDescargandoSugerido(false);
    }
  };

  const descargarFinal = async () => {
    setError("");
    setExito("");
    setDescargandoFinal(true);
    try {
      await descargarInformeFinal(solicitudActivaId);
      setExito("Informe final descargado en PDF.");
    } catch (e) {
      setError(e.message || "No se pudo descargar el informe final.");
    } finally {
      setDescargandoFinal(false);
    }
  };

  if (rol !== "COORDINADOR") {
    return (
      <div className="container mt-4">
        <div className="alert alert-danger">Acceso denegado. Solo el Coordinador puede ver esta pantalla.</div>
      </div>
    );
  }

  const textoSugerido = textoInformeSugerido(consolidado, delegaciones);
  const filasSugeridas = consolidado?.informeSugeridoAsignaturas || [];
  const textoFinal = consolidado?.informeFinalTexto || informeFinalTexto;
  const puedeGenerarFinal = consolidado?.puedeGenerarInformeFinal === true;
  const revisadoPorDocente =
    consolidado?.revisadoPor ||
    revisadoPor(delegaciones.find((d) => d?.estado === "REVISADO") || {});
  const resultadoDocente = consolidado?.resultadoDocente || "-";
  const tituloVista =
    vista === "delegar" ? "DELEGAR DOCENTES"
      : vista === "sugeridos" ? "INFORMES SUGERIDOS DEL DOCENTE"
        : vista === "final" ? "GENERAR INFORME FINAL"
          : "PANEL DEL COORDINADOR";
  const solicitudActiva = solicitudes.find(
    (s) => String(s.solicitudId) === String(solicitudActivaId)
  );
  const moduloActivo = consolidado?.detalle?.modulo || solicitudActiva?.modulo || "INTERNO";
  const comiteActivo = consolidado?.detalle?.comiteCorreo || solicitudActiva?.comiteCorreo || "-";
  const coordinadorActivo =
    consolidado?.detalle?.coordinadorCorreo || "Se registra al emitir el informe";

  return (
    <div className="container mt-4">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="mb-0">{tituloVista}</h2>
        <button className="btn btn-outline-primary btn-sm" onClick={cargarPendientes} disabled={cargando}>
          Actualizar lista
        </button>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {exito && <div className="alert alert-success">{exito}</div>}
      {!solicitudActivaId && (
        <div className="alert alert-warning">
          Seleccione una solicitud para continuar con esta etapa del trámite.
        </div>
      )}

      {vista !== "completo" && (
        <div className="card shadow-sm border-0 mb-4">
          <div className="card-body p-3">
            <label htmlFor="solicitudActiva" className="form-label fw-bold mb-2">
              Solicitud activa
            </label>
            <select
              id="solicitudActiva"
              className="form-select"
              value={solicitudActivaId}
              onChange={(e) => seleccionarSolicitud(e.target.value)}
              disabled={cargando}
            >
              <option value="">Seleccione una solicitud</option>
              {solicitudes.map((s) => (
                <option key={s.solicitudId} value={s.solicitudId}>
                  {s.numeroProceso || `SOL-${String(s.solicitudId).padStart(4, "0")}`} ·{" "}
                  {s.carreraOrigen || "-"} → {s.carreraDestino || "-"}
                </option>
              ))}
            </select>
          </div>
        </div>
      )}

      {!mostrarDetalle || !solicitudSeleccionada ? (
        <ListaSolicitudes
          solicitudes={solicitudes}
          cargando={cargando}
          onAbrir={abrirDetalle}
        />
      ) : (
        <>
          <DetalleSolicitud
            key={solicitudSeleccionada.solicitudId}
            solicitud={solicitudSeleccionada}
            onVolver={() => {
              setSolicitudSeleccionada(null);
              setError("");
            }}
            onGuardar={guardarInforme}
            onDescargarInforme={() => descargar(solicitudSeleccionada.solicitudId)}
            descargando={descargando}
            cargando={cargando}
          />

          {!solicitudSeleccionada.resolucion &&
            solicitudSeleccionada.estado === "INFORME_EMITIDO" && (
              <FormularioResolucion
                key={solicitudSeleccionada.solicitudId}
                solicitud={solicitudSeleccionada}
                onRegistrar={guardarResolucion}
                cargando={cargando}
              />
            )}
        </>
      )}

      <hr className="my-5" />

      {mostrarDelegacion && (
        <>
      {/* Delegación de docentes */}
      <div className="card shadow-sm border-0 mb-4">
        <div className="card-header bg-primary text-white">
          <h4 className="mb-0 fw-bold">DELEGAR DOCENTES</h4>
        </div>
        <div className="card-body p-4">
          <p className="text-muted">
            Delegue la revisión a <strong>uno o dos docentes</strong>. Cada docente evaluará
            las solicitudes asignadas y generará su informe con &laquo;se sugiere&raquo;.
          </p>
          <form className="row g-3 align-items-end" onSubmit={delegar}>
            <div className="col-md-12">
              <label htmlFor="solicitudDelegacion" className="form-label fw-bold">
                Solicitud a delegar
              </label>
              <select
                id="solicitudDelegacion"
                className="form-select"
                value={solicitudActivaId}
                onChange={(e) => {
                  setSolicitudActivaId(e.target.value);
                  prefillTitular(e.target.value, solicitudes);
                  setError("");
                  setExito("");
                }}
                disabled={cargando}
              >
                <option value="">Seleccione una solicitud</option>
                {solicitudes.map((s) => (
                  <option key={s.solicitudId} value={s.solicitudId}>
                    {s.numeroProceso || `SOL-${String(s.solicitudId).padStart(4, "0")}`} ·{" "}
                    {s.carreraOrigen || "-"} → {s.carreraDestino || "-"}
                  </option>
                ))}
              </select>
            </div>
            <div className="col-md-4">
              <label htmlFor="correoDocenteUno" className="form-label fw-bold">
                Correo del docente 1
              </label>
              <input
                id="correoDocenteUno"
                type="email"
                className="form-control"
                value={correoUno}
                onChange={(e) => setCorreoUno(e.target.value)}
                placeholder="docente@uta.edu.ec"
              />
            </div>
            <div className="col-md-4">
              <label htmlFor="correoDocenteDos" className="form-label fw-bold">
                Correo del docente 2 (opcional)
              </label>
              <input
                id="correoDocenteDos"
                type="email"
                className="form-control"
                value={correoDos}
                onChange={(e) => setCorreoDos(e.target.value)}
                placeholder="docente2@uta.edu.ec"
              />
            </div>
            <div className="col-md-4 d-flex gap-2">
              <button type="submit" className="btn btn-primary fw-bold" disabled={delegando || !solicitudActivaId}>
                {delegando ? "Delegando..." : "Delegar"}
              </button>
              <button
                type="button"
                className="btn btn-outline-secondary"
                onClick={cargarConsolidado}
                disabled={cargandoConsolidado || !solicitudActivaId}
              >
                {cargandoConsolidado ? "Actualizando..." : "Actualizar consolidado"}
              </button>
            </div>
          </form>

          <hr className="my-4" />

          <h5 className="fw-bold">Datos del titular del récord</h5>
          <p className="text-muted small">
            Cargue aquí el nombre y la cédula que aparecen en el récord académico. También pueden
            dejarse vacíos si el agente los extrae del PDF; guárdelos antes de delegar para que el
            Docente los vea en su revisión. Guardar no cambia el estado de la solicitud.
          </p>
          <div className="row g-3 align-items-end">
            <div className="col-md-4">
              <label htmlFor="titularDelegarNombre" className="form-label fw-bold">
                Nombre del estudiante
              </label>
              <input
                id="titularDelegarNombre"
                className="form-control"
                value={camposFinales.nombreEstudiante}
                onChange={(e) => cambiarCampoFinal("nombreEstudiante", e.target.value)}
                placeholder="Nombre según récord"
              />
            </div>
            <div className="col-md-4">
              <label htmlFor="titularDelegarCedula" className="form-label fw-bold">
                Cédula del estudiante
              </label>
              <input
                id="titularDelegarCedula"
                className="form-control"
                value={camposFinales.cedulaEstudiante}
                onChange={(e) => cambiarCampoFinal("cedulaEstudiante", e.target.value)}
                placeholder="C.I. según récord"
              />
            </div>
            <div className="col-md-4">
              <button
                type="button"
                className="btn btn-outline-primary w-100"
                onClick={guardarTitular}
                disabled={cargando || !solicitudActivaId}
              >
                {cargando ? "Guardando..." : "Guardar titular"}
              </button>
            </div>
          </div>

          <hr className="my-4" />

          <h5 className="fw-bold">Estado de las delegaciones</h5>
          {delegaciones.length === 0 ? (
            <p className="text-muted small mb-0">
              Aún no hay delegaciones registradas para esta solicitud.
            </p>
          ) : (
            <div className="table-responsive">
              <table className="table table-bordered align-middle small mb-0">
                <thead className="table-light">
                  <tr>
                    <th>Docente</th>
                    <th>Estado</th>
                    <th>Revisado por</th>
                    <th>Informe sugerido</th>
                  </tr>
                </thead>
                <tbody>
                  {delegaciones.map((d, indice) => {
                    const texto =
                      d?.informeTexto ||
                      d?.informeSugeridoTexto ||
                      d?.informeSugerido ||
                      d?.informe?.texto ||
                      "-";
                    const badge = estadoDelegacionBadge(d?.estado || d?.estadoDelegacion);
                    return (
                      <tr key={d?.id ?? indice}>
                        <td>{correoDelegado(d)}</td>
                        <td><span className={`badge ${badge.clase}`}>{badge.texto}</span></td>
                        <td>{revisadoPor(d)}</td>
                        <td>{texto.length > 120 ? `${texto.slice(0, 120)}…` : texto}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
        </>
      )}

      {mostrarSugeridos && (
        <>
      {/* Resultados consolidados */}
      <div className="card shadow-sm border-0 mb-4">
        <div className="card-header bg-primary text-white d-flex justify-content-between align-items-center gap-3">
          <h4 className="mb-0 fw-bold">
            {vista === "sugeridos"
              ? "INFORMES SUGERIDOS DEL DOCENTE"
              : vista === "final"
                ? "REVISIÓN DEL INFORME DOCENTE"
                : "RESULTADOS CONSOLIDADOS"}
          </h4>
          <button
            type="button"
            className="btn btn-light btn-sm fw-bold text-primary flex-shrink-0"
            onClick={descargarSugerido}
            disabled={!textoSugerido || descargandoSugerido}
            title="Descarga el memorando completo del Docente en PDF"
          >
            {descargandoSugerido ? "Descargando..." : "⬇ Descargar informe sugerido del Docente"}
          </button>
        </div>
        <div className="card-body p-4">
          {cargandoConsolidado && <div className="alert alert-secondary">Cargando consolidado...</div>}

          {consolidado?.revisadoPor && (
            <div className="alert alert-success small">
              <strong>Revisado y sugerido por {consolidado.revisadoPor}</strong>
              {consolidado.resultadoDocente && ` · Resultado: ${consolidado.resultadoDocente}`}.
              {" "}El informe final ya puede consolidarse.
            </div>
          )}
          {!consolidado?.revisadoPor && !cargandoConsolidado && (
            <div className="alert alert-warning small">
              El Docente aún no ha emitido su informe sugerido. El informe final se habilita
              cuando uno de los docentes delegados registre su revisión.
            </div>
          )}

          {delegaciones.length === 0 ? (
            <p className="text-muted mb-4">
              Aún no hay delegaciones registradas. Delegate docentes para ver aquí el estado
              de cada revisión.
            </p>
          ) : (
            <div className="table-responsive mb-4">
              <table className="table table-bordered align-middle">
                <thead className="table-light">
                  <tr>
                    <th>Docente</th>
                    <th>Estado</th>
                    <th>Revisado por</th>
                    <th>Informe sugerido</th>
                  </tr>
                </thead>
                <tbody>
                  {delegaciones.map((d, indice) => {
                    const texto =
                      d?.informeTexto ||
                      d?.informeSugeridoTexto ||
                      d?.informeSugerido ||
                      d?.informe?.texto ||
                      "-";
                    return (
                      <tr key={d?.id ?? indice}>
                        <td>{correoDelegado(d)}</td>
                        <td>
                          {(() => {
                            const badge = estadoDelegacionBadge(d?.estado || d?.estadoDelegacion);
                            return <span className={`badge ${badge.clase}`}>{badge.texto}</span>;
                          })()}
                        </td>
                        <td>{revisadoPor(d)}</td>
                        <td className="small">
                          {texto.length > 220 ? `${texto.slice(0, 220)}…` : texto}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {textoSugerido && (
            <>
              <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                <h5 className="fw-bold mb-0">Informe sugerido del Docente</h5>
                <span className="text-muted small">
                  Revisado por {revisadoPorDocente} · Resultado {resultadoDocente}
                </span>
              </div>

              {filasSugeridas.length > 0 ? (
                <div className="table-responsive">
                  <table className="table table-sm table-bordered align-middle">
                    <thead className="table-light">
                      <tr>
                        <th style={{ width: "3rem" }}>N.º</th>
                        <th>Materia de origen</th>
                        <th>Equivalente en destino</th>
                        <th className="text-center" style={{ width: "5rem" }}>Contenido</th>
                        <th className="text-center" style={{ width: "6rem" }}>¿Se homologa?</th>
                        <th>Justificación del Docente</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filasSugeridas.map((f, indice) => {
                        const homologa = String(f.seHomologa || "").trim().toUpperCase().startsWith("S");
                        return (
                          <tr key={f.asignaturaId ?? indice}>
                            <td className="text-center">{indice + 1}</td>
                            <td>{f.asignaturaOrigen || "-"}</td>
                            <td>{f.asignaturaDestino || "Sin equivalente en la malla destino"}</td>
                            <td className="text-center">{f.contenido || "-"}</td>
                            <td className="text-center">
                              <span className={`badge ${homologa ? "bg-success" : "bg-secondary"}`}>
                                {homologa ? "SÍ" : "NO"}
                              </span>
                            </td>
                            <td className="small">{f.justificacion || "-"}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              ) : (
                <pre className="bg-light border rounded p-3" style={{ whiteSpace: "pre-wrap" }}>
                  {textoSugerido}
                </pre>
              )}

              <details className="mt-3">
                <summary className="small fw-semibold">Ver texto completo del informe sugerido</summary>
                <pre className="bg-light border rounded p-3 mt-2 mb-0" style={{ whiteSpace: "pre-wrap" }}>
                  {textoSugerido}
                </pre>
              </details>
              <p className="text-muted small mt-2 mb-0">
                El PDF descargado contiene el memorando completo con membrete, firma y la tabla
                formal de asignaturas evaluadas.
              </p>
            </>
          )}

          {textoFinal && (
            <>
              <h5 className="fw-bold">Informe final emitido</h5>
              <pre className="bg-light border rounded p-3" style={{ whiteSpace: "pre-wrap" }}>
                {textoFinal}
              </pre>
            </>
          )}
        </div>
      </div>
        </>
      )}

      {mostrarFinal && (
        <>
      {/* Informe final */}
      <div className="card shadow-sm border-0 mb-4">
        <div className="card-header bg-primary text-white">
          <h4 className="mb-0 fw-bold">GENERAR INFORME FINAL</h4>
        </div>
        <div className="card-body p-4">
          <div className="alert alert-info">
            El informe final se envía al <strong>Consejo Directivo</strong>, que solo es
            destinatario del documento. El Consejo Directivo no tiene un rol en el sistema.
          </div>

          {puedeGenerarFinal ? (
            <div className="alert alert-success">
              <strong>Ya revisado y sugerido por el Docente.</strong> Revisión de{" "}
              {revisadoPorDocente}
              {resultadoDocente !== "-" && ` · Resultado: ${resultadoDocente}`}. Puede consolidar
              el informe final y enviarlo al Consejo Directivo.
            </div>
          ) : (
            <div className="alert alert-warning">
              <strong>Aún no puede generar el informe final.</strong> Primero debe delegar a un
              Docente y esperar a que emita su informe sugerido; aquí se mostrará quién revisó
              la solicitud y su resultado.
            </div>
          )}

          <div className="alert alert-light border small mb-4">
            <div className="mb-1">
              <strong>Modalidad:</strong>{" "}
              <span
                className={`badge ${
                  moduloActivo === "EXTERNO" ? "bg-info text-dark" : "bg-primary"
                }`}
              >
                {moduloActivo}
              </span>
            </div>
            <div className="mb-1">
              <strong>Registrado por:</strong> {comiteActivo}
            </div>
            <div>
              <strong>Coordinador:</strong> {coordinadorActivo}
            </div>
          </div>

          <h5 className="fw-bold">Datos del récord académico (titular del trámite)</h5>
          <p className="text-muted small">
            El titular no es un usuario del sistema: el Coordinador entrega el récord académico, ya sea
            interno o externo, y sus datos se usan en el informe. Verifique y corrija aquí lo que
            extrajo el agente antes de generar el documento.
          </p>
          <div className="row g-3 mb-4">
            <div className="col-md-6">
              <label htmlFor="nombreEstudiante" className="form-label fw-bold">
                Nombre del estudiante (según récord)
              </label>
              <input
                id="nombreEstudiante"
                className="form-control"
                value={camposFinales.nombreEstudiante}
                onChange={(e) => cambiarCampoFinal("nombreEstudiante", e.target.value)}
                placeholder="Nombre tomado del récord académico"
              />
            </div>
            <div className="col-md-6">
              <label htmlFor="cedulaEstudiante" className="form-label fw-bold">
                Cédula del estudiante (según récord)
              </label>
              <input
                id="cedulaEstudiante"
                className="form-control"
                value={camposFinales.cedulaEstudiante}
                onChange={(e) => cambiarCampoFinal("cedulaEstudiante", e.target.value)}
                placeholder="C.I. tomada del récord académico"
              />
            </div>
          </div>
          <div className="d-flex justify-content-end mb-4">
            <button
              type="button"
              className="btn btn-outline-primary btn-sm"
              onClick={guardarTitular}
              disabled={cargando || !solicitudActivaId}
            >
              {cargando ? "Guardando..." : "Guardar titular"}
            </button>
          </div>

          <h5 className="fw-bold">Firmante del informe (Coordinador)</h5>
          <div className="row g-3">
            <div className="col-md-6">
              <label htmlFor="numeroMemorando" className="form-label fw-bold">
                Número de memorando (opcional)
              </label>
              <input
                id="numeroMemorando"
                className="form-control"
                value={camposFinales.numeroMemorando}
                onChange={(e) => cambiarCampoFinal("numeroMemorando", e.target.value)}
              />
            </div>
            <div className="col-md-6">
              <label htmlFor="nombreFirmante" className="form-label fw-bold">
                Nombre del firmante (opcional)
              </label>
              <input
                id="nombreFirmante"
                className="form-control"
                value={camposFinales.nombreFirmante}
                onChange={(e) => cambiarCampoFinal("nombreFirmante", e.target.value)}
                placeholder="Si se deja vacío, firma el Coordinador en sesión"
              />
            </div>
          </div>

          {asignaturasFinales.length > 0 && (
            <div className="mt-4">
              <h5 className="fw-bold">Decisión final por asignatura</h5>
              <div className="table-responsive">
                <table className="table table-bordered align-middle">
                  <thead className="table-light">
                    <tr>
                      <th>Asignatura</th>
                      <th>Decisión</th>
                      <th>Observación</th>
                    </tr>
                  </thead>
                  <tbody>
                    {asignaturasFinales.map((a, indice) => (
                      <tr key={a.asignaturaId ?? indice}>
                        <td>{a.asignaturaId}</td>
                        <td>
                          <select
                            className="form-select form-select-sm"
                            value={a.decision}
                            onChange={(e) =>
                              cambiarAsignaturaFinal(indice, "decision", e.target.value)
                            }
                          >
                            {DECISIONES_FINALES.map((decision) => (
                              <option key={decision} value={decision}>
                                {decision}
                              </option>
                            ))}
                          </select>
                        </td>
                        <td>
                          <input
                            type="text"
                            className="form-control form-control-sm"
                            value={a.observacion}
                            onChange={(e) =>
                              cambiarAsignaturaFinal(indice, "observacion", e.target.value)
                            }
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          <div className="d-flex flex-wrap gap-2 mt-3">
            <button
              type="button"
              className="btn btn-success fw-bold"
              onClick={generarInformeFinal}
              disabled={generandoFinal || !solicitudActivaId || !puedeGenerarFinal}
            >
              {generandoFinal ? "Generando..." : "Generar informe final"}
            </button>
            {informeFinalGenerado && (
              <button
                type="button"
                className="btn btn-outline-primary"
                onClick={descargarFinal}
                disabled={descargandoFinal}
              >
                {descargandoFinal ? "Descargando..." : "Descargar PDF final"}
              </button>
            )}
          </div>
        </div>
      </div>
        </>
      )}
    </div>
  );
}
