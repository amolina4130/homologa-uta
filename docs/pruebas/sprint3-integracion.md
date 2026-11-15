# Pruebas de Integración — Sprint 3 (HU-12 a HU-23)

## Información del entorno
- **Rama:** `fix/integracion-sprint-3`
- **Fecha de verificación:** 14/11/2026
- **Alcance:** integración de las historias HU-12 a HU-23 en los microservicios
  `autenticacion`, `solicitudes`, `documentos`, `homologacion`, `reglas` y `gateway`,
  más el frontend React.
- **Entorno de compilación:** .NET 8 (SDK 8.0.132), Node/Vite, Docker Compose v5.5.1.

## 1. Compilación de los microservicios

Comando ejecutado por cada proyecto (`dotnet build <proyecto> --nologo -v q`):

| Proyecto | Errores |
|---|---|
| `services/autenticacion/HomologaUTA.Autenticacion.csproj` | 0 |
| `services/documentos/HomologaUTA.Documentos.csproj` | 0 |
| `services/homologacion/HomologaUTA.Homologacion.csproj` | 0 |
| `services/reglas/HomologaUTA.Reglas.csproj` | 0 |
| `services/solicitudes/HomologaUTA.Solicitudes.csproj` | 0 |
| `gateway/HomologaUTA.Gateway.csproj` | 0 |

**Resultado:** todos los proyectos compilan con 0 errores. Solo quedan advertencias
no bloqueantes (nullable en `DocumentosController`, `FrameworkReference` duplicada en
Homologación y un parámetro XML sin documentar).

## 2. Pruebas automatizadas

Comando: `dotnet test <proyecto de pruebas> --nologo`

| Suite | Correctas | Con error | Total |
|---|---:|---:|---:|
| `HomologaUTA.Autenticacion.Tests` | 7 | 0 | 7 |
| `documentos.Tests` | 4 | 0 | 4 |
| `HomologaUTA.Homologacion.Tests` | 53 | 0 | 53 |
| `HomologaUTA.Solicitudes.Tests` | 3 | 0 | 3 |
| **Total** | **67** | **0** | **67** |

Pruebas nuevas relevantes para el Sprint 3:

- `AuthControllerTests` (HU-12): registro válido crea cuenta `ESTUDIANTE` con contraseña
  hasheada, correo duplicado devuelve `409`, contraseña débil y nombre vacío devuelven `400`.
- `AreaAsignaturaTests` (HU-19/HU-21): clasificación por área y conversión de nivel
  textual (`Primero`…`Décimo`, `Nivel 5`, `4`) a número.
- `PdfSimpleWriterTests` (HU-18): PDF válido, paginación, escape de paréntesis y barras,
  documento vacío.
- `ResolucionServiceTests` (HU-17): `409` sin informe, `409` con análisis pendiente,
  registro válido + cambio de estado, `409` duplicado, `400` asignatura ajena,
  `400` sin decisiones y consulta con/sin resolución.
- `ResolucionControllerTests` (HU-17): `403` para `ESTUDIANTE` y `CONSEJO`, `409` para
  `COORDINADOR` sin informe, `404` al consultar una resolución inexistente.
- `SolicitudesControllerTest`: creación de solicitud con catálogo sembrado (carreras y
  asignaturas válidas).

## 3. Frontend

- Comando: `npm run build` en `frontend/`.
- Resultado: **exitoso**, 56 módulos transformados.
- Salida: `dist/assets/index-Bkl38_Os.js` (348.50 kB, gzip 100.28 kB).
- Rutas nuevas verificadas en `App.jsx`: `/registro`, `/equivalencias`, `/memoria`,
  `/resoluciones`, edición de solicitud y formulario de resolución dentro del panel
  del coordinador.

## 4. Docker Compose

- Comando: `docker compose config --quiet`.
- Resultado: **válido**, código de salida `0`. Única advertencia: el atributo `version`
  está obsoleto (no afecta la ejecución).
- `docker compose up --build` **no se pudo ejecutar** en este equipo porque el daemon
  de Docker Desktop no estaba disponible (`npipe:////./pipe/dockerDesktopLinuxEngine`
  no encontrado). Por eso la validación quedó a nivel de configuración y compilación,
  no de arranque real de contenedores.

## 5. Cobertura por historia

| HU | Funcionalidad | Estado |
|---|---|---|
| HU-12 | Registro de cuentas (`POST /auth/registro`), nombre de usuario, validación de contraseña | Implementado y probado |
| HU-13 | Carga y consulta de planes analíticos por asignatura | Implementado |
| HU-14 | Registro y consulta de equivalencias entre carreras | Implementado |
| HU-15 | Campos de módulo y universidad de origen/destino en la solicitud | Implementado |
| HU-16 | Edición de solicitud en estado editable e historial de cambios | Implementado |
| HU-17 | Resolución del Consejo Directivo sobre solicitudes con informe emitido | Implementado y probado |
| HU-18 | Informe técnico descargable en PDF sin dependencias nativas | Implementado y probado |
| HU-19 | Memoria del agente: estado, carga y asignaturas clasificadas por área | Implementado |
| HU-20 | Extracción del récord académico desde documentos (PDF) | Implementado |
| HU-21 | Preselección de asignaturas destino por área y nivel | Implementado |
| HU-22 | Combinación de una o dos materias de origen por materia de destino (interno y externo) | Implementado |
| HU-23 | Flujo completo de análisis y cierre de la solicitud | Implementado |

## 6. Pendientes / observaciones

- **Prueba E2E en navegador:** no realizada porque Docker no estaba operativo en el
  equipo. Queda pendiente repetir el flujo completo (registro → solicitud → documentos →
  análisis → informe → resolución) con `docker compose up --build`.
- **Verificación de migraciones en SQL Server:** las migraciones
  `003_NombreUsuario` (auth), `005_Sprint3CatalogoMemoria` (solicitudes) y
  `003_Sprint3MemoriaResolucion` (homologación) se generaron correctamente, pero su
  aplicación real sobre SQL Server no se pudo confirmar sin contenedores.
- Las advertencias de compilación listadas en la sección 1 no impiden el funcionamiento
  y pueden limpiarse en una tarea posterior.

## Conclusión

La integración del Sprint 3 compila sin errores en todos los microservicios, la suite
automatizada pasa completa (67/67) y el frontend genera el bundle de producción. La
configuración de Docker Compose es válida. La única verificación pendiente es el
arranque real de contenedores y la prueba E2E en navegador, bloqueada por la
indisponibilidad del daemon de Docker en el entorno de prueba.
