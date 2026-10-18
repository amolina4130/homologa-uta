# Convenciones de nombres — HomologaUTA

Propuesta del Sprint 1 (tarea TA-01.1). Si el equipo cambia algo, se actualiza este archivo.

## Idioma
- Dominio y rutas en **español** (Solicitud, Carrera, Asignatura, `/solicitudes`).
- Términos técnicos habituales en inglés (Controller, Service, Dto, Gateway).

## Git
| Elemento | Formato | Ejemplo |
|---|---|---|
| Ramas permanentes | `main`, `develop` | |
| Ramas de trabajo | `feature/HU-xx-descripcion-corta` | `feature/HU-02-login-roles` |
| Corrección | `fix/descripcion-corta` | `fix/mensaje-error-login` |
| Commits | `tipo(HU-xx): descripción` | `feat(HU-04): endpoint POST /solicitudes` |
| Tipos de commit | `feat`, `fix`, `docs`, `chore`, `test` | |
| Pull Request | Título `HU-xx: descripción`; hacia `develop`; mínimo 1 aprobación de otro integrante | |
| Etiqueta del sprint | `v0.1-sprint1` | |

## Soluciones y proyectos .NET
| Elemento | Nombre |
|---|---|
| Gateway | `HomologaUTA.Gateway` |
| Servicio de Autenticación | `HomologaUTA.Autenticacion` |
| Servicio de Solicitudes (incluye catálogo) | `HomologaUTA.Solicitudes` |
| Servicio de Documentos | `HomologaUTA.Documentos` |
| Carpetas dentro de cada servicio | `Controllers/`, `Entidades/`, `Dtos/`, `Datos/`, `Servicios/` |
| Pruebas | `HomologaUTA.<Servicio>.Tests` (xUnit) |

## Código C#
- Clases, métodos y propiedades: `PascalCase`. Parámetros y variables locales: `camelCase`. Campos privados: `_camelCase`. Interfaces: `IServicioX`.
- Entidades en singular (`Solicitud`), controladores en plural (`SolicitudesController`).
- DTO con sufijo: `LoginDto`, `SolicitudCrearDto`, `SolicitudVerDto`.

## Base de datos (una por servicio)
| Servicio | Base de datos |
|---|---|
| Autenticación | `HomologaUTA_Auth` |
| Solicitudes | `HomologaUTA_Solicitudes` |
| Documentos | `HomologaUTA_Documentos` |

- Tablas en plural y `PascalCase`: `Usuarios`, `Roles`, `Carreras`, `Asignaturas`, `Solicitudes`.
- Ningún servicio lee las tablas de otro.
- Migraciones de EF Core: `NN_Descripcion` (por ejemplo `001_CrearUsuarios`).

## API
- Rutas en minúsculas y plural: `/solicitudes`, `/carreras`, `/auth/login`.
- JSON en `camelCase`.
- Roles: `ESTUDIANTE`, `COORDINADOR`, `SECRETARIA`, `CONSEJO`, `ADMINISTRADOR`.
- Estados de solicitud: `REGISTRADA` (Sprint 1); los demás llegan en el Sprint 2.
- Tipos de solicitud: `CAMBIO_CARRERA`, `CAMBIO_UNIVERSIDAD`, `MOVILIDAD`.

## Frontend (React + Vite)
| Elemento | Formato | Ejemplo |
|---|---|---|
| Componentes y páginas | `PascalCase.jsx` | `EstadoSistema.jsx`, `Login.jsx` |
| Carpetas | `src/pages`, `src/components`, `src/services` | |
| Funciones y variables | `camelCase` | `obtenerCarreras()` |
| Llamadas a la API | un archivo por servicio en `src/services` | `src/services/auth.js` |
| Rutas del navegador | las del mapa de pantallas | `/estado`, `/login`, `/carreras`, `/solicitudes/nueva` |

## Docker
- Contenedores: `homologa-<componente>` (`homologa-gateway`, `homologa-auth`, `homologa-solicitudes`, `homologa-documentos`, `homologa-sqlserver`, `homologa-frontend`).
- Puertos: Frontend 3000, Gateway 8080, Autenticación 5001, Solicitudes 5002, Documentos 5003, SQL Server 1433.
- Secretos en `.env` (no se sube). Se sube `.env.example` con valores de ejemplo.

## Tareas y mockups
- Historias `HU-01`, tareas `TA-01.1`, mockups `M-01`, `M-02`, `M-03`, `M-04`.
- Archivos de mockup: `M-0X_nombre-en-minusculas.png`.
