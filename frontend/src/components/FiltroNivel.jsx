import React from 'react';

const NIVELES = [
  "Todos",
  "Primero",
  "Segundo",
  "Tercero",
  "Cuarto",
  "Quinto",
  "Sexto",
  "Séptimo",
  "Octavo",
  "Noveno"
];

export default function FiltroNivel({ nivelSeleccionado, onChange }) {
  return (
    <div style={{ marginBottom: "10px" }}>
      <label htmlFor="filtro-nivel" style={{ fontWeight: "bold", marginRight: "8px" }}>
        Filtrar por nivel:
      </label>
      <select
        id="filtro-nivel"
        value={nivelSeleccionado}
        onChange={(e) => onChange(e.target.value)}
        style={{ padding: "5px", border: "1px solid #ccc", borderRadius: "4px" }}
      >
        {NIVELES.map((nivel) => (
          <option key={nivel} value={nivel}>
            {nivel}
          </option>
        ))}
      </select>
    </div>
  );
}