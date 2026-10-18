# Etapa 1: Compilar la aplicación React
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

# Etapa 2: Servir los archivos con Nginx
FROM nginx:alpine
# Copiamos los archivos compilados de React a la carpeta pública de Nginx
COPY --from=build /app/dist /usr/share/nginx/html
# Exponemos el puerto 80 (el estándar de Nginx)
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]