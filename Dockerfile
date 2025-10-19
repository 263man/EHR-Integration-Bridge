# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file and project files first for dependency caching
# This ensures a faster build when only code changes.
COPY EhrIntegrationBridge.sln .
COPY EhrBridge.Api/*.csproj EhrBridge.Api/
# Since the PatientDataGenerator uses external packages, we copy the project file
COPY EhrBridge.Api/DataGeneration/PatientDataGenerator.cs EhrBridge.Api/DataGeneration/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code (especially services, controllers, data models, and configurations)
COPY . .

# Build and publish API only
# The output path needs to be adjusted to reflect the project name if needed.
# We publish directly into a single folder for the API.
RUN dotnet publish "EhrBridge.Api/EhrBridge.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy the published API artifacts from the build stage
COPY --from=build /app/publish .

# 🛑 FIX: Explicitly copy the configuration file from the build stage's source 
# directory to the runtime directory. This guarantees the API can find the
# "EhrDatabase" connection string and fixes the startup crash.
COPY --from=build /src/EhrBridge.Api/appsettings.json .
COPY --from=build /src/EhrBridge.Api/appsettings.Development.json .

# Set the entry point. The 'command' in docker-compose.yml 
# will execute the DLL from the root of this WORKDIR.