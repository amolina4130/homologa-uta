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
```
## 4. Respuesta Esperada (Response)
El agente externo debe devolver un JSON con la decisión por asignatura y su justificación verificable:

```json
{
  "solicitudId": 101,
  "analisisCompletado": true,
  "asignaturas": [
    {
      "nombreOrigen": "Programación Orientada a Objetos",
      "nombreDestino": "Programación Orientada a Objetos",
      "decision": "HOMOLOGABLE",
      "justificacion": "El contenido, la profundidad y la carga horaria son equivalentes. El agente verificó que los temas de herencia y polimorfismo coinciden en un 90%.",
      "porcentajeSimilitud": 90
    },
    {
      "nombreOrigen": "Base de Datos I",
      "nombreDestino": "Base de Datos",
      "decision": "NO_HOMOLOGABLE",
      "justificacion": "La carga horaria de la universidad de origen es significativamente menor y no cubre los temas de transacciones y optimización requeridos por la UTA.",
      "porcentajeSimilitud": 60
    }
  ],
  "observacionesGenerales": "El estudiante tiene un perfil técnico adecuado, pero requiere tutoría en el área de bases de datos."
}
```
## 5. Reglas de Validación del Contrato
- El agente externo **no recibe reglas normativas** de la UTA (a diferencia del agente interno). Él decide basándose en su propio razonamiento y en la documentación.
- La respuesta **debe incluir siempre la justificación** de la decisión para que sea auditable.
- Si el agente falla o excede el tiempo de espera, el sistema mostrará el error y permitirá reintentar sin perder los datos.

## 6. Validación con el Docente
*(Este apartado se llenará cuando el equipo presente el contrato al Ing. Hernán Naranjo para su aprobación antes de implementar el conector).*