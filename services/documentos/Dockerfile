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
# pdftotext (poppler) conserva las columnas de los planes analíticos al leerlos.
RUN apt-get update && apt-get install -y --no-install-recommends poppler-utils && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "HomologaUTA.Documentos.dll"]