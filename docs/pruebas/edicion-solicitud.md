# Pruebas de Edición de Solicitud - TA-16.3

## Información del entorno
- **Fecha de prueba:** 15/11/2026
- **Responsable:** Katherine Moyolema

## Resultados de las pruebas

### Caso 1: Número de proceso único
- **Acción:** Crear una solicitud y verificar el campo `numeroProceso`.
- **Resultado esperado:** El sistema genera un número único con formato `SOL-XXXX`.

### Caso 2: Filtros
- **Acción:** Consultar solicitudes aplicando filtros por estado y fecha.
- **Resultado esperado:** Solo se muestran las solicitudes que coinciden con el filtro.

### Caso 3: Edición válida
- **Acción:** Editar una solicitud en estado `REGISTRADA`.
- **Resultado esperado:** La edición se guarda correctamente (200 OK).

### Caso 4: Edición rechazada (solicitud en revisión)
- **Acción:** Intentar editar una solicitud en estado `PENDIENTE_REVISION`.
- **Resultado esperado:** El sistema rechaza la edición con un mensaje claro (400 Bad Request).