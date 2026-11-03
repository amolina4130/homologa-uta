# Pruebas del Agente Externo - TA-09.4

## Información del entorno
- **Fecha de prueba:** 03/11/2026
- **Responsable:** Katherine Moyolema
- **Endpoint probado:** `POST /homologacion/{id}/analizar-externo`

## Resultados de las pruebas

### 1. Envío de documentos y recepción de decisión justificada
- **Documento enviado:** Malla de la Universidad Externa (simulada)
- **Decisión recibida:** HOMOLOGABLE
- **Justificación recibida:** "Los contenidos, la profundidad y la carga horaria son equivalentes."
- **Evidencia visual:**
  ![Captura Decisión](capturas/externo-decision.png)

### 2. Tiempo de espera agotado (Timeout)
- **Escenario:** Estudiante "lento@externa.edu.ec" (simula una demora de 35s).
- **Respuesta del sistema:** `504 Gateway Timeout` a los 30.01 segundos, con `"reintentable": true`.
- **Evidencia visual:**
  ![Captura Timeout](capturas/externo-timeout.png)

### 3. Reintento sin perder datos
- **Escenario:** Se reintentó la petición después del timeout con un estudiante normal.
- **Resultado:** La solicitud se procesó correctamente (200 OK) y la decisión se guardó sin duplicar el registro.
- **Evidencia visual:**
  ![Captura Reintento](capturas/externo-reintento.png)

## Conclusión
El conector del agente externo maneja correctamente los timeouts y permite reintentar sin pérdida de datos, cumpliendo con los criterios de aceptación del Issue #71.