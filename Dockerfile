# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

COPY *.sln .
COPY EhrIntegrationBridge/*.csproj ./EhrIntegrationBridge/
RUN dotnet restore

COPY . .
WORKDIR /source/EhrIntegrationBridge
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Create the final, lightweight image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EhrIntegrationBridge.dll"]