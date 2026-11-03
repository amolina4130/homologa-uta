import React, { useEffect, useState } from "react";
import { useAuth } from "../context/AuthContext";

export default function Inicio() {
  const { rol } = useAuth();
  const [usuario, setUsuario] = useState({ nombre: "", correo: "", rol: "" });

  useEffect(() => {
    const userData = localStorage.getItem("usuario");
    if (userData) {
      setUsuario(JSON.parse(userData));
    } else {
      setUsuario((prev) => ({ ...prev, rol: rol || "No registrado" }));
    }
  }, [rol]);

  return (
    <div className="container mt-4">
      <div className="card shadow-sm p-4 border-0">
        <h2 className="mb-4">¡Bienvenido!</h2>
        
        <div className="mb-3">
          <label className="fw-bold">Nombre:</label>
          <p>{usuario.nombre || "No registrado"}</p>
        </div>

        <div className="mb-3">
          <label className="fw-bold">Correo:</label>
          <p>{usuario.correo || "No registrado"}</p>
        </div>

        <div className="mb-3">
          <label className="fw-bold">Rol:</label>
          <p>{usuario.rol || "No registrado"}</p>
        </div>
      </div>
    </div>
  );
}