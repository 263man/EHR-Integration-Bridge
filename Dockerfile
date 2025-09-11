# Use the official .NET SDK image as a build environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy only the csproj file and restore dependencies. This is more efficient.
COPY EhrIntegrationBridge/*.csproj ./EhrIntegrationBridge/
RUN dotnet restore "EhrIntegrationBridge/EhrIntegrationBridge.csproj"

# Copy the rest of the application's source code
COPY . .

# Publish the application
RUN dotnet publish "EhrIntegrationBridge/EhrIntegrationBridge.csproj" -c Release -o /app/publish

# Use the official .NET runtime image for the final, smaller image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

# The entrypoint for the container
ENTRYPOINT ["dotnet", "EhrIntegrationBridge.dll"]