# Usar la imagen oficial de .NET 8 para compilar
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo del proyecto y restaurar dependencias
COPY *.csproj ./
RUN dotnet restore

# Copiar el resto del código y compilar
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Imagen final para ejecutar
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Exponer el puerto por defecto de .NET (ellos lo mapearán en el compose)
EXPOSE 8080

ENTRYPOINT ["dotnet", "HomologaUTA.Solicitudes.dll"]

# Ignorar la carpeta de pruebas al compilar
RUN rm -rf HomologaUTA.Solicitudes.Tests