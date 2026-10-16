# HomologaUTA — Sistema de Homologación UTA

Proyecto de la Carrera de Software, FISEI, Universidad Técnica de Ambato (ciclo julio – diciembre 2026).

## Equipo
- Lucero Urresta Karol Sulamith (Frontend)
- Molina Alvarado Anahi Dayana (Líder técnico / Backend)
- Moyolema Criollo Katherine Lissette (Backend / DevOps)
- Quilligana Cargua Angela Estefania (Scrum Master / Backend)

Docente: Ing. Hernan Naranjo, Mg

## Sprint 1 (14 al 21 de octubre de 2026)
| Historia | Mockup |
|---|---|
| HU-01 Levantar el sistema con un solo comando | M-01 Estado del sistema |
| HU-02 Iniciar sesión con credenciales institucionales y roles | M-02 Inicio de sesión |
| HU-03 Carreras, mallas y usuarios de prueba precargados | M-03 Carreras y mallas |
| HU-04 Registrar una solicitud de homologación | M-04 Nueva solicitud |

Los mockups están en [`docs/mockups`](docs/mockups).

## Estructura
```
gateway/                  API Gateway (YARP)
services/autenticacion/   Servicio de Autenticación
services/solicitudes/     Servicio de Solicitudes y catálogo
services/documentos/      Servicio de Documentos
frontend/                 React
docs/                     Planificación y mockups
```

## Cómo ejecutar (se completa en la HU-01)
```
docker compose up
```

## Flujo de ramas
`main` (versiones estables) ← `develop` (integración) ← `feature/HU-xx-nombre` (una por tarea o historia).
Ver [GUIA_GIT.md](GUIA_GIT.md).

## Documentos del equipo
- [Convenciones de nombres](docs/CONVENCIONES.md)
- [Contrato de API del Sprint 1](docs/API_CONTRATO.md)
- Variables de entorno: copiar `.env.example` a `.env`.
