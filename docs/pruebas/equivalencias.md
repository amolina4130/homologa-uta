# Pruebas de Equivalencias - TA-14.5

## Información del entorno
- **Fecha de prueba:** 14/11/2026
- **Responsable:** Katherine Moyolema

## Resultados de las pruebas

### Caso 1: Creación y duplicado
- **Acción:** POST /equivalencias con OrigenId: 1, DestinoId: 2.
- **Resultado esperado:** 200 OK (primera vez), 409 Conflict (segunda vez).

### Caso 2: Acceso con otro rol (403)
- **Acción:** POST /equivalencias con token de Estudiante.
- **Resultado esperado:** 403 Forbidden.

### Caso 3: Análisis con equivalencia activa
- **Acción:** Analizar asignatura con equivalencia activa.
- **Resultado esperado:** La equivalencia se aplica correctamente.

### Caso 4: Análisis con equivalencia desactivada
- **Acción:** Analizar asignatura con equivalencia desactivada.
- **Resultado esperado:** La equivalencia no se aplica.