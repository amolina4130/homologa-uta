import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import FiltroNivel from '../components/FiltroNivel';
import { analizarSolicitud, analizarSolicitudExterna } from '../services/homologacionService';

export default function NuevaSolicitud() {
  const navigate = useNavigate();
  const [carreras, setCarreras] = useState([]);
  const [asignaturasDisponibles, setAsignaturasDisponibles] = useState([]);
  
  // TIPO ESTÁTICO (FIJO)
  const tipo = "Homologación";
  
  const [modulo, setModulo] = useState("INTERNO");
  const [universidadOrigen, setUniversidadOrigen] = useState("");
  const [universidadDestino, setUniversidadDestino] = useState("");
  const [origen, setOrigen] = useState("");
  const [destino, setDestino] = useState("");
  const [seleccionadas, setSeleccionadas] = useState([]);
  
  const [nivelSeleccionado, setNivelSeleccionado] = useState("Todos");
  
  const [mensaje, setMensaje] = useState("");
  const [tipoMensaje, setTipoMensaje] = useState("danger");
  const [cargando, setCargando] = useState(false);
  const [errorReintento, setErrorReintento] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem("token");
    fetch("http://localhost:8080/carreras", {
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
    
    const token = localStorage.getItem("token");
    fetch(`http://localhost:8080/carreras/${origen}/asignaturas`, {
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

  const handleSubmit = async (e) => {
    if (e) e.preventDefault();
    
    if (!origen || !destino) return alert("Seleccione carrera de origen y destino");
    if (origen === destino) return alert("La carrera de origen y destino no pueden ser iguales");
    if (seleccionadas.length === 0) return alert("Seleccione al menos una asignatura para homologar");

    const esExterno = modulo === "EXTERNO";
    if (esExterno && (!universidadOrigen.trim() || !universidadDestino.trim())) {
      return alert("Para el agente externo indique la universidad de origen y la de destino");
    }
    
    setCargando(true);
    setMensaje("");
    setErrorReintento(false);
    const token = localStorage.getItem("token");
    
    try {
      const res = await fetch("http://localhost:8080/solicitudes", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Authorization": `Bearer ${token}` },
        body: JSON.stringify({ 
          tipo: "Homologación",
          modulo,
          universidadOrigen: esExterno ? universidadOrigen.trim() : null,
          universidadDestino: esExterno ? universidadDestino.trim() : null,
          carreraOrigenId: parseInt(origen), 
          carreraDestinoId: parseInt(destino), 
          asignaturasIds: seleccionadas
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

        // Flujo externo: primero hay que subir los documentos procesados y luego el agente externo analiza.
        if (esExterno) {
          setTipoMensaje("success");
          setMensaje("Solicitud externa registrada. Adjunte la malla, el historial y los sílabos para analizarla.");
          setTimeout(() => navigate(`/adjuntar-documentos?solicitudId=${solicitudId}&volver=resultado`), 1200);
          return;
        }

        // Encadenamos el análisis con las asignaturas reales de la solicitud.
        setTipoMensaje("info");
        setMensaje("Solicitud registrada. Generando el análisis del agente...");
        try {
          await analizarSolicitud(solicitudId);
          setTipoMensaje("success");
          setMensaje("¡Solicitud registrada y analizada con éxito!");
        } catch (e) {
          // La solicitud ya existe: el estudiante puede reintentar el análisis en la pantalla de resultado.
          setTipoMensaje("warning");
          setMensaje(
            `Solicitud registrada, pero el análisis no se pudo generar: ${e.message || "error desconocido"}.`
          );
        }

        setTimeout(() => navigate(`/resultado/${solicitudId}`), 1200);
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
                      <label className="form-label fw-bold">Módulo de análisis</label>
                      <select 
                        className="form-select" 
                        value={modulo} 
                        onChange={(e) => setModulo(e.target.value)}
                      >
                        <option value="INTERNO">Agente interno (reglas normativas UTA)</option>
                        <option value="EXTERNO">Agente externo (universidades externas)</option>
                      </select>
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
                      <select className="form-select" value={origen} onChange={(e) => setOrigen(e.target.value)} required>
                        <option value="">Seleccione una carrera...</option>
                        {carreras.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                      </select>
                    </div>
                    
                    <div className="mb-3">
                      <label className="form-label fw-bold">Carrera Destino</label>
                      <select className="form-select" value={destino} onChange={(e) => setDestino(e.target.value)} required>
                        <option value="">Seleccione una carrera...</option>
                        {carreras.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                      </select>
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