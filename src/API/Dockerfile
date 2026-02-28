# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/API/StorageService.Api.csproj", "src/API/"]
COPY ["src/Application/StorageService.Application.csproj", "src/Application/"]
COPY ["src/Domain/StorageService.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/StorageService.Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "./src/API/StorageService.Api.csproj"
COPY . .
WORKDIR "src/API"
RUN dotnet build "./StorageService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./StorageService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# --- minimal additions: copy the error catalog and set its path ---
COPY /error-codes/errors.json /app/errors.json
# ------------------------------------------------------------------

ENTRYPOINT ["dotnet", "StorageService.Api.dll"]