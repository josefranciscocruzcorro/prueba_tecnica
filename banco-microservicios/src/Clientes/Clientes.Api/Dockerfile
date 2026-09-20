# syntax=docker/dockerfile:1
#
# Imagen del microservicio de Clientes. El contexto de construcción es la raíz del repositorio,
# porque el proyecto depende de los bloques compartidos que viven fuera de su carpeta.

# ── Compilación ────────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /origen

# Primero solo los manifiestos: mientras no cambien las dependencias, esta capa se reutiliza y
# la restauración de paquetes no se repite en cada compilación.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/Shared.Kernel/*.csproj          src/BuildingBlocks/Shared.Kernel/
COPY src/BuildingBlocks/Shared.Contracts/*.csproj       src/BuildingBlocks/Shared.Contracts/
COPY src/BuildingBlocks/Shared.Api/*.csproj             src/BuildingBlocks/Shared.Api/
COPY src/BuildingBlocks/Shared.Infrastructure/*.csproj  src/BuildingBlocks/Shared.Infrastructure/
COPY src/Clientes/Clientes.Domain/*.csproj              src/Clientes/Clientes.Domain/
COPY src/Clientes/Clientes.Application/*.csproj         src/Clientes/Clientes.Application/
COPY src/Clientes/Clientes.Infrastructure/*.csproj      src/Clientes/Clientes.Infrastructure/
COPY src/Clientes/Clientes.Api/*.csproj                 src/Clientes/Clientes.Api/

RUN dotnet restore src/Clientes/Clientes.Api/Clientes.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Clientes/       src/Clientes/

RUN dotnet publish src/Clientes/Clientes.Api/Clientes.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /publicacion

# ── Ejecución ──────────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ejecucion
WORKDIR /app

# curl es la única adición a la imagen base: la necesita la sonda de salud de Docker.
RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=compilacion /publicacion .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080

# El contenedor no corre como root: $APP_UID viene definido en la imagen base de .NET.
USER $APP_UID

HEALTHCHECK --interval=15s --timeout=5s --start-period=45s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/salud/listo || exit 1

ENTRYPOINT ["dotnet", "Clientes.Api.dll"]
