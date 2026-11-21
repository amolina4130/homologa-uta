# Pruebas de extremo a extremo del Sprint 3 (TE-05, HU-23)

## Entorno
- Fecha de ejecución:
- Rama / commit probado: `develop` @ <hash>
- Ejecutado por:
- URL del frontend:
- Navegador:

## Datos de prueba
| Rol | Usuario | Notas |
|---|---|---|
| Estudiante | | |
| Coordinador | | |
| Administrador | | |

## Casos

### Flujo del módulo interno (con combinación de dos materias)
| Caso | Paso | Resultado esperado | Resultado obtenido | Estado | Captura |
|---|---|---|---|---|---|
| E2E-01 | Registro de cuenta de Estudiante | Cuenta creada, acceso permitido | | | HU23_caso01.png |
| E2E-02 | Crear solicitud interna | Solicitud en borrador | | | HU23_caso02.png |
| E2E-03 | Adjuntar récord y sílabos | Documentos adjuntos listados | | | HU23_caso03.png |
| E2E-04 | Extracción del récord | Materias aprobadas guardadas | | | HU23_caso04.png |
| E2E-05 | Revisar y corregir materias aprobadas | `PUT` 200, corrección en el historial | | | HU23_caso05.png |
| E2E-06 | Preselección | Pares con candidatas; las sin candidata aparecen como "sin equivalente probable" | | | HU23_caso06.png |
| E2E-07 | Análisis con memoria y sílabos, con **combinación de dos materias** | Resultado con `combinada: true` y dos materias de origen | | | HU23_caso07.png |
| E2E-08 | Revisión del Coordinador | Decisión registrada | | | HU23_caso08.png |
| E2E-09 | Resolución del Consejo | Estado final registrado | | | HU23_caso09.png |
| E2E-10 | Descarga del informe PDF | PDF con materias de origen y decisión | | | HU23_caso10.png |

### Flujo del módulo externo
| Caso | Paso | Resultado esperado | Resultado obtenido | Estado | Captura |
|---|---|---|---|---|---|
| E2E-11 | Crear solicitud externa y adjuntar documentos | Solicitud creada | | | HU23_caso11.png |
| E2E-12 | Extracción y revisión de materias aprobadas | Materias guardadas y corregibles | | | HU23_caso12.png |
| E2E-13 | Preselección y análisis | Resultados por asignatura | | | HU23_caso13.png |
| E2E-14 | Revisión, resolución y PDF | Informe descargado | | | HU23_caso14.png |

### Roles y permisos
| Caso | Verificación | Resultado esperado | Resultado obtenido | Estado | Captura |
|---|---|---|---|---|---|
| E2E-15 | Estudiante intenta corregir materias de otro | 403 | | | HU23_caso15.png |
| E2E-16 | Estudiante corrige con la solicitud en revisión | 409 | | | HU23_caso16.png |
| E2E-17 | Estudiante intenta `POST /memoria/carga` | 403 | | | HU23_caso17.png |
| E2E-18 | Coordinador y Administrador ven solo sus opciones | Menús y acciones según rol | | | HU23_caso18.png |

### La memoria reemplaza a los archivos físicos
| Caso | Verificación | Resultado esperado | Resultado obtenido | Estado | Evidencia |
|---|---|---|---|---|---|
| E2E-19 | `GET /memoria/estado` antes del análisis | Memoria cargada con huella | | | HU23_caso19.png |
| E2E-20 | Análisis con los archivos físicos del catálogo no disponibles | El análisis funciona igual | | | HU23_caso20.png |

## Resumen
- Casos ejecutados: __ / 20
- Aprobados: __ · Fallidos: __
- Defectos encontrados (con enlace a su issue):

## Conclusiones