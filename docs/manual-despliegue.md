# Manual de Despliegue — HomologaUTA

## 1. Requisitos del sistema
- Docker Desktop o Docker Engine (versión 20.10 o superior).
- Docker Compose (versión 2.0 o superior).
- Git.
- Al menos 8 GB de RAM y 20 GB de espacio en disco recomendados.

## 2. Pasos para el despliegue
1. Clonar el repositorio:
   ```bash
   git clone https://github.com/amolina4130/homologa-uta.git
   cd homologa-uta
   ```
2. Crear el archivo de variables de entorno a partir del ejemplo:
   ```bash
   cp .env.example .env
   ```
   (Opcional) Editar el `.env` si se necesita cambiar contraseñas o puertos.
3. Levantar todos los servicios:
   ```bash
   docker compose up -d --build
   ```
4. Verificar que los contenedores estén corriendo:
   ```bash
   docker ps
   ```

## 3. Verificación de salud
- Acceder a http://localhost:3000 para ver el frontend.
- Acceder a http://localhost:3000/estado para ver el estado del sistema.
- Cada microservicio tiene su Swagger en `http://localhost:<puerto>/swagger`.
- El endpoint de health está en `http://localhost:<puerto>/health`.

## 4. Carga de la memoria del agente
La memoria del agente es necesaria para que el análisis funcione.
1. Hacer login como Administrador y obtener el token.
2. Hacer una petición `POST http://localhost:8080/memoria/carga` con el token.
3. Verificar el estado con `GET http://localhost:8080/memoria/estado`.

Para actualizar la memoria, repetir la petición del paso 2. El sistema calcula una huella (hash) y solo actualiza si hay cambios.

## 5. Solución de problemas frecuentes
- **Error de conexión a SQL Server:** esperar a que el contenedor `homologa_sql` esté "healthy" (`docker ps`) antes de que los demás servicios intenten conectarse. Si es necesario, reiniciar los servicios: `docker compose restart`.
- **El frontend no compila:** revisar que no haya errores de sintaxis en los archivos `.jsx` (por ejemplo, falta de `export default`).
- **El agente no responde:** verificar que el contenedor de Ollama esté corriendo y que el modelo esté descargado (`ollama pull llama3` o el que corresponda).
- **Puerto ocupado:** si el puerto 1433 o 3000 está en uso, detener el proceso que lo ocupa o cambiar el mapeo de puertos en `docker-compose.yml`.

## 6. Detener el sistema
```bash
docker compose down
```
Para eliminar también los volúmenes (borrar la base de datos):
```bash
docker compose down -v
```