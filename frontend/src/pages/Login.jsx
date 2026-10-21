import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth, rutaPorRol } from "../context/AuthContext";

export default function Login() {
  const navigate = useNavigate();
  const { iniciarSesion } = useAuth();
  const [correo, setCorreo] = useState("");
  const [contrasena, setContrasena] = useState("");
  const [errores, setErrores] = useState({});
  const [errorLogin, setErrorLogin] = useState("");
  const [cargando, setCargando] = useState(false);

  function validar() {
    const e = {};
    if (!correo.trim()) e.correo = "Ingrese su correo institucional.";
    else if (!/^\S+@\S+\.\S+$/.test(correo)) e.correo = "Ingrese un correo válido.";
    if (!contrasena) e.contrasena = "Ingrese su contraseña.";
    return e;
  }

  async function ingresar(ev) {
    ev.preventDefault();
    setErrorLogin("");
    const e = validar();
    setErrores(e);
    if (Object.keys(e).length > 0) return;

    setCargando(true);
    try {
      const rol = await iniciarSesion(correo, contrasena);
      navigate(rutaPorRol(rol));
    } catch (err) {
      setErrorLogin(
        err.tipo === "credenciales"
          ? "Credenciales incorrectas. Intente nuevamente."
          : "No se pudo conectar con el servidor. Intente más tarde."
      );
    } finally {
      setCargando(false);
    }
  }

  return (
    <div className="container d-flex justify-content-center align-items-center vh-100 bg-light">
      <div className="card shadow-lg p-4" style={{ maxWidth: "450px", width: "100%" }}>
        
        <div className="text-center mb-4">
          {/* Aquí puedes poner el logo de la UTA con una etiqueta img si lo tienes */}
          <h2 className="fw-bold text-primary">HomologaUTA</h2>
          <p className="text-muted">Sistema de Homologación - UTA</p>
        </div>

        <form onSubmit={ingresar} noValidate>
          <h4 className="mb-4 text-center">Iniciar Sesión</h4>

          <div className="mb-3">
            <label htmlFor="correo" className="form-label fw-bold">Correo institucional</label>
            <input 
              id="correo" 
              type="email" 
              className={`form-control ${errores.correo ? 'is-invalid' : ''}`}
              value={correo}
              placeholder="estudiante@uta.edu.ec"
              onChange={(e) => setCorreo(e.target.value)} 
            />
            {errores.correo && <div className="invalid-feedback">{errores.correo}</div>}
          </div>

          <div className="mb-4">
            <label htmlFor="contrasena" className="form-label fw-bold">Contraseña</label>
            <input 
              id="contrasena" 
              type="password" 
              className={`form-control ${errores.contrasena ? 'is-invalid' : ''}`}
              value={contrasena}
              onChange={(e) => setContrasena(e.target.value)} 
            />
            {errores.contrasena && <div className="invalid-feedback">{errores.contrasena}</div>}
          </div>

          {errorLogin && (
            <div className="alert alert-danger py-2" role="alert">
              {errorLogin}
            </div>
          )}

          <button type="submit" className="btn btn-primary w-100 py-2 fw-bold" disabled={cargando}>
            {cargando ? "Ingresando..." : "Ingresar"}
          </button>
        </form>

        <p className="text-center text-muted mt-3 mb-0" style={{ fontSize: "0.85rem" }}>
          Al ingresar, el sistema lo redirige a la pantalla según su rol.
        </p>
      </div>
    </div>
  );
}