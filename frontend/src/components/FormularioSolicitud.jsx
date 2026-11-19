import { useState, useEffect } from "react";
import { obtenerCarreras, obtenerAsignaturas } from "../services/solicitudesService";

export default function FormularioSolicitud({ onSubmit, cargando }) {
  const [carreras, setCarreras] = useState([]);
  const [asignaturas, setAsignaturas] = useState([]);
  const [formData, setFormData] = useState({
    tipo: "Homologación", 
    modulo: "INTERNO", 
    carreraOrigenId: "",
    carreraDestinoId: "",
    universidadOrigen: "", 
    universidadDestino: "", 
    asignaturasIds: [],
  });
  const [errores, setErrores] = useState({});

  // Cargar las carreras al iniciar
  useEffect(() => {
    obtenerCarreras().then((data) => setCarreras(data.data || data)).catch(console.error);
  }, []);

  // Cuando cambia la carrera de origen, cargamos sus asignaturas
  useEffect(() => {
    if (formData.carreraOrigenId) {
      obtenerAsignaturas(formData.carreraOrigenId)
        .then((data) => setAsignaturas(data.data || data))
        .catch(console.error);
    } else {
      const timer = setTimeout(() => setAsignaturas([]), 0);
      return () => clearTimeout(timer);
    }
  }, [formData.carreraOrigenId]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const handleAsignaturaCheck = (id) => {
    setFormData((prev) => {
      const yaSeleccionada = prev.asignaturasIds.includes(id);
      return {
        ...prev,
        asignaturasIds: yaSeleccionada
          ? prev.asignaturasIds.filter((aId) => aId !== id)
          : [...prev.asignaturasIds, id],
      };
    });
  };

  const validar = () => {
    const e = {};
    if (!formData.carreraOrigenId) e.carreraOrigenId = "Seleccione la carrera de origen.";
    if (!formData.carreraDestinoId) e.carreraDestinoId = "Seleccione la carrera de destino.";
    if (formData.carreraOrigenId === formData.carreraDestinoId) e.carreraDestinoId = "La carrera de destino no puede ser igual a la de origen.";
    if (formData.asignaturasIds.length === 0) e.asignaturas = "Seleccione al menos una asignatura.";

    // Validación 400 para el módulo externo
    if (formData.modulo === "EXTERNO") {
      if (!formData.universidadOrigen.trim()) e.universidadOrigen = "Ingrese la universidad de origen.";
      if (!formData.universidadDestino.trim()) e.universidadDestino = "Ingrese la universidad de destino.";
    }

    return e;
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    const eObj = validar();
    setErrores(eObj);
    if (Object.keys(eObj).length === 0) {
      // Al enviar, si es interno, limpiamos las universidades para no enviar basura
      const payload = { ...formData };
      if (payload.modulo === "INTERNO") {
        delete payload.universidadOrigen;
        delete payload.universidadDestino;
      }
      onSubmit(payload);
    }
  };

  const campo = { width: "100%", padding: 10, boxSizing: "border-box", border: "2px solid #000", marginBottom: "5px" };
  const error = { color: "red", fontSize: 14, margin: "0 0 10px" };

  return (
    <form onSubmit={handleSubmit} noValidate style={{ display: "flex", gap: "30px", border: "2px solid #000", padding: "20px", marginTop: "20px" }}>
      
      {/* Columna Izquierda */}
      <div style={{ flex: 1 }}>
        <h3>Nueva solicitud de homologación</h3>
        
        {/* Selector de Módulo */}
        <label>Módulo de análisis</label>
        <select name="modulo" value={formData.modulo} onChange={handleChange} style={campo}>
          <option value="INTERNO">Agente interno (Cambio de carrera UTA)</option>
          <option value="EXTERNO">Agente externo (Universidad externa)</option>
        </select>

        {/* Campos condicionales para el módulo externo */}
        {formData.modulo === "EXTERNO" && (
          <div style={{ padding: "10px", backgroundColor: "#f0f0f0", marginBottom: "15px", border: "1px solid #ccc" }}>
            <label>Universidad de Origen</label>
            <input 
              type="text" 
              name="universidadOrigen" 
              value={formData.universidadOrigen} 
              onChange={handleChange} 
              style={campo} 
              placeholder="Ej. Universidad Central del Ecuador"
            />
            {errores.universidadOrigen && <p style={error}>{errores.universidadOrigen}</p>}

            <label>Universidad de Destino</label>
            <input 
              type="text" 
              name="universidadDestino" 
              value={formData.universidadDestino} 
              onChange={handleChange} 
              style={campo} 
              placeholder="Ej. Universidad Técnica de Ambato"
            />
            {errores.universidadDestino && <p style={error}>{errores.universidadDestino}</p>}
          </div>
        )}

        <label>Carrera de origen</label>
        <select name="carreraOrigenId" value={formData.carreraOrigenId} onChange={handleChange} style={campo}>
          <option value="">Seleccione una carrera</option>
          {carreras.map((c) => (
            <option key={c.id} value={c.id}>{c.nombre}</option>
          ))}
        </select>
        {errores.carreraOrigenId && <p style={error}>{errores.carreraOrigenId}</p>}

        <label>Carrera de destino</label>
        <select name="carreraDestinoId" value={formData.carreraDestinoId} onChange={handleChange} style={campo}>
          <option value="">Seleccione una carrera</option>
          {carreras.map((c) => (
            <option key={c.id} value={c.id}>{c.nombre}</option>
          ))}
        </select>
        {errores.carreraDestinoId && <p style={error}>{errores.carreraDestinoId}</p>}
      </div>

      {/* Columna Derecha */}
      <div style={{ flex: 1, borderLeft: "2px solid #000", paddingLeft: "20px" }}>
        <h4>Asignaturas a homologar</h4>
        {asignaturas.length === 0 ? (
          <p>Seleccione una carrera de origen para ver las asignaturas.</p>
        ) : (
          <div style={{ maxHeight: "200px", overflowY: "auto", border: "1px solid #ccc", padding: "10px" }}>
            {asignaturas.map((a) => (
              <div key={a.id} style={{ marginBottom: "8px" }}>
                <input
                  type="checkbox"
                  id={`asig-${a.id}`}
                  checked={formData.asignaturasIds.includes(a.id)}
                  onChange={() => handleAsignaturaCheck(a.id)}
                />
                <label htmlFor={`asig-${a.id}`} style={{ marginLeft: "8px" }}>{a.nombre}</label>
              </div>
            ))}
          </div>
        )}
        {errores.asignaturas && <p style={error}>{errores.asignaturas}</p>}

        <div style={{ marginTop: "30px", textAlign: "right" }}>
          <button type="submit" disabled={cargando} style={{ padding: "10px 32px", fontWeight: "bold", cursor: "pointer" }}>
            {cargando ? "Registrando..." : "Registrar solicitud"}
          </button>
        </div>
      </div>
    </form>
  );
}