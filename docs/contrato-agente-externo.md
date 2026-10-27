# Contrato del Conector — Agente Externo (HU-09)

## 1. Objetivo
Definir la estructura de datos que el sistema enviará al agente externo autónomo (para universidades externas) y la estructura de respuesta que este debe devolver, con el fin de garantizar una comunicación estandarizada y verificable.

## 2. Configuración del Agente Externo
- **Tipo de Agente:** Autónomo (sin reglas fijas impuestas por el código).
- **Entorno de Ejecución:** Servicio externo (se simulará mediante una API REST o un modelo de lenguaje externo).
- **Método de Comunicación:** HTTP REST (JSON).
- **Endpoint sugerido:** `POST /api/external-agent/analyze` (o el que defina el equipo de backend en el Sprint 2).

## 3. Datos que se envían (Request)
El sistema (servicio de Homologación) enviará un JSON con la siguiente estructura:

```json
{
  "solicitudId": 101,
  "estudiante": "estudiante@uta.edu.ec",
  "carreraOrigen": "Ingeniería en Software (Universidad Externa)",
  "carreraDestino": "Ingeniería en Software (UTA)",
  "documentos": {
    "mallaOrigen": "texto extraído de la malla...",
    "historialAcademico": "texto extraído del historial...",
    "silabos": [
      {
        "asignatura": "Programación Orientada a Objetos",
        "contenido": "texto extraído del sílabo..."
      }
    ]
  }
}