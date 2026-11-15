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
  "modulo": "EXTERNO",
  "universidadOrigen": "Universidad de Cuenca",
  "universidadDestino": "Universidad Técnica de Ambato",
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

`modulo`, `universidadOrigen` y `universidadDestino` se agregan en el Sprint 3 (HU-15) para el
módulo externo. En el módulo interno las universidades van vacías.

## 4. Respuesta Esperada (Response)
El agente externo debe devolver un JSON con la decisión por asignatura y su justificación verificable.
Desde el Sprint 3 (HU-22) una materia de destino puede estar cubierta por **una o dos materias de
origen**; en ese caso se envían en `materiasOrigen` y la respuesta incluye los tres porcentajes
calculados sobre el conjunto:

```json
{
  "solicitudId": 101,
  "analisisCompletado": true,
  "asignaturas": [
    {
      "nombreOrigen": "Física 1 + Física 2",
      "nombreDestino": "Física General",
      "materiasOrigen": [
        { "nombre": "Física 1", "nota": 18 },
        { "nombre": "Física 2", "nota": 17 }
      ],
      "decision": "HOMOLOGABLE",
      "porcentajeContenido": 88,
      "porcentajeProfundidad": 85,
      "porcentajeCargaHoraria": 92,
      "porcentajeSimilitud": 88,
      "justificacion": "Física 1 y Física 2 cubren en conjunto la mecánica, el electromagnetismo y la termodinámica de Física General; la carga horaria sumada alcanza la de destino."
    },
    {
      "nombreOrigen": "Programación Orientada a Objetos",
      "nombreDestino": "Programación Orientada a Objetos",
      "materiasOrigen": [
        { "nombre": "Programación Orientada a Objetos", "nota": 19 }
      ],
      "decision": "HOMOLOGABLE",
      "porcentajeContenido": 90,
      "porcentajeProfundidad": 90,
      "porcentajeCargaHoraria": 95,
      "porcentajeSimilitud": 90,
      "justificacion": "El contenido, la profundidad y la carga horaria son equivalentes. El agente verificó que los temas de herencia y polimorfismo coinciden en un 90%."
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

### Campos de la respuesta
- `materiasOrigen` (opcional, máximo dos): materias que respaldan la decisión. Si no se envía, el
  backend usa `nombreOrigen` como única materia de origen.
- `porcentajeContenido`, `porcentajeProfundidad`, `porcentajeCargaHoraria` (opcionales): porcentajes
  calculados sobre el conjunto de materias de origen. Si faltan, se usa `porcentajeSimilitud` como
  contenido y cero para profundidad y carga horaria.
- `decision` y `justificacion`: obligatorias en la práctica; si faltan, la asignatura queda en
  `REQUIERE_REVISION` para revisión manual.

## 5. Reglas de Validación del Contrato
- El agente externo **no recibe reglas normativas** de la UTA (a diferencia del agente interno). Él decide basándose en su propio razonamiento y en la documentación.
- La respuesta **debe incluir siempre la justificación** de la decisión para que sea auditable.
- `materiasOrigen` acepta una o dos entradas. Si llegan más de dos, la asignatura se rechaza y queda
  en `REQUIERE_REVISION` con su justificación; no se guarda como combinación válida.
- Los resultados sin combinación se siguen devolviendo igual, con una sola materia de origen.
- Si el agente falla o excede el tiempo de espera, el sistema mostrará el error y permitirá reintentar sin perder los datos.

## 6. Validación con el Docente
*(Este apartado se llenará cuando el equipo presente el contrato al Ing. Hernán Naranjo para su aprobación antes de implementar el conector).*