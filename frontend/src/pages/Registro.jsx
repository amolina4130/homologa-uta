import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { registrar } from "../services/authService";

const LARGO_MINIMO = 8;

function validarCampos(nombre, correo, contrasena, confirmacion) {
  const errores = {};

  if (!nombre.trim()) errores.nombre = "Ingrese su nombre completo.";
  else if (nombre.trim().length > 200)
    errores.nombre = "El nombre no puede superar los 200 caracteres.";

  if (!correo.trim()) errores.correo = "Ingrese su correo institucional.";
  else if (!/^\S+@\S+\.\S+$/.test(correo.trim()))
    errores.correo = "Ingrese un correo válido.";

  if (!contrasena) errores.contrasena = "Ingrese una contraseña.";
  else if (contrasena.length < LARGO_MINIMO)
    errores.contrasena = `La contraseña debe tener al menos ${LARGO_MINIMO} caracteres.`;
  else if (!/[A-Z]/.test(contrasena))
    errores.contrasena = "La contraseña debe incluir al menos una letra mayúscula.";
  else if (!/[a-z]/.test(contrasena))
    errores.contrasena = "La contraseña debe incluir al menos una letra minúscula.";
  else if (!/[0-9]/.test(contrasena))
    errores.contrasena = "La contraseña debe incluir al menos un número.";

  // La confirmación se valida solo en la pantalla.
  if (!confirmacion) errores.confirmacion = "Repita la contraseña.";
  else if (contrasena !== confirmacion)
    errores.confirmacion = "Las contraseñas no coinciden.";

  return errores;
}

export default function Registro() {
  const navigate = useNavigate();
  const [nombre, setNombre] = useState("");
  const [correo, setCorreo] = useState("");
  const [contrasena, setContrasena] = useState("");
  const [confirmacion, setConfirmacion] = useState("");
  const [errores, setErrores] = useState({});
  const [errorServidor, setErrorServidor] = useState("");
  const [cargando, setCargando] = useState(false);

  async function crearCuenta(ev) {
    ev.preventDefault();
    setErrorServidor("");

    const encontrados = validarCampos(nombre, correo, contrasena, confirmacion);
    setErrores(encontrados);
    if (Object.keys(encontrados).length > 0) return;

    setCargando(true);
    try {
      await registrar(nombre.trim(), correo.trim(), contrasena);
      navigate("/login", {
        state: {
          mensajeExito:
            "Cuenta creada correctamente. Ya puede iniciar sesión con su correo.",
        },
      });
    } catch (err) {
      setErrorServidor(
        err.tipo === "duplicado"
          ? err.message
          : err.message || "No se pudo crear la cuenta. Intente nuevamente."
      );
    } finally {
      setCargando(false);
    }
  }

  return (
    <div className="container d-flex justify-content-center align-items-center min-vh-100 bg-light py-4">
      <div className="card shadow-lg p-4" style={{ maxWidth: "520px", width: "100%" }}>
        <div className="text-center mb-4">
          <h2 className="fw-bold text-primary">HomologaUTA</h2>
          <p className="text-muted mb-0">Sistema de Homologación - UTA</p>
        </div>

        <form onSubmit={crearCuenta} noValidate>
          <h4 className="mb-4 text-center">Crear una cuenta</h4>

          <div className="mb-3">
            <label htmlFor="nombre" className="form-label fw-bold">
              Nombre completo
            </label>
            <input
              id="nombre"
              type="text"
              className={`form-control ${errores.nombre ? "is-invalid" : ""}`}
              value={nombre}
              placeholder="Nombres y apellidos"
              onChange={(e) => setNombre(e.target.value)}
            />
            {errores.nombre && <div className="invalid-feedback">{errores.nombre}</div>}
          </div>

          <div className="mb-3">
            <label htmlFor="correo" className="form-label fw-bold">
              Correo institucional
            </label>
            <input
              id="correo"
              type="email"
              className={`form-control ${errores.correo ? "is-invalid" : ""}`}
              value={correo}
              placeholder="estudiante@uta.edu.ec"
              onChange={(e) => setCorreo(e.target.value)}
            />
            {errores.correo && <div className="invalid-feedback">{errores.correo}</div>}
          </div>

          <div className="mb-3">
            <label htmlFor="contrasena" className="form-label fw-bold">
              Contraseña
            </label>
            <input
              id="contrasena"
              type="password"
              className={`form-control ${errores.contrasena ? "is-invalid" : ""}`}
              value={contrasena}
              onChange={(e) => setContrasena(e.target.value)}
            />
            {errores.contrasena ? (
              <div className="invalid-feedback">{errores.contrasena}</div>
            ) : (
              <div className="form-text">
                Mínimo {LARGO_MINIMO} caracteres, una mayúscula, una minúscula y un número.
              </div>
            )}
          </div>

          <div className="mb-4">
            <label htmlFor="confirmacion" className="form-label fw-bold">
              Confirmar contraseña
            </label>
            <input
              id="confirmacion"
              type="password"
              className={`form-control ${errores.confirmacion ? "is-invalid" : ""}`}
              value={confirmacion}
              onChange={(e) => setConfirmacion(e.target.value)}
            />
            {errores.confirmacion && (
              <div className="invalid-feedback">{errores.confirmacion}</div>
            )}
          </div>

          {errorServidor && (
            <div className="alert alert-danger py-2" role="alert">
              {errorServidor}
            </div>
          )}

          <button
            type="submit"
            className="btn btn-primary w-100 py-2 fw-bold"
            disabled={cargando}
          >
            {cargando ? "Creando cuenta..." : "Registrarme"}
          </button>
        </form>

        <p className="text-center text-muted mt-3 mb-0" style={{ fontSize: "0.9rem" }}>
          ¿Ya tiene una cuenta?{" "}
          <Link to="/login" className="fw-bold">
            Inicie sesión
          </Link>
        </p>
      </div>
    </div>
  );
}
