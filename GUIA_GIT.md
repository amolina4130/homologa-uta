# Guía de Git del equipo

## 1. Crear el repositorio (lo hace una sola persona, p. ej. la Scrum Master)
1. En GitHub: **New repository** → nombre `homologa-uta` → vacío (sin README).
2. Invitar a las otras 3 personas en **Settings → Collaborators**.
3. En la carpeta del proyecto:
```
git init
git branch -M main
git add .
git commit -m "chore(repo): estructura inicial y mockups del Sprint 1"
git remote add origin https://github.com/USUARIO/homologa-uta.git
git push -u origin main
git checkout -b develop
git push -u origin develop
```

## 2. Cada integrante (una sola vez)
```
git clone https://github.com/USUARIO/homologa-uta.git
cd homologa-uta
git checkout develop
```

## 3. Trabajar en una tarea
```
git checkout develop
git pull
git checkout -b feature/HU-01-estado-sistema
# ... trabajar ...
git add .
git commit -m "feat(HU-01): pantalla Estado del sistema"
git push -u origin feature/HU-01-estado-sistema
```
Luego abrir un **Pull Request** hacia `develop` en GitHub. Otro integrante lo revisa y lo aprueba (criterio de finalización del sprint).

Nombres de ramas sugeridos:
- `feature/HU-01-docker-gateway`
- `feature/HU-02-login-roles`
- `feature/HU-03-catalogo`
- `feature/HU-04-solicitudes`

## 4. Formato de commits
`tipo(HU-xx): descripción corta`, con tipos `feat`, `fix`, `docs`, `chore`, `test`.

## 5. Cierre del sprint (21/10)
Cuando todo esté en `develop` y la demostración funcione:
```
git checkout main
git merge develop
git tag v0.1-sprint1
git push origin main --tags
```
