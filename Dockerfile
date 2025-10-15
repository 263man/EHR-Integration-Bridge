# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all files
COPY . ./

# Restore dependencies
RUN dotnet restore "EhrIntegrationBridge.sln"

# Build and publish API
RUN dotnet publish "EhrBridge.Api/EhrBridge.Api.csproj" -c Release -o /app/api

# Build and publish Worker
RUN dotnet publish "EhrIntegrationBridge/EhrIntegrationBridge.csproj" -c Release -o /app/worker

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy build outputs
COPY --from=build /app/api ./api
COPY --from=build /app/worker ./worker

# No default ENTRYPOINT; docker-compose will set command per service
