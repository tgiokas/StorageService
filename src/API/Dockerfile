# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/API/Storage.Api.csproj", "src/API/"]
COPY ["src/Application/Storage.Application.csproj", "src/Application/"]
COPY ["src/Domain/Storage.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Storage.Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "./src/API/Storage.Api.csproj"
COPY . .
WORKDIR "/src/API"
RUN dotnet build "./Storage.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Storage.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# --- minimal additions: copy the error catalog and set its path ---
COPY ../../error-codes/errors.json /app/errors.json
# ------------------------------------------------------------------

ENTRYPOINT ["dotnet", "Storage.Api.dll"]
