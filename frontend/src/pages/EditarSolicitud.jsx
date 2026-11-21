import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { obtenerSolicitud, actualizarSolicitud } from "../services/solicitudesService";

export default function EditarSolicitud() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [formData, setFormData] = useState({ carreraOrigen: "", carreraDestino: "" });
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");
  const [cargando, setCargando] = useState(false);

  useEffect(() => {
    // Cargar los datos de la solicitud para precargar el formulario
    const cargarDatos = async () => {
      try {
        const data = await obtenerSolicitud(id);
        setFormData({ carreraOrigen: data.carreraOrigen, carreraDestino: data.carreraDestino });
      } catch (err) {
        setError("No se pudieron cargar los datos de la solicitud.");
      }
    };
    cargarDatos();
  }, [id]);

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(""); setExito(""); setCargando(true);

    try {
      await actualizarSolicitud(id, formData);
      setExito("Solicitud actualizada correctamente.");
      setTimeout(() => navigate("/mis-solicitudes"), 2000); // Redirigir en 2 segundos
    } catch (err) {
      // Aquí se capturan los errores 409 (en revisión) y 403 (no es dueño)
      setError(err.message);
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-8">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Editar Solicitud</h4>
            </div>
            <div className="card-body p-4">
              {error && <div className="alert alert-danger">{error}</div>}
              {exito && <div className="alert alert-success">{exito}</div>}
              
              <form onSubmit={handleSubmit}>
                <div className="mb-3">
                  <label className="form-label fw-bold">Carrera de Origen</label>
                  <input type="text" className="form-control" name="carreraOrigen" value={formData.carreraOrigen} onChange={handleChange} required />
                </div>
                <div className="mb-4">
                  <label className="form-label fw-bold">Carrera de Destino</label>
                  <input type="text" className="form-control" name="carreraDestino" value={formData.carreraDestino} onChange={handleChange} required />
                </div>
                
                <div className="d-flex justify-content-between">
                  <button type="button" className="btn btn-secondary px-4" onClick={() => navigate("/mis-solicitudes")}>Cancelar</button>
                  <button type="submit" className="btn btn-primary px-4" disabled={cargando}>
                    {cargando ? "Guardando..." : "Guardar Cambios"}
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