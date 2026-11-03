# Pruebas del Flujo Completo — HomologaUTA

## Datos del Sprint
- **Sprint:** 2
- **Fecha de ejecución:** 03/11/2026
- **Responsable:** Karol Lucero
- **Entorno:** Docker local y Postman

## Escenario de prueba
Flujo completo: el estudiante registra una solicitud, el agente interno propone una decisión, y el Coordinador revisa y emite el informe técnico.

## Resultados por paso

### 1. El estudiante registra la solicitud
- **Acción:** Iniciar sesión como estudiante en Postman.
- **Resultado esperado:** Obtener el token JWT.
- **Resultado obtenido:** **Exitoso**.
- **Observaciones:** Se corrigieron dos errores en el backend: el error 4060 (base de datos no encontrada) agregando `EnsureCreated()` en el `Program.cs`, y el error de contraseña nula agregando un valor por defecto en el `SeedData.cs`. El login devuelve el token correctamente.

### 2. El agente propone con su justificación
- **Acción:** Consultar el resultado del análisis de la solicitud.
- **Resultado obtenido:** **Pendiente de integración con Ollama** (el agente no está completamente desplegado en el entorno local).

### 3. El Coordinador revisa y emite el informe técnico
- **Acción:** Login como coordinador y revisión de solicitudes.
- **Resultado obtenido:** **Pendiente** (depende de la integración del agente y del endpoint de informe).

### 4. Un usuario con otro rol recibe 403 en el panel
- **Acción:** Probar el endpoint sin el rol adecuado.
- **Resultado obtenido:** **Pendiente** (el Gateway debe validar los roles).

## Conclusión
El flujo de autenticación y base de datos quedó funcionando correctamente. Se documentaron y solucionaron los errores 4060 y de contraseña nula. Los pasos 2, 3 y 4 quedan pendientes de la integración completa del agente de IA y la configuración de roles en el Gateway.