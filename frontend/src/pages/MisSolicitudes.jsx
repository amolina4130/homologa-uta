//import React, { useEffect, useState } from 'react';
import { useState, useEffect } from "react";
import { TablaSolicitudes } from '../components/TablaSolicitudes';
import { obtenerMisSolicitudes } from '../services/solicitudesService';

const MisSolicitudes = () => {
  const [solicitudes, setSolicitudes] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const cargarDatos = async () => {
      try {
        const data = await obtenerMisSolicitudes();
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
    <div className="container mt-4">
      <div className="row justify-content-center">
        <div className="col-md-10">
          <div className="card shadow-sm border-0">
            <div className="card-header bg-primary text-white">
              <h4 className="mb-0 fw-bold">Mis Solicitudes</h4>
            </div>
            <div className="card-body p-4">
              {cargando && (
                <div className="d-flex justify-content-center my-5">
                  <div className="spinner-border text-primary" role="status">
                    <span className="visually-hidden">Cargando...</span>
                  </div>
                </div>
              )}

              {error && (
                <div className="alert alert-danger shadow-sm" role="alert">
                  {error}
                </div>
              )}

              {!cargando && !error && (
                <TablaSolicitudes solicitudes={solicitudes} />
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default MisSolicitudes;