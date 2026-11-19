# Pruebas de Extracción de Récord - TA-20.4

## Información del entorno
- **Fecha de prueba:** 19/11/2026
- **Responsable:** Katherine Moyolema

## Resultados de las pruebas

### Caso 1: Récord válido (materias y notas correctas)
- **Acción:** Subir un PDF de récord académico válido (universidad interna).
- **Resultado esperado:** El sistema extrae las materias y sus notas correctamente.

### Caso 2: PDF escaneado o ilegible
- **Acción:** Subir un PDF escaneado o con texto no reconocible.
- **Resultado esperado:** El sistema marca el récord como "requiere revisión" y no extrae materias.

### Caso 3: Solicitud sin récord
- **Acción:** Consultar el récord de una solicitud que no tiene documento adjunto.
- **Resultado esperado:** `404 Not Found`.

### Caso 4: Corrección válida
- **Acción:** El estudiante dueño corrige una materia extraída.
- **Resultado esperado:** `200 OK` y la corrección se guarda.

### Caso 5: Corrección rechazada
- **Acción:** Otro usuario (o un estado bloqueado) intenta corregir la materia.
- **Resultado esperado:** `403 Forbidden` o `409 Conflict`.