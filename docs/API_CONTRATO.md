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
Cabecera `Authorization: Bearer <token>` en todo lo que no sea público. El JWT lleva `sub` (id), `correo` y `rol`.

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
| `GET /solicitudes/pendientes` | COORDINADOR | HU-10 | M-08 |
| `GET /solicitudes/{id}/detalle` | COORDINADOR | HU-10 | M-08 |
| `POST /solicitudes/{id}/informe` | COORDINADOR | HU-10 | M-08 |
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