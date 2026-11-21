# HomologaUTA — Sistema de Homologación UTA

Proyecto de la Carrera de Software, FISEI, Universidad Técnica de Ambato (ciclo julio – diciembre 2026).

## Equipo
- Lucero Urresta Karol Sulamith (Frontend)
- Molina Alvarado Anahi Dayana (Líder técnico / Backend)
- Moyolema Criollo Katherine Lissette (Backend / DevOps)
- Quilligana Cargua Angela Estefania (Scrum Master / Backend)

Docente: Ing. Hernan Naranjo, Mg

## Sprints
### Sprint 1 (14 al 21 de octubre de 2026)
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

## Cómo levantar el sistema

### Requisitos
- Git
- Docker Desktop (en ejecución)
- Para desarrollar: .NET SDK (la misma versión LTS en todo el equipo) y Node.js LTS

### Pasos
1. Clonar el repositorio:
```
   git clone https://github.com/amolina4130/homologa-uta.git
   cd homologa-uta
   git checkout develop
```
2. Crear el archivo de variables de entorno y cambiar los valores:
```
   cp .env.example .env
```
3. Levantar todo con un solo comando:
```
   docker compose up -d --build
```
4. Abrir la pantalla Estado del sistema: http://localhost:3000/estado

### Puertos
| Componente | Puerto |
|---|---|
| Frontend (React) | 3000 |
| API Gateway (YARP) | 8080 |
| Servicio de Autenticación | 5001 |
| Servicio de Solicitudes | 5002 |
| Servicio de Documentos | 5003 |
| Servicio de Reglas | 5004 |
| Servicio de Homologación | 5005 |
| Servicio de Catálogo | 5006 |
| SQL Server | 1433 |

Cada servicio expone Swagger en `/swagger` y su estado en `/health`.

### Usuarios de prueba
Se precargan con la HU-03: un usuario por rol (por ejemplo `estudiante@uta.edu.ec` y `coordinador@uta.edu.ec`). La contraseña de demostración la define el equipo en el archivo `.env`.

## Memoria del agente (HU-19)
La memoria del agente se guarda en el servicio de Catálogo. 
- **Carga inicial:** se hace con `POST /memoria/carga` (solo Administrador). 
- **Actualización:** se repite la llamada y el sistema detecta si hubo cambios mediante una huella (hash).
- **Consulta:** el servicio de Homologación consulta la memoria mediante `GET /memoria/estado`.

## Flujo de ramas
`main` (versiones estables) ← `develop` (integración) ← `feature/HU-xx-nombre` (una por tarea o historia).
Ver [GUIA_GIT.md](GUIA_GIT.md).

## Documentos del equipo
- [Convenciones de nombres](docs/CONVENCIONES.md)
- [Contrato de API](docs/API_CONTRATO.md)
- [Manual de despliegue](docs/manual-despliegue.md)
- Variables de entorno: copiar `.env.example` a `.env`.