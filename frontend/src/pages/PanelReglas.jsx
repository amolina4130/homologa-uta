import { useState, useEffect } from "react";
import { useAuth } from "../context/AuthContext";
import { obtenerReglas, actualizarRegla } from "../services/reglasService";

export default function PanelReglas() {
  const { rol } = useAuth();
  const [reglas, setReglas] = useState([]);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState("");
  const [mensajeExito, setMensajeExito] = useState("");

  useEffect(() => {
    obtenerReglas()
      .then((data) => setReglas(data.data || data))
      .catch(() => setError("No se pudieron cargar las reglas."));
  }, []);

  const handleCambio = (id, campo, valor) => {
    setReglas((prev) =>
      prev.map((regla) =>
        regla.id === id ? { ...regla, [campo]: valor } : regla
      )
    );
  };

  const guardarCambios = async () => {
    setCargando(true);
    setError("");
    setMensajeExito("");
    try {
      for (const regla of reglas) {
        await actualizarRegla(regla.id, {
          umbral: regla.umbral,
          activa: regla.activa,
        });
      }
      setMensajeExito("Cambios guardados correctamente. Se aplicarán en el siguiente análisis.");
    } catch {
      setError("No se pudieron guardar los cambios. Verifique la conexión.");
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className="container mt-4" style={{ maxWidth: 1000 }}>
      <h2 className="mb-4">PANEL DE REGLAS NORMATIVAS</h2>

      <div className="card p-4 mb-4 shadow-sm">
        <h3 className="mb-3">Catálogo de reglas (RN-01 a RN-11)</h3>

        <table className="table table-bordered table-hover align-middle">
          <thead className="table-secondary">
            <tr>
              <th>Regla</th>
              <th>Descripción</th>
              <th>Fuente</th>
              <th>Umbral</th>
              <th>Activa</th>
            </tr>
          </thead>
          <tbody>
            {reglas.map((regla) => (
              <tr key={regla.id}>
                <td className="fw-bold">{regla.codigo}</td>
                <td>{regla.descripcion}</td>
                <td>{regla.fuente}</td>
                <td>
                  <input
                    type="text"
                    className="form-control form-control-sm"
                    value={regla.umbral || "—"}
                    onChange={(e) => handleCambio(regla.id, "umbral", e.target.value)}
                    disabled={rol !== "ADMINISTRADOR"}
                    style={{ width: "100px" }}
                  />
                </td>
                <td className="text-center">
                  <input
                    type="checkbox"
                    className="form-check-input"
                    checked={regla.activa}
                    onChange={(e) => handleCambio(regla.id, "activa", e.target.checked)}
                    disabled={rol !== "ADMINISTRADOR"}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {error && <div className="alert alert-danger">{error}</div>}
        {mensajeExito && <div className="alert alert-success">{mensajeExito}</div>}

        {rol === "ADMINISTRADOR" && (
          <div className="mt-3">
            <button 
              className="btn btn-primary" 
              onClick={guardarCambios} 
              disabled={cargando}
            >
              {cargando ? "Guardando..." : "Guardar cambios"}
            </button>
            <button className="btn btn-outline-secondary ms-2">Cancelar</button>
          </div>
        )}

        <div className="mt-4 text-muted small">
          <p className="mb-1">Los cambios se aplican en el siguiente análisis, sin recompilar el sistema.</p>
          <p className="mb-0">Solo el Administrador puede modificar las reglas; con otro rol el Gateway responde 403.</p>
        </div>
      </div>
    </div>
  );
}