# Pruebas E2E — Sprint 2

## Datos de la prueba
- **Tarea:** TE-03
- **Fecha:** 04/11/2026
- **Responsable:** Karol Lucero
- **Entorno:** Docker local + Navegador (Chrome)

## Caso 1: Login desde la interfaz
- **Acción:** Iniciar sesión como estudiante.
- **Resultado esperado:** Ingreso exitoso y redirección al menú.
- **Resultado obtenido:** **Exitoso**.
- **Evidencia:** Captura del menú de estudiante con el rol ESTUDIANTE.

## Caso 2: Registrar Solicitud
- **Acción:** Ir a "Nueva solicitud", seleccionar carreras y asignaturas, y registrar.
- **Resultado esperado:** La solicitud se guarda correctamente.
- **Resultado obtenido:** **Bloqueado**.
- **Observación:** Los desplegables de carreras están vacíos. El endpoint `GET /carreras` devuelve `200 OK` pero con un array vacío (`[]`). Esto indica que la base de datos no tiene las carreras precargadas (el `SeedData` no está insertando los datos desde el JSON).

## Caso 3: Resultado del Análisis (Agente)
- **Resultado obtenido:** **No probado** (bloqueado por el Caso 2).

## Caso 4: Panel del Coordinador
- **Resultado obtenido:** **No probado** (bloqueado por el Caso 2).

## Conclusión
El flujo E2E se completó parcialmente. El login desde la interfaz funciona correctamente y el frontend está estable. Los pasos de registro y análisis quedaron bloqueados por un problema en la carga de datos semilla del servicio de Solicitudes. Se recomienda revisar el `SeedData` y asegurar que las carreras se inserten correctamente en la base de datos.