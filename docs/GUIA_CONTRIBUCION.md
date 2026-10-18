# Guía de contribución

## Flujo de trabajo
1. **Issue primero.** Antes de empezar una tarea, crea su Issue con la plantilla *Tarea* (`TA-XX.X — nombre`). Asígnatelo y ponle el milestone *Sprint 1*.
2. **Rama.** Sal de `develop` con el nombre `feature/HU-xx-descripcion-corta`.
   ```
   git checkout develop
   git pull
   git checkout -b feature/HU-01-gateway-yarp
   ```
3. **Commits pequeños** con el formato `tipo(HU-xx): descripción (#issue)`.
   Tipos: `feat`, `fix`, `docs`, `chore`, `test`.
   Ejemplo: `feat(HU-01): configura rutas del Gateway (#5)`
4. **Pull Request** hacia `develop`, con la plantilla del repositorio. En la descripción escribe `Closes #número` para cerrar el Issue.
5. **Revisión.** Otro integrante lo revisa y lo aprueba. Nadie aprueba su propio Pull Request.
6. **Unión.** Se une con *Squash and merge* o *Merge*, según acuerde el equipo, y se borra la rama.

## Reglas
- No se sube código directo a `main` ni a `develop`, salvo el commit inicial del repositorio (excepción de arranque).
- `main` solo recibe `develop` al cerrar el sprint, con la etiqueta `v0.1-sprint1`.
- No se sube `.env`, `bin/`, `obj/` ni `node_modules/`.
- Se siguen las [convenciones de nombres](CONVENCIONES.md) y el [contrato de API](API_CONTRATO.md). Si hay que cambiar un endpoint, se cambia primero el contrato y se avisa al equipo.

## Definición de hecho de una tarea
- Compila y funciona al levantar con `docker compose up`.
- Cumple los criterios de aceptación de su historia.
- Tiene Pull Request aprobado y su Issue cerrado.

## Reportar errores
Usa la plantilla *Bug* de los Issues, con los pasos para reproducirlo.
