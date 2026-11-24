import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import FiltroNivel from '../components/FiltroNivel';
import { analizarSolicitud } from '../services/homologacionService';
import { recomendarPorCarrera } from '../services/docenteService';
import { useAuth } from '../context/AuthContext';

const GATEWAY_URL = import.meta.env.VITE_GATEWAY_URL || "http://localhost:8080";

export default function NuevaSolicitud() {
  const navigate = useNavigate();
  const { rol } = useAuth();
  const [carreras, setCarreras] = useState([]);
  const [asignaturasDisponibles, setAsignaturasDisponibles] = useState([]);
  
  const [modulo, setModulo] = useState("INTERNO");
  const [universidadOrigen, setUniversidadOrigen] = useState("");
  const [universidadDestino, setUniversidadDestino] = useState("");
  const [estudianteNombre, setEstudianteNombre] = useState("");
  const [estudianteCedula, setEstudianteCedula] = useState("");
  const [origen, setOrigen] = useState("");
  const [destino, setDestino] = useState("");
  const [seleccionadas, setSeleccionadas] = useState([]);
  
  const [nivelSeleccionado, setNivelSeleccionado] = useState("Todos");
  
  const [mensaje, setMensaje] = useState("");
  const [tipoMensaje, setTipoMensaje] = useState("danger");
  const [cargando, setCargando] = useState(false);
  const [errorReintento, setErrorReintento] = useState(false);
  const [recomendacion, setRecomendacion] = useState(null);
  const [recomendando, setRecomendando] = useState(false);
  const [errorRecomendacion, setErrorRecomendacion] = useState("");

  useEffect(() => {
    const token = sessionStorage.getItem("token");
    fetch(`${GATEWAY_URL}/carreras`, {
      headers: { "Authorization": `Bearer ${token}` }
    })
      .then(res => res.json())
      .then(data => setCarreras(data))
      .catch(() => console.error("Error cargando carreras"));
  }, []);

  useEffect(() => {
    if (!origen) {
      const timer = setTimeout(() => {
        setAsignaturasDisponibles([]);
        setSeleccionadas([]);
      }, 0);
      return () => clearTimeout(timer);
    }
    
    const token = sessionStorage.getItem("token");
    fetch(`${GATEWAY_URL}/carreras/${origen}/asignaturas`, {
      headers: { "Authorization": `Bearer ${token}` }
    })
      .then(res => res.json())
      .then(data => {
        setAsignaturasDisponibles(data);
        setSeleccionadas([]);
        setNivelSeleccionado("Todos");
      })
      .catch(() => console.error("Error cargando asignaturas"));
  }, [origen]);

  const toggleAsignatura = (id) => {
    if (seleccionadas.includes(id)) {
      setSeleccionadas(seleccionadas.filter(a => a !== id));
    } else {
      setSeleccionadas([...seleccionadas, id]);
    }
  };

  const asignaturasFiltradas = asignaturasDisponibles.filter(
    (a) => nivelSeleccionado === "Todos" || a.nivel === nivelSeleccionado
  );

  const pedirRecomendacion = async () => {
    if (!origen || !destino) {
      return alert("Seleccione la carrera de origen y la de destino para pedir la recomendación");
    }
    if (origen === destino) {
      return alert("La carrera de origen y destino no pueden ser iguales");
    }

    setRecomendando(true);
    setErrorRecomendacion("");
    setRecomendacion(null);
    try {
      const resultado = await recomendarPorCarrera({
        carreraOrigenId: parseInt(origen),
        carreraDestinoId: parseInt(destino),
        asignaturasOrigenIds: seleccionadas,
      });
      setRecomendacion(resultado || { recomendaciones: [] });
    } catch (e) {
      setErrorRecomendacion(
        e.message || "No se pudo obtener la recomendación del agente interno."
      );
    } finally {
      setRecomendando(false);
    }
  };

  // Solo se muestran las materias que el agente interno marca como homologables.
  const recomendacionesAptas = (recomendacion?.recomendaciones || []).filter(
    (r) => r.homologable
  );

  const handleSubmit = async (e) => {
    if (e) e.preventDefault();
    
    if (!origen || !destino) return alert("Seleccione carrera de origen y destino");
    if (origen === destino) return alert("La carrera de origen y destino no pueden ser iguales");
    if (estudianteCedula.trim() && !/^\d{10}$/.test(estudianteCedula.trim())) {
      return alert("La cédula del estudiante debe tener exactamente 10 dígitos.");
    }
    if (seleccionadas.length === 0) return alert("Seleccione al menos una asignatura para homologar");

    const esExterno = modulo === "EXTERNO";
    if (esExterno && (!universidadOrigen.trim() || !universidadDestino.trim())) {
      return alert("Para el agente externo indique la universidad de origen y la de destino");
    }
    
    setCargando(true);
    setMensaje("");
    setErrorReintento(false);
    const token = sessionStorage.getItem("token");
    
    try {
      const res = await fetch(`${GATEWAY_URL}/solicitudes`, {
        method: "POST",
        headers: { "Content-Type": "application/json", "Authorization": `Bearer ${token}` },
        body: JSON.stringify({ 
          tipo: "Homologación",
          modulo,
          universidadOrigen: esExterno ? universidadOrigen.trim() : null,
          universidadDestino: esExterno ? universidadDestino.trim() : null,
          carreraOrigenId: parseInt(origen), 
          carreraDestinoId: parseInt(destino), 
          asignaturasIds: seleccionadas,
          estudianteNombre: estudianteNombre.trim(),
          estudianteCedula: estudianteCedula.trim()
          // ELIMINADO: estudianteCorreo (el backend lo saca del token)
        })
      });
      
      if (res.ok) {
        const creada = await res.json().catch(() => ({}));
        const solicitudId = creada?.id ?? creada?.data?.id;

        if (!solicitudId) {
          setTipoMensaje("warning");
          setMensaje("La solicitud se registró, pero no se recibió su identificador. Revise Mis solicitudes.");
          setTimeout(() => navigate("/mis-solicitudes"), 2000);
          return;
        }

        // El registro NO pide el informe a la IA. Solo guarda la solicitud y su análisis
        // técnico; el informe lo genera y envía el Docente después de la delegación.
        if (esExterno) {
          setTipoMensaje("success");
          setMensaje(
            "Solicitud externa registrada. Ahora registre la URL de la carpeta y adjunte el récord; después delegue al Docente."
          );
          setTimeout(
            () => navigate(`/ruta-carpeta?solicitudId=${solicitudId}&modo=URL`),
            1200
          );
          return;
        }

        // Análisis técnico determinista de las materias seleccionadas (no es el informe IA).
        setTipoMensaje("info");
        setMensaje("Solicitud registrada. Guardando el análisis técnico de las materias...");
        try {
          await analizarSolicitud(solicitudId);
          setTipoMensaje("success");
          setMensaje(
            "Solicitud registrada. Continúe subiendo el récord académico; luego podrá delegar al Docente."
          );
        } catch (e) {
          setTipoMensaje("warning");
          setMensaje(
            `Solicitud registrada, pero el análisis técnico no se pudo generar: ${e.message || "error desconocido"}. Puede continuar subiendo el récord y delegar.`
          );
        }

        setTimeout(
          () => navigate(`/adjuntar-documentos?solicitudId=${solicitudId}`),
          1600
        );
      } else {
        const errorData = await res.json().catch(() => ({}));
        setTipoMensaje("danger");
        setMensaje(errorData.error || "Error al registrar la solicitud.");
      }
    } catch (err) {
      setTipoMensaje("danger");
      setMensaje(err.message || "No se pudo registrar la solicitud. Intente nuevamente.");
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-10">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Registrar Nueva Solicitud</h4>
              <small className="d-block fw-normal">
                Solicitud registrada por el {rol === "COORDINADOR" ? "Coordinador" : "Comité"}
              </small>
            </div>
            <div className="card-body p-4">
              
              {errorReintento && (
                <div className="alert alert-warning d-flex justify-content-between align-items-center">
                  <span>{mensaje}</span>
                  <button 
                    type="button" 
                    className="btn btn-sm btn-outline-dark" 
                    onClick={() => handleSubmit(null)}
                  >
                    Reintentar
                  </button>
                </div>
              )}

              {!errorReintento && mensaje && (
                <div className={`alert alert-${tipoMensaje}`}>
                  {mensaje}
                </div>
              )}
              
              <form onSubmit={handleSubmit}>
                <div className="row">
                  <div className="col-md-6 border-end">
                    
                    {/* CAMPO ESTÁTICO HOMOLOGACIÓN */}
                    <div className="mb-3">
                      <label className="form-label fw-bold">Tipo de solicitud</label>
                      <input type="text" className="form-control" value="Homologación" readOnly disabled />
                    </div>

                    <div className="mb-3">
                      <label className="form-label fw-bold">Modalidad de homologación</label>
                      <div className="btn-group w-100" role="group" aria-label="Modalidad de homologación">
                        <button
                          type="button"
                          className={`btn btn-outline-primary ${modulo === "INTERNO" ? "active" : ""}`}
                          onClick={() => setModulo("INTERNO")}
                        >
                          INTERNO · Agente UTA
                        </button>
                        <button
                          type="button"
                          className={`btn btn-outline-primary ${modulo === "EXTERNO" ? "active" : ""}`}
                          onClick={() => setModulo("EXTERNO")}
                        >
                          EXTERNO · Otra universidad
                        </button>
                      </div>
                      <div className="form-text">
                        En ambas modalidades el Coordinador entrega el récord académico: de ahí se toman
                        el nombre y la cédula del titular para el informe.
                      </div>
                    </div>

                    <div className="card border-light bg-light mb-3">
                      <div className="card-body p-3">
                        <h6 className="fw-bold mb-2">Datos del titular del récord (opcionales)</h6>
                        <div className="row g-2">
                          <div className="col-md-6">
                            <label htmlFor="estudianteNombre" className="form-label small mb-1">
                              Nombre del estudiante
                            </label>
                            <input
                              id="estudianteNombre"
                              type="text"
                              className="form-control form-control-sm"
                              value={estudianteNombre}
                              onChange={(e) => setEstudianteNombre(e.target.value)}
                              placeholder="Si se deja vacío, se toma del récord"
                            />
                          </div>
                          <div className="col-md-6">
                            <label htmlFor="estudianteCedula" className="form-label small mb-1">
                              Cédula del estudiante
                            </label>
                            <input
                              id="estudianteCedula"
                              type="text"
                              inputMode="numeric"
                              maxLength={10}
                              className="form-control form-control-sm"
                              value={estudianteCedula}
                              onChange={(e) => setEstudianteCedula(e.target.value.replace(/\D/g, "").slice(0, 10))}
                              placeholder="10 dígitos; si se deja vacío, se toma del récord"
                            />
                          </div>
                        </div>
                        <div className="form-text mt-1">
                          El titular no es usuario del sistema. Si no los conoce, déjelos vacíos: se
                          completan al adjuntar el récord y se pueden corregir en Delegar docentes.
                        </div>
                      </div>
                    </div>

                    {modulo === "EXTERNO" && (
                      <>
                        <div className="mb-3">
                          <label className="form-label fw-bold">Universidad de origen</label>
                          <input
                            type="text"
                            className="form-control"
                            placeholder="Ej.: Universidad Nacional de Colombia"
                            value={universidadOrigen}
                            onChange={(e) => setUniversidadOrigen(e.target.value)}
                            required
                          />
                        </div>
                        <div className="mb-3">
                          <label className="form-label fw-bold">Universidad de destino</label>
                          <input
                            type="text"
                            className="form-control"
                            placeholder="Ej.: Universidad Técnica de Ambato"
                            value={universidadDestino}
                            onChange={(e) => setUniversidadDestino(e.target.value)}
                            required
                          />
                        </div>
                        <div className="alert alert-info small py-2">
                          Tras registrar la solicitud deberá adjuntar la malla, el historial y los sílabos.
                          El agente externo analizará esos documentos sin reglas fijas.
                        </div>
                      </>
                    )}
                    
                    <div className="mb-3">
                      <label className="form-label fw-bold">Carrera Origen</label>
                      <select
                        className="form-select"
                        value={origen}
                        onChange={(e) => {
                          const valor = e.target.value;
                          setOrigen(valor);
                          if (valor && valor === destino) setDestino("");
                        }}
                        required
                      >
                        <option value="">Seleccione una carrera...</option>
                        {carreras.map(c => (
                          <option key={c.id} value={c.id} disabled={String(c.id) === String(destino)}>
                            {c.nombre}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="mb-3">
                      <label className="form-label fw-bold">Carrera Destino</label>
                      <select
                        className="form-select"
                        value={destino}
                        onChange={(e) => {
                          const valor = e.target.value;
                          setDestino(valor);
                          if (valor && valor === origen) setOrigen("");
                        }}
                        required
                      >
                        <option value="">Seleccione una carrera...</option>
                        {carreras.map(c => (
                          <option key={c.id} value={c.id} disabled={String(c.id) === String(origen)}>
                            {c.nombre}
                          </option>
                        ))}
                      </select>
                      <div className="form-text">La carrera de destino debe ser diferente a la de origen.</div>
                    </div>

                    <div className="alert alert-info small py-2 mb-3">
                      Aquí solo se registra la solicitud. Después suba el récord académico y delegue al
                      Docente: él revisará, editará y enviará el informe sugerido. Para modalidad externa,
                      además registre la URL/carpeta compartida. <strong>Desarrollo de Proyectos,
                      Titulación y Prácticas Laborales no se homologan</strong> por regla institucional
                      y aparecerán como “No” en la revisión.
                    </div>
                  </div>

                  <div className="col-md-6 ps-md-4">
                    <label className="form-label fw-bold">Asignaturas a homologar</label>
                    
                    <FiltroNivel 
                      nivelSeleccionado={nivelSeleccionado} 
                      onChange={setNivelSeleccionado} 
                    />

                    <div className="border rounded p-3 bg-light" style={{ maxHeight: '250px', overflowY: 'auto' }}>
                      {!origen ? (
                        <p className="text-muted small">Seleccione una carrera de origen para ver las asignaturas.</p>
                      ) : asignaturasDisponibles.length === 0 ? (
                        <p className="text-muted small">Cargando asignaturas...</p>
                      ) : asignaturasFiltradas.length === 0 ? (
                        <p className="text-muted small">No hay asignaturas en este nivel.</p>
                      ) : (
                        asignaturasFiltradas.map(a => (
                          <div className="form-check mb-2" key={a.id}>
                            <input 
                              className="form-check-input" 
                              type="checkbox" 
                              id={`asig-${a.id}`}
                              checked={seleccionadas.includes(a.id)}
                              onChange={() => toggleAsignatura(a.id)}
                            />
                            <label className="form-check-label" htmlFor={`asig-${a.id}`}>
                              {a.nombre} <span className="text-muted small">({a.nivel})</span>
                            </label>
                          </div>
                        ))
                      )}
                    </div>

                    {modulo === "INTERNO" && (
                      <div className="mt-2 text-end">
                        <button
                          type="button"
                          className="btn btn-link btn-sm p-0"
                          onClick={pedirRecomendacion}
                          disabled={recomendando || cargando}
                          title="Solo una recomendación; no decide la homologación ni genera el informe final"
                        >
                          {recomendando
                            ? "Consultando recomendación..."
                            : "Recomendación de materia a homologar"}
                        </button>

                        {errorRecomendacion && (
                          <div className="small text-danger mt-1">{errorRecomendacion}</div>
                        )}

                        {recomendacion && !recomendando && (
                          <div className="mt-2 text-start">
                            {recomendacionesAptas.length === 0 ? (
                              <div className="small text-muted">
                                No hay materias recomendadas para homologar.
                              </div>
                            ) : (
                              <>
                                <div
                                  className="border rounded p-2 bg-light"
                                  style={{ maxHeight: '160px', overflowY: 'auto' }}
                                >
                                  <table className="table table-sm table-borderless mb-0 small">
                                    <thead>
                                      <tr>
                                        <th>Materia de origen</th>
                                        <th>Destino sugerido</th>
                                        <th className="text-center">%</th>
                                      </tr>
                                    </thead>
                                    <tbody>
                                      {recomendacionesAptas.map((r) => (
                                        <tr key={r.asignaturaId}>
                                          <td>{r.asignaturaOrigen}</td>
                                          <td>{r.asignaturaDestino || "-"}</td>
                                          <td className="text-center">{r.porcentajeContenido}%</td>
                                        </tr>
                                      ))}
                                    </tbody>
                                  </table>
                                </div>
                                <button
                                  type="button"
                                  className="btn btn-outline-primary btn-sm mt-2 py-0 px-2"
                                  onClick={() => {
                                    const ids = recomendacionesAptas
                                      .map((r) => Number(r.asignaturaId))
                                      .filter((id) => asignaturasDisponibles.some((a) => Number(a.id) === id));
                                    setSeleccionadas((prev) => Array.from(new Set([...prev, ...ids])));
                                  }}
                                >
                                  Aplicar recomendación
                                </button>
                              </>
                            )}
                            <div className="form-text mt-1">
                              Solo una recomendación; no decide ni genera el informe final.
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                </div>

                <div className="text-end mt-4">
                  <button type="submit" className="btn btn-primary px-5 fw-bold" disabled={cargando}>
                    {cargando ? "Registrando..." : "Registrar Solicitud"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}