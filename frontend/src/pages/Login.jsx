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

  const campo = { width: "100%", padding: 10, boxSizing: "border-box", border: "2px solid #000" };
  const error = { color: "#b00020", fontSize: 14, margin: "4px 0 0" };

  return (
    <div style={{ maxWidth: 900, margin: "40px auto", fontFamily: "sans-serif", border: "2px solid #000" }}>
      <div style={{ borderBottom: "2px solid #000", padding: "8px 16px", fontWeight: "bold" }}>INICIO DE SESIÓN</div>
      <h1 style={{ textAlign: "center" }}>SISTEMA DE HOMOLOGACIÓN - UTA</h1>

      <div style={{ display: "flex", gap: 48, padding: 32, alignItems: "flex-start" }}>
        <div style={{ width: 200, height: 200, border: "2px solid #000", display: "flex", alignItems: "center", justifyContent: "center" }}>
          Logo UTA
        </div>

        <form onSubmit={ingresar} noValidate style={{ flex: 1 }}>
          <h2>Iniciar sesión</h2>

          <label htmlFor="correo">Correo institucional</label>
          <input id="correo" type="email" style={campo} value={correo}
                 placeholder="estudiante@uta.edu.ec"
                 onChange={(e) => setCorreo(e.target.value)} />
          {errores.correo && <p style={error}>{errores.correo}</p>}

          <div style={{ marginTop: 16 }}>
            <label htmlFor="contrasena">Contraseña</label>
            <input id="contrasena" type="password" style={campo} value={contrasena}
                   onChange={(e) => setContrasena(e.target.value)} />
            {errores.contrasena && <p style={error}>{errores.contrasena}</p>}
          </div>

          {errorLogin && <p style={error}>{errorLogin}</p>}

          <button type="submit" disabled={cargando} style={{ marginTop: 16, padding: "10px 32px" }}>
            {cargando ? "Ingresando..." : "Ingresar"}
          </button>

          <p style={{ marginTop: 24 }}>Al ingresar, el sistema lo redirige a la pantalla según su rol.</p>
        </form>
      </div>
    </div>
  );
}