import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { registro } from "../services/authService";

export default function Registro() {
  const navigate = useNavigate();
  const [nombre, setNombre] = useState("");
  const [correo, setCorreo] = useState("");
  const [contrasena, setContrasena] = useState("");
  const [confirmacion, setConfirmacion] = useState("");
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");
  const [cargando, setCargando] = useState(false);

  async function enviar(ev) {
    ev.preventDefault();
    setError("");
    setExito("");

    if (!nombre.trim()) return setError("Ingrese su nombre completo.");
    if (!/^\S+@\S+\.\S+$/.test(correo)) return setError("Ingrese un correo válido.");
    if (contrasena.length < 8) return setError("La contraseña debe tener al menos 8 caracteres.");
    if (!/\d/.test(contrasena) || !/[A-Za-z]/.test(contrasena))
      return setError("La contraseña debe incluir letras y números.");
    if (contrasena !== confirmacion) return setError("Las contraseñas no coinciden.");

    setCargando(true);
    try {
      await registro(nombre.trim(), correo.trim(), contrasena);
      setExito("Cuenta creada correctamente. Ya puede iniciar sesión.");
      setTimeout(() => navigate("/login"), 1500);
    } catch (err) {
      setError(err.message);
    } finally {
      setCargando(false);
    }
  }

  return (
    <div className="container mt-5">
      <div className="row justify-content-center">
        <div className="col-md-6">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Crear cuenta de estudiante</h4>
            </div>
            <div className="card-body p-4">
              {error && <div className="alert alert-danger">{error}</div>}
              {exito && <div className="alert alert-success">{exito}</div>}

              <form onSubmit={enviar}>
                <div className="mb-3">
                  <label className="form-label fw-bold">Nombre completo</label>
                  <input className="form-control" value={nombre} onChange={(e) => setNombre(e.target.value)} required />
                </div>
                <div className="mb-3">
                  <label className="form-label fw-bold">Correo institucional</label>
                  <input type="email" className="form-control" value={correo} onChange={(e) => setCorreo(e.target.value)} required />
                </div>
                <div className="mb-3">
                  <label className="form-label fw-bold">Contraseña</label>
                  <input type="password" className="form-control" value={contrasena} onChange={(e) => setContrasena(e.target.value)} required />
                  <div className="form-text">Mínimo 8 caracteres, con letras y números.</div>
                </div>
                <div className="mb-3">
                  <label className="form-label fw-bold">Confirmar contraseña</label>
                  <input type="password" className="form-control" value={confirmacion} onChange={(e) => setConfirmacion(e.target.value)} required />
                </div>
                <button className="btn btn-primary w-100 fw-bold" disabled={cargando}>
                  {cargando ? "Creando cuenta..." : "Registrarse"}
                </button>
              </form>

              <div className="text-center mt-3">
                <Link to="/login">Ya tengo una cuenta</Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
