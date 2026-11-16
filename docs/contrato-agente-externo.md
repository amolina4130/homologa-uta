# Contrato del Conector — Agente Externo (HU-09, ampliado en HU-22)

## 1. Objetivo
Definir la estructura de datos que el sistema enviará al agente externo autónomo (para universidades externas) y la estructura de respuesta que este debe devolver, con el fin de garantizar una comunicación estandarizada y verificable.

## 2. Configuración del Agente Externo
- **Tipo de Agente:** Autónomo (sin reglas fijas impuestas por el código).
- **Entorno de Ejecución:** Servicio externo (se simulará mediante una API REST o un modelo de lenguaje externo).
- **Método de Comunicación:** HTTP REST (JSON).
- **Endpoint sugerido:** `POST /api/external-agent/analyze` (o el que defina el equipo de backend en el Sprint 2).
- **Reintentos:** configurables con `AgenteExterno:Reintentos` (por defecto 2).

## 3. Datos que se envían (Request)
El sistema (servicio de Homologación) enviará un JSON con la siguiente estructura:

```json
{
  "solicitudId": 101,
  "estudiante": "estudiante@uta.edu.ec",
  "carreraOrigen": "Ingeniería en Software (Universidad Externa)",
  "carreraDestino": "Ingeniería en Software (UTA)",
  "materiasOrigen": [
    "Programación Orientada a Objetos",
    "Estructura de Datos",
    "Bases de Datos"
  ],
  "materiasDestino": [
    {
      "asignaturaId": 8,
      "nombre": "Programación Orientada a Objetos",
      "area": "Programación",
      "nivel": "Segundo",
      "contenido": "texto del plan analítico de la asignatura de destino...",
      "materiasOrigenPermitidas": [
        "Programación Orientada a Objetos",
        "Estructura de Datos"
      ],
      "notasMateriasOrigen": {
        "Programación Orientada a Objetos": 17.5,
        "Estructura de Datos": 16.0
      }
    }
  ],
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

- **`materiasOrigen`**: materias aprobadas del estudiante que la preselección (HU-21) considera candidatas.
- **`materiasDestino`**: asignaturas de la carrera de destino que resultaron candidatas por área y nivel.
  Cada una incluye las materias aprobadas que pueden cubrirla (`materiasOrigenPermitidas`) y sus notas.
- Si la preselección no está disponible (por ejemplo, memoria del agente sin cargar), el servicio mantiene el
  comportamiento anterior: envía los sílabos de la solicitud y una sola materia de origen por destino.

## 4. Respuesta Esperada (Response)
El agente externo debe devolver un JSON con la decisión por asignatura y su justificación verificable:

```json
{
  "solicitudId": 101,
  "analisisCompletado": true,
  "asignaturas": [
    {
      "asignaturaDestinoId": 8,
      "nombreDestino": "Programación Orientada a Objetos",
      "materiasOrigen": [
        "Programación Orientada a Objetos",
        "Estructura de Datos"
      ],
      "decision": "HOMOLOGABLE",
      "justificacion": "Entre las dos materias de origen se cubren los temas de herencia, polimorfismo y estructuras lineales que requiere la asignatura de destino.",
      "porcentajeSimilitud": 95
    },
    {
      "nombreDestino": "Base de Datos",
      "materiasOrigen": [
        "Bases de Datos"
      ],
      "decision": "NO_HOMOLOGABLE",
      "justificacion": "La carga horaria de la universidad de origen es significativamente menor y no cubre los temas de transacciones y optimización requeridos por la UTA.",
      "porcentajeSimilitud": 60
    }
  ],
  "observacionesGenerales": "El estudiante tiene un perfil técnico adecuado, pero requiere tutoría en el área de bases de datos."
}
```

- **`asignaturaDestinoId`** es opcional pero recomendado: permite asociar la respuesta con la preselección.
- **`materiasOrigen`** contiene una o dos materias de origen que, en conjunto, cubren la asignatura de destino.
  Se mantiene `nombreOrigen` por retrocompatibilidad; si no llega `materiasOrigen`, se usa `nombreOrigen`.

## 5. Reglas de Validación del Contrato
- El agente externo **no recibe reglas normativas** de la UTA (a diferencia del agente interno). Él decide basándose en su propio razonamiento y en la documentación.
- La respuesta **debe incluir siempre la justificación** de la decisión para que sea auditable.
- **Combinación (HU-22):** una asignatura de destino puede cubrirse con **una o dos** materias de origen.
  - Si el agente propone más de dos, o materias que no están en `materiasOrigenPermitidas` (o en
    `materiasOrigen` cuando el destino no trae la lista), la respuesta se considera inválida.
  - Una respuesta inválida se **reintenta** con Polly hasta agotar `AgenteExterno:Reintentos`.
  - Agotados los reintentos, esas asignaturas quedan en **`REQUIERE_REVISION`** con una justificación
    que indica que el agente no entregó una combinación válida, sin perder el resto del análisis.
- Si el agente falla (error HTTP) o excede el tiempo de espera, el sistema muestra el error y permite
  reintentar sin perder los datos: no se guarda un análisis parcial.
- Las decisiones del agente (`HOMOLOGABLE`, `NO_HOMOLOGABLE`, otra) se guardan tal cual, sin imponer
  umbrales de porcentaje.

## 6. Validación con el Docente
*(Este apartado se llenará cuando el equipo presente el contrato al Ing. Hernán Naranjo para su aprobación antes de implementar el conector).*
