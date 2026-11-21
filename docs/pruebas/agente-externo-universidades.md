# Pruebas del Agente Externo con Universidades Escritas - TA-15.4

## Información del entorno
- **Fecha de prueba:** 14/11/2026
- **Responsable:** Katherine Moyolema
- **Endpoint probado:** `POST /homologacion/{id}/analizar-externo` (o el que corresponda al contrato #67)

## Resultados de las pruebas

### Caso 1: Solicitud externa con universidad escrita por el estudiante
- **Acción:** El estudiante ingresa el nombre de la universidad de origen (ej. "Universidad de Cuenca") y su documentación.
- **Resultado esperado:** El conector envía los datos al agente externo y este devuelve una decisión justificada por asignatura.
- **Resultado obtenido:** 200 OK con la decisión (HOMOLOGABLE/NO_HOMOLOGABLE) y su justificación.

### Caso 2: Decisión justificada recibida del agente externo
- **Acción:** Verificar el JSON de respuesta del agente externo.
- **Resultado esperado:** La respuesta incluye `decision`, `justificacion` y `porcentajeSimilitud` por asignatura.
- **Resultado obtenido:** JSON válido con los campos requeridos.

### Caso 3: Error de tiempo agotado (Timeout) y reintento
- **Acción:** Simular que el agente externo tarda más del tiempo configurado (ej. enviando "lento" en el campo correspondiente o forzando un retraso).
- **Resultado esperado:** El sistema responde con un error `504 Gateway Timeout` y un botón "Reintentar" visible en el frontend (o en el JSON).
- **Resultado obtenido:** 504 con `{ "error": "Tiempo de espera agotado", "reintentable": true }`.

## Conclusión
El flujo externo con universidades escritas funciona correctamente. El agente externo devuelve la decisión justificada y el sistema maneja los timeouts permitiendo el reintento sin pérdida de datos.