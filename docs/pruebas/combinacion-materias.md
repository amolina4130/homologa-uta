# Pruebas de Combinación de Materias - TA-22.4

## Información del entorno
- **Fecha de prueba:** 19/11/2026
- **Responsable:** Katherine Moyolema

## Resultados de las pruebas

### Caso 1: Física 1 + Física 2 (Homologable de forma combinada)
- **Acción:** Combinar Física 1 y Física 2 de origen para cubrir Física General de destino.
- **Resultado esperado:** `combinada: true`, `decision: HOMOLOGABLE`.

### Caso 2: Una sola materia alcanza (sin combinación)
- **Acción:** Analizar una sola materia de origen (ej. Cálculo Diferencial) contra Cálculo de destino.
- **Resultado esperado:** `combinada: false`, `decision: HOMOLOGABLE`.

### Caso 3: Ni dos materias juntas alcanzan
- **Acción:** Combinar Física 1 y Física 2 contra una materia de destino más avanzada (ej. Física Avanzada).
- **Resultado esperado:** `decision: NO_HOMOLOGABLE`.

### Caso 4: Rechazo de más de dos materias de origen
- **Acción:** Intentar combinar 3 materias de origen.
- **Resultado esperado:** El sistema rechaza la solicitud con un mensaje claro.

### Caso 5: Módulo externo con combinación justificada
- **Acción:** Combinar materias de una universidad externa.
- **Resultado esperado:** El agente externo devuelve la decisión con su justificación.