# =============================================================================
# Stage 1: сборка
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

# Установка Node.js для сборки SPA-проекта (esproj)
RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Восстановление зависимостей .NET (кэш слоя)
COPY ["OpenBPM.Server/OpenBPM.Server.csproj", "OpenBPM.Server/"]
RUN dotnet restore "OpenBPM.Server/OpenBPM.Server.csproj"

# Установка зависимостей npm (кэш слоя)
COPY ["openbpm.client/package.json", "openbpm.client/package-lock.json*", "openbpm.client/"]
RUN cd openbpm.client && npm ci

# Копирование исходников и публикация
COPY . .
RUN dotnet publish "OpenBPM.Server/OpenBPM.Server.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore

# =============================================================================
# Stage 2: runtime
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Открываем HTTP-порт (рекомендуемый порт ASP.NET Core в контейнерах)
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OpenBPM.Server.dll"]
