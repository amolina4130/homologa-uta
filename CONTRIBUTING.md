# Guía de contribución — HomologaUTA

Reglas para trabajar en equipo en este repositorio. Aplican a las 4 personas del proyecto.
Los detalles de nombres están en [`docs/CONVENCIONES.md`](docs/CONVENCIONES.md), el contrato de API en [`docs/API_CONTRATO.md`](docs/API_CONTRATO.md) y los comandos de Git en [`GUIA_GIT.md`](GUIA_GIT.md).

## 1. Reglas de oro
1. **Nadie sube directo a `main` ni a `develop`.** Todo cambio entra por Pull Request.
2. **Una rama por tarea o historia**, creada desde `develop`.
3. **Todo Pull Request lo revisa y aprueba otra persona**, nunca quien lo escribió.
4. **Antes de empezar, `git pull` en `develop`.** Antes de pedir revisión, `git pull` otra vez para traer lo que subieron los demás.
5. **Nunca se sube información sensible:** contraseñas, claves JWT ni el archivo `.env`. Solo se sube `.env.example`.
6. **Si algo cambia en el contrato de API o en las convenciones, se avisa al grupo y se actualiza el archivo en el mismo Pull Request.**

## 2. Ramas

| Rama | Para qué sirve | Quién escribe en ella |
|---|---|---|
| `main` | Versiones estables y presentables | Solo por Pull Request desde `develop`, al cerrar el sprint |
| `develop` | Integración del trabajo de todos | Solo por Pull Request desde las ramas de trabajo |
| `feature/HU-xx-descripcion` | Una tarea o historia nueva | La persona responsable |
| `fix/descripcion` | Corregir un error | La persona que lo corrige |

Ejemplos: `feature/HU-02-login-roles`, `feature/HU-04-solicitudes`, `fix/mensaje-error-login`.

Reglas de nombre: minúsculas, palabras separadas por guion, sin tildes ni espacios.
## 2.1 Tablero de tareas

El seguimiento se hace en **GitHub Projects** (o Trello). Columnas:

| Columna | Qué contiene |
|---|---|
| Backlog | Todas las tareas del proyecto (PB-01 a PB-16) |
| To Do | Tareas del Sprint actual (TA-xx.x) |
| In Progress | Tarea que alguien está haciendo ahora |
| Review | Tarea con Pull Request abierto, esperando revisión |
| Done | Tarea aprobada y en `develop` |

**Regla:** cada tarea del Sprint Backlog (TA-01.1, TA-01.2, etc.) debe estar en el tablero y moverse según avanza. No se considera terminada hasta que está en `Done`.

## 3. Mensajes de commit

Formato:

```
tipo(ámbito): descripción corta
```

### Tipos
| Tipo | Cuándo se usa |
|---|---|
| `feat` | Una funcionalidad nueva |
| `fix` | La corrección de un error |
| `docs` | Cambios solo en documentación |
| `chore` | Tareas de configuración o estructura que no cambian la lógica (carpetas, `.gitignore`, Docker) |
| `test` | Agregar o corregir pruebas |
| `refactor` | Reordenar código sin cambiar lo que hace |
| `style` | Formato (espacios, comas), sin cambiar la lógica |

### Ámbito
Lo que se tocó. Puede ser:
- **El número de la historia:** `HU-01`, `HU-02`, `HU-03`, `HU-04`.
- **La parte del proyecto:** `repo`, `docs`, `gateway`, `docker`, `frontend`, `auth`, `solicitudes`.

### Descripción
- En español, en minúscula, en infinitivo o en presente, sin punto final.
- Corta (idealmente menos de 70 caracteres) y concreta: qué se hizo.

### Ejemplos correctos
```
feat(HU-02): endpoint POST /auth/login
feat(HU-04): formulario de nueva solicitud
fix(HU-02): corregir mensaje de error del login
test(HU-04): pruebas de validación de solicitudes
docs(repo): agregar contrato de API
chore(docker): agregar docker-compose con SQL Server
refactor(gateway): separar la configuración de rutas
```

### Ejemplos incorrectos
```
cambios                          (no dice tipo ni qué se hizo)
Update login.cs                  (formato automático de GitHub)
feat: arreglé cosas varias       (falta el ámbito y es vago)
FIX(HU-02): Corregí el Login.    (mayúsculas y punto final)
```
## 3.1 Mockups

- Se guardan en `docs/mockups/` como **PNG**.
- Nomenclatura: `M-0X-nombre.png` (ej: `M-01-estado-sistema.png`, `M-02-login.png`).
- Cada mockup se **valida con el equipo antes** de programar su pantalla.
- Los mockups del Sprint 1 son:
  - M-01 — Estado del sistema (Karol)
  - M-02 — Inicio de sesión (Karol)
  - M-03 — Carreras y mallas (Estefania)
  - M-04 — Nueva solicitud (Anahi)

## 4. Pull Requests

### Cómo abrirlo
1. Subir la rama: `git push -u origin feature/HU-xx-descripcion`.
2. En GitHub, **New pull request**: base `develop` ← comparar con tu rama.
3. **Título:** `HU-xx: descripción` (por ejemplo `HU-02: login con roles`).
4. En la descripción, indicar: qué se hizo, qué tarea cierra (`TA-xx.x`) y cómo probarlo.
5. Pedir revisión a otro integrante en **Reviewers**.

### Lista antes de pedir revisión
- [ ] El proyecto compila y levanta sin errores.
- [ ] Probé lo que hice (en Swagger o en el navegador).
- [ ] No subí `.env`, contraseñas, `bin/`, `obj/` ni `node_modules/`.
- [ ] Respeté las convenciones de nombres.
- [ ] Si cambié algo del contrato de API, actualicé `docs/API_CONTRATO.md`.
- [ ] Hice `git pull` de `develop` y resolví los conflictos.

### Cómo se revisa
- Quien revisa lee el código, lo prueba y comenta lo que corregir.
- Se aprueba solo si cumple la lista de arriba y los criterios de aceptación de la historia.
- Quien escribió el cambio **no** aprueba su propio Pull Request.
- Después de aprobado, se hace **Merge** a `develop` y se borra la rama.

## 5. Estructura del repositorio
```
gateway/                  API Gateway (YARP)
services/autenticacion/   Servicio de Autenticación
services/solicitudes/     Servicio de Solicitudes y catálogo
services/documentos/      Servicio de Documentos
frontend/                 Aplicación React (Vite)
docs/                     Planificación, mockups, convenciones y contrato de API
docs/mockups/             Mockups M-01 a M-04
.env.example              Plantilla de variables de entorno
.gitignore                Archivos que no se suben
docker-compose.yml        Levanta todo el sistema
```

Cada persona trabaja dentro de la carpeta de su tarea y evita modificar lo de otros sin avisar.

## 6. Resumen de convenciones de código

| Parte | Regla |
|---|---|
| Proyectos .NET | HomologaUTA.<Servicio> (ej: HomologaUTA.Autenticacion, HomologaUTA.Solicitudes, HomologaUTA.Documentos) |
| Carpetas del repo | minúsculas y sin tildes (autenticacion, solicitudes, documentos) |
| Clases, métodos y propiedades C# | `PascalCase` |
| Variables y parámetros C# | `camelCase` |
| Campos privados C# | `_camelCase` |
| Bases de datos | `HomologaUTA_Auth`, `HomologaUTA_Solicitudes`, `HomologaUTA_Documentos` |
| Tablas | Plural y `PascalCase` (`Usuarios`, `Carreras`) |
| Rutas de la API | Minúsculas y plural (`/solicitudes`) |
| JSON | `camelCase` |
| Componentes React | `PascalCase.jsx` |
| Funciones y variables JS | `camelCase` |

## 7. Reglas de la API
- Todo el frontend habla **solo con el Gateway** (`http://localhost:8080`), nunca con un servicio directo.
- Los endpoints, las peticiones y las respuestas siguen el contrato de `docs/API_CONTRATO.md`.
- Errores: `401` sin token o token inválido, `403` sin permiso para el rol, `400` con validaciones, `404` si no existe.

## 8. Seguridad
- Las contraseñas se guardan con hash, nunca en texto plano.
- Las claves y contraseñas viven en el `.env`, que está en `.gitignore`.
- Si se sube un secreto por error, **avisar de inmediato al equipo y cambiarlo**; borrarlo del archivo no basta, porque queda en el historial de Git.

## 9. Definición de hecho (una tarea está terminada cuando…)
- Funciona y cumple los criterios de aceptación de su historia.
- Se probó:
  - Backend: pruebas xUnit pasando y endpoints verificados en Swagger.
  - API: colección de Postman actualizada y probada (si aplica).
  - Frontend: probado en el navegador contra el Gateway.
- Su Pull Request fue revisado y aprobado por otra persona, y está en `develop`.
- La tarea está cerrada en el tablero (columna Done).

## 10. Cierre del sprint
Cuando todo esté en `develop` y la demostración funcione, Anahi, como líder técnico:
```
git checkout develop
git pull origin develop
git checkout main
git pull origin main
git merge develop
git tag v0.1-sprint1
git push origin main --tags
git checkout develop
```

## 11. Dudas y conflictos
- Si dos personas tocaron el mismo archivo y Git muestra un conflicto, resolverlo **juntas** y no borrar el trabajo de la otra.
- Si no estás seguro de algo, pregunta en el grupo antes de hacer el commit.
