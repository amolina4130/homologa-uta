//import React, { useEffect, useState } from 'react';
import { useState, useEffect } from "react";
import { TablaSolicitudes } from '../components/TablaSolicitudes';
import { obtenerMisSolicitudes } from '../services/solicitudesService';

export const MisSolicitudes = () => {
  const [solicitudes, setSolicitudes] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const cargarDatos = async () => {
      try {
        const data = await obtenerMisSolicitudes();
        // Si el backend devuelve { data: [...] }, usa setSolicitudes(data.data)
        setSolicitudes(Array.isArray(data) ? data : data.data || []);
      } catch (err) {
        if (err.tipo === "no_autorizado") {
          setError("Debes iniciar sesión para ver tus solicitudes.");
        } else {
          setError(err.message);
        }
      } finally {
        setCargando(false);
      }
    };
    cargarDatos();
  }, []);

  return (
    <div style={{ padding: '20px' }}>
      <h2>Mis Solicitudes</h2>
      {cargando && <p>Cargando solicitudes...</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {!cargando && !error && <TablaSolicitudes solicitudes={solicitudes} />}
    </div>
  );
};