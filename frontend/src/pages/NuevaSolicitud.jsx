import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

export default function NuevaSolicitud() {
  const navigate = useNavigate();
  const [carreras, setCarreras] = useState([]);
  const [asignaturasDisponibles, setAsignaturasDisponibles] = useState([]);
  
  const [tipo, setTipo] = useState("Cambio de carrera");
  const [origen, setOrigen] = useState("");
  const [destino, setDestino] = useState("");
  const [seleccionadas, setSeleccionadas] = useState([]);
  
  const [mensaje, setMensaje] = useState("");
  const [cargando, setCargando] = useState(false);

  // 1. Cargar las carreras al inicio
  useEffect(() => {
    const token = localStorage.getItem("token");
    fetch("http://localhost:8080/carreras", {
      headers: { "Authorization": `Bearer ${token}` }
    })
      .then(res => res.json())
      .then(data => setCarreras(data))
      .catch(() => console.error("Error cargando carreras"));
  }, []);

  // 2. Cuando cambia la carrera de origen, cargar sus asignaturas
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
        setSeleccionadas([]); // Limpiamos selección previa
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

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!origen || !destino) return alert("Seleccione carrera de origen y destino");
    if (origen === destino) return alert("La carrera de origen y destino no pueden ser iguales");
    if (seleccionadas.length === 0) return alert("Seleccione al menos una asignatura para homologar");
    
    setCargando(true);
    setMensaje("");
    const token = localStorage.getItem("token");
    
    try {
      const res = await fetch("http://localhost:8080/solicitudes", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Authorization": `Bearer ${token}` },
        body: JSON.stringify({ 
          tipo, 
          carreraOrigenId: parseInt(origen), 
          carreraDestinoId: parseInt(destino), 
          asignaturasIds: seleccionadas
        })
      });
      
      if (res.ok) {
        setMensaje("¡Solicitud registrada con éxito!");
        setTimeout(() => navigate("/mis-solicitudes"), 1500);
      } else {
        const errorData = await res.json().catch(() => ({}));
        setMensaje(errorData.error || "Error al registrar la solicitud.");
      }
    } catch {
      setMensaje("Error de conexión con el servidor.");
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
              {mensaje && (
                <div className={`alert ${mensaje.includes("éxito") ? "alert-success" : "alert-danger"}`}>
                  {mensaje}
                </div>
              )}
              
              <form onSubmit={handleSubmit}>
                <div className="row">
                  {/* COLUMNA IZQUIERDA */}
                  <div className="col-md-6 border-end">
                    <div className="mb-3">
                      <label className="form-label fw-bold">Tipo de solicitud</label>
                      <select className="form-select" value={tipo} onChange={(e) => setTipo(e.target.value)}>
                        <option value="Cambio de carrera">Cambio de carrera</option>
                        <option value="Homologación">Homologación</option>
                      </select>
                    </div>
                    
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

                  {/* COLUMNA DERECHA (Asignaturas) */}
                  <div className="col-md-6 ps-md-4">
                    <label className="form-label fw-bold">Asignaturas a homologar</label>
                    <div className="border rounded p-3 bg-light" style={{ maxHeight: '250px', overflowY: 'auto' }}>
                      {!origen ? (
                        <p className="text-muted small">Seleccione una carrera de origen para ver las asignaturas.</p>
                      ) : asignaturasDisponibles.length === 0 ? (
                        <p className="text-muted small">Cargando asignaturas...</p>
                      ) : (
                        asignaturasDisponibles.map(a => (
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