# Planificación del Sprint 1

Resumen de la planificación. El detalle completo (fichas de historia, mockups y tareas) está en el documento Word de planificación entregado al docente.

## Datos generales
| Parámetro | Detalle |
|---|---|
| Período | 14 al 21 de octubre de 2026 (días hábiles: 14, 15, 16, 19, 20 y 21) |
| Historias | HU-01, HU-02, HU-03 y HU-04 |
| Story Points | 22 SP (1 SP ≈ 1,5 horas) |
| Horas planificadas | 33,5 h de 43,2 h netas disponibles |
| Tareas | 22, repartidas entre 4 integrantes |
| Revisión del Sprint | Miércoles 21 de octubre de 2026, con demostración |

## Objetivo
Entregar un incremento que se ejecute con un solo comando y permita iniciar sesión con roles, consultar las carreras de la FISEI y registrar una solicitud de homologación, con un mockup por historia.

## Historias y mockups
| Historia | SP | Horas | Mockup |
|---|---|---|---|
| HU-01 Levantar el sistema con un solo comando | 5 | 8 h | M-01 Estado del sistema |
| HU-02 Iniciar sesión con credenciales institucionales y roles | 6 | 9 h | M-02 Inicio de sesión |
| HU-03 Carreras, mallas y usuarios de prueba precargados | 4 | 6 h | M-03 Carreras y mallas |
| HU-04 Registrar una solicitud de homologación | 7 | 10,5 h | M-04 Nueva solicitud |

## Tareas
| Tarea | Nombre | Responsable | Horas | Fechas |
|---|---|---|---|---|
| TA-01.1 | Estructura de la solución y el repositorio | Anahi | 1 | 14/10 |
| TA-01.2 | Dockerfile y docker-compose | Katherine | 2 | 14/10 |
| TA-01.3 | API Gateway (YARP) | Anahi | 1,5 | 14 al 15/10 |
| TA-01.4 | Servicios base con Swagger y /health | Estefania | 2 | 14/10 |
| TA-01.5 | Mockup M-01 y pantalla Estado del sistema | Karol | 1,5 | 14/10 |
| TA-02.1 | Modelo de usuarios y roles | Estefania | 1 | 15/10 |
| TA-02.2 | Endpoint POST /auth/login | Estefania | 2 | 15 al 16/10 |
| TA-02.3 | Validar JWT y rol en el Gateway | Anahi | 1 | 15/10 |
| TA-02.4 | Mockup M-02 (Inicio de sesión) | Karol | 1 | 14 al 15/10 |
| TA-02.5 | Pantalla de inicio de sesión | Karol | 2 | 15 al 16/10 |
| TA-02.6 | Menú y rutas protegidas según el rol | Karol | 1 | 16/10 |
| TA-02.7 | Pruebas del inicio de sesión y del acceso por rol | Katherine | 1 | 16/10 |
| TA-03.1 | Tablas Carrera y Asignatura | Estefania | 1 | 16/10 |
| TA-03.2 | Datos semilla de las 5 carreras | Katherine | 2 | 19/10 |
| TA-03.3 | Usuarios de prueba por rol | Estefania | 0,5 | 19/10 |
| TA-03.4 | Endpoints del catálogo | Estefania | 1 | 19/10 |
| TA-03.5 | Mockup M-03 y pantalla Carreras y mallas | Estefania | 1,5 | 19 al 20/10 |
| TA-04.1 | Mockup M-04 (Nueva solicitud) | Anahi | 1 | 16/10 |
| TA-04.2 | Modelo, estados y endpoints de solicitudes | Anahi | 4,5 | 15 al 20/10 |
| TA-04.3 | Formulario Nueva solicitud | Karol | 2,5 | 16 al 19/10 |
| TA-04.4 | Pestaña Mis solicitudes | Katherine | 1,5 | 20/10 |
| TA-04.5 | Pruebas del registro de solicitudes | Estefania | 1 | 20/10 |

## Capacidad por integrante
| Integrante | Rol | Horas planificadas |
|---|---|---|
| Estefania Quilligana | Scrum Master / Backend | 10 h |
| Anahi Molina | Líder técnico / Backend | 9 h |
| Karol Lucero | Frontend | 8 h |
| Katherine Moyolema | Backend / DevOps | 6,5 h |

## Fuera del Sprint 1 (pasa al Sprint 2)
- Carga de documentos de respaldo en PDF (HU-05).
- Catálogo de reglas normativas y validación automática.
- Agente de IA (Ollama), RabbitMQ y conector del agente externo.
- Panel del Coordinador, informe técnico y resolución del Consejo.

## Riesgos principales
- Tiempo limitado (6 días hábiles, 2 h diarias por persona): priorizar login y solicitud.
- Docker o SQL Server sin levantar en algún equipo: probar el compose desde el inicio.
- Integración tardía entre Gateway, JWT y servicios: acordar la clave y los claims desde el inicio.
- Mallas de las carreras incompletas: precargar las disponibles y marcar el resto como ejemplo.

## Criterios de finalización
- Las 4 historias cumplen sus criterios de aceptación y están integradas en `develop`.
- El sistema se levanta con `docker compose up` y la demostración se ejecuta sin errores.
- Cada pantalla coincide con su mockup (M-01 a M-04).
- Cada historia fue revisada por otro integrante (Pull Request aprobado).
