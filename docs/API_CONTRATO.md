# Contrato de API — Sprint 1

Propuesta para confirmar en la tarea TA-01.1. Todo pasa por el **API Gateway** (`http://localhost:8080`), que valida el JWT y enruta por prefijo.

| Prefijo en el Gateway | Servicio |
|---|---|
| `/health` | Gateway (consulta a los demás) |
| `/auth` | Autenticación |
| `/carreras` | Solicitudes (catálogo) |
| `/solicitudes` | Solicitudes |
| `/homologacion` | Homologación (validación de reglas) |

## Resumen
| Método y ruta | Acceso | Historia | Pantalla |
|---|---|---|---|
| `GET /health` | Público | HU-01 | M-01 |
| `POST /auth/login` | Público | HU-02 | M-02 |
| `GET /carreras` | Autenticado | HU-03 | M-03, M-04 |
| `GET /carreras/{id}/asignaturas` | Autenticado | HU-03 | M-03, M-04 |
| `POST /solicitudes` | ESTUDIANTE | HU-04 | M-04 |
| `GET /solicitudes/mias` | ESTUDIANTE | HU-04 | Pestaña Mis solicitudes |

Los endpoints de documentos (`POST` y `GET /solicitudes/{id}/documentos`) pasan al Sprint 2.

## Autenticación
Cabecera `Authorization: Bearer <token>` en todo lo que no sea público. El JWT lleva `sub` (id), `email` y `rol`.

| Código | Cuándo |
|---|---|
| 401 | Sin token, token inválido o vencido |
| 403 | Token válido pero el rol no tiene permiso |

## GET /health
Respuesta `200`:
```json
{
  "componentes": [
    { "nombre": "API Gateway (YARP)", "puerto": 8080, "estado": "EN_LINEA", "respuestaMs": 12 },
    { "nombre": "Servicio de Autenticación", "puerto": 5001, "estado": "EN_LINEA", "respuestaMs": 15 },
    { "nombre": "Servicio de Solicitudes", "puerto": 5002, "estado": "SIN_RESPUESTA", "respuestaMs": null }
  ]
}
```
`estado`: `EN_LINEA` o `SIN_RESPUESTA`. RabbitMQ y Ollama no se consultan; la pantalla los muestra fijos como "Sprint 2".

## POST /auth/login
Petición:
```json
{ "correo": "estudiante@uta.edu.ec", "contrasena": "..." }
```
`200`:
```json
{ "token": "eyJ...", "correo": "estudiante@uta.edu.ec", "rol": "ESTUDIANTE" }
```
`401` (mismo mensaje para cualquier credencial incorrecta):
```json
{ "mensaje": "Credenciales incorrectas. Intente nuevamente" }
```

## GET /carreras
`200`:
```json
[ { "id": 1, "nombre": "Ingeniería en Software" } ]
```
Devuelve las 5 carreras de la FISEI.

## GET /carreras/{id}/asignaturas
`200`:
```json
[ { "id": 10, "nivel": "Primero", "nombre": "Fundamentos de Programación", "creditos": 4, "horas": 144 } ]
```
`404` si la carrera no existe.

## POST /solicitudes
Petición:
```json
{
  "tipo": "CAMBIO_CARRERA",
  "modulo": "INTERNO",
  "carreraOrigenId": 1,
  "carreraDestinoId": 3,
  "asignaturaIds": [10, 11]
}
```
`201`:
```json
{ "id": 3, "estado": "REGISTRADA" }
```
`400` (validaciones, el mensaje se muestra junto a cada campo):
```json
{ "errores": { "tipo": "Seleccione un tipo de solicitud", "carreraDestinoId": "El destino debe ser distinto del origen" } }
```
Reglas: todos los campos son obligatorios, al menos una asignatura, origen distinto de destino. El estudiante se toma del JWT.

## GET /solicitudes/mias
`200`, solo las solicitudes del estudiante autenticado:
```json
[ { "id": 3, "numero": "0003", "fecha": "2026-10-20", "tipo": "CAMBIO_CARRERA", "origen": "Software", "destino": "Industrial", "estado": "REGISTRADA" } ]
```

## Endpoints del Sprint 2 (Coordinador)

| Método y ruta | Acceso | Historia | Pantalla |
|---|---|---|---|
| `GET /homologacion/coordinador/pendientes` | COORDINADOR | HU-10 | M-08 |
| `GET /homologacion/coordinador/{id}/detalle` | COORDINADOR | HU-10 | M-08 |
| `POST /homologacion/coordinador/{id}/informe` | COORDINADOR | HU-10 | M-08 |
| `POST /homologacion/{id}/validar` | Autenticado | HU-07 | — |

## POST /homologacion/{id}/validar
Ejecuta las reglas objetivas (RN-01, RN-02, RN-04, RN-05 y RN-06) sobre la solicitud `{id}` y devuelve el resultado por asignatura.

Petición:
```json
{
  "tipo": "CAMBIO_UNIVERSIDAD",
  "esUniversidadExtranjera": false,
  "requisitos": { "tieneCambiosCarreraPrevios": false, "periodosCursados": 4 },
  "documentacion": { "tieneMallaOrigen": true, "tieneHistorialAcademico": true, "tieneSilabos": true, "documentosApostillados": false },
  "asignaturas": [
    { "id": 10, "nombre": "Cálculo I", "aprobada": true, "aniosDesdeAprobacion": 2, "numeroMatricula": 1 },
    { "id": 11, "nombre": "Física I", "aprobada": true, "aniosDesdeAprobacion": 12, "numeroMatricula": 1 }
  ]
}
```
`requisitos` es obligatorio solo para cambio de universidad (RN-05).

`200`:
```json
{
  "solicitudId": 1,
  "tipo": "CAMBIO_UNIVERSIDAD",
  "resumen": { "total": 2, "listasParaAgente": 1, "derivadas": 1, "excluidas": 0, "conFaltantes": 0 },
  "validacionesGenerales": [ { "regla": "RN-06", "cumple": true, "detalle": "..." } ],
  "asignaturas": [
    { "id": 10, "asignatura": "Cálculo I", "estado": "LISTA_PARA_AGENTE", "reglaAplicada": "RN-03", "detalle": "..." },
    { "id": 11, "asignatura": "Física I", "estado": "DERIVADA", "reglaAplicada": "RN-01", "detalle": "..." }
  ]
}
```
Estados: `LISTA_PARA_AGENTE`, `DERIVADA` (RN-01), `EXCLUIDA` (RN-02, RN-04 o RN-05), `CON_FALTANTES` (RN-06). `400` con `{ "error": "..." }` si los datos no son válidos.
## Endpoints del Coordinador (HU-10)

Pasan por el Gateway bajo `/homologacion/coordinador` y solo los puede usar el rol COORDINADOR (`401` sin token, `403` con otro rol). La fecha, el estudiante, el tipo y las carreras los toma Homologación del servicio de Solicitudes mediante una ruta interna (`/internal/solicitudes`) que el Gateway no expone.

### GET /homologacion/coordinador/pendientes
Solicitudes en `PENDIENTE_REVISION`, las más antiguas primero.

`200`:
```json
[
  {
    "solicitudId": 2,
    "fecha": "2026-11-02T10:15:00",
    "estudiante": "estudiante@uta.edu.ec",
    "tipo": "Homologación",
    "carreraOrigen": "Ingeniería en Software",
    "carreraDestino": "Ingeniería Industrial",
    "fechaAnalisis": "2026-11-03T04:12:01Z",
    "totalAsignaturas": 2
  }
]
```
`502` si el servicio de Solicitudes no responde.

### GET /homologacion/coordinador/{id}/detalle
Detalle de la solicitud con la propuesta del agente por asignatura. El `id` de cada asignatura es el que se usa al emitir el informe.

`200`:
```json
{
  "solicitudId": 2,
  "estado": "PENDIENTE_REVISION",
  "fecha": "2026-11-02T10:15:00",
  "estudiante": "estudiante@uta.edu.ec",
  "tipo": "Homologación",
  "carreraOrigen": "Ingeniería en Software",
  "carreraDestino": "Ingeniería Industrial",
  "fechaAnalisis": "2026-11-03T04:12:01Z",
  "coordinadorCorreo": null,
  "fechaInforme": null,
  "asignaturas": [
    {
      "id": 5,
      "asignaturaOrigen": "Programación I",
      "asignaturaDestino": "Fundamentos de Programación",
      "propuesta": {
        "decision": "HOMOLOGABLE",
        "justificacion": "Los contenidos son equivalentes.",
        "porcentajeContenido": 90,
        "porcentajeProfundidad": 85,
        "porcentajeHoras": 88,
        "requiereRevision": false
      },
      "decisionCoordinador": null,
      "observacionCoordinador": null
    }
  ]
}
```
`404` si la solicitud no tiene análisis. `502` si el servicio de Solicitudes no responde.

### POST /homologacion/coordinador/{id}/informe
Emite el informe técnico. Debe traer **todas** las asignaturas del análisis, cada una con su decisión (`HOMOLOGABLE` o `NO_HOMOLOGABLE`) y una observación obligatoria (máx. 1000 caracteres). El Coordinador se toma del token.

Petición:
```json
{
  "asignaturas": [
    { "asignaturaId": 5, "decision": "HOMOLOGABLE", "observacion": "Cumple con el contenido y la carga horaria." }
  ]
}
```
`200`:
```json
{
  "solicitudId": 2,
  "estado": "INFORME_EMITIDO",
  "coordinadorCorreo": "coordinador@uta.edu.ec",
  "fechaInforme": "2026-11-03T05:30:00Z",
  "asignaturas": [
    { "asignaturaId": 5, "asignaturaOrigen": "Programación I", "decision": "HOMOLOGABLE", "observacion": "Cumple con el contenido y la carga horaria." }
  ]
}
```

| Código | Cuándo |
|---|---|
| 400 | Falta alguna asignatura, hay repetidas o ajenas a la solicitud, decisión inválida u observación vacía |
| 401 | No se pudo identificar al Coordinador en el token |
| 404 | La solicitud no tiene análisis |
| 409 | El informe ya fue emitido |

Al guardarse, el análisis pasa a `INFORME_EMITIDO` y sale de la lista de pendientes.
