# Pruebas del Informe PDF - TA-18.4

## Información del entorno
- **Fecha de prueba:** 19/11/2026
- **Responsable:** Katherine Moyolema

## Resultados de las pruebas

### Caso 1: Descarga correcta por el estudiante dueño
- **Acción:** Estudiante dueño solicita el informe PDF.
- **Resultado esperado:** `200 OK` con el archivo PDF generado.

### Caso 2: Rechazo (403) a otro estudiante
- **Acción:** Un estudiante distinto intenta descargar el informe de otro.
- **Resultado esperado:** `403 Forbidden`.

### Caso 3: Descarga correcta por el Coordinador
- **Acción:** Coordinador solicita el informe PDF.
- **Resultado esperado:** `200 OK` con el archivo PDF.

### Caso 4: Solicitud sin análisis
- **Acción:** Intentar descargar el informe de una solicitud que aún no tiene análisis.
- **Resultado esperado:** `400 Bad Request` con un mensaje de error.