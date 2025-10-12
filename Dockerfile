# Use the official .NET SDK image as a build environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy only the new API's csproj file and restore dependencies.
COPY EhrBridge.Api/*.csproj ./EhrBridge.Api/
RUN dotnet restore "EhrBridge.Api/EhrBridge.Api.csproj"

# Copy the rest of the application's source code
COPY . .

# Publish the new API application
RUN dotnet publish "EhrBridge.Api/EhrBridge.Api.csproj" -c Release -o /app/publish

# Use the official ASP.NET runtime image for the final, smaller image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 
WORKDIR /app
# Expose the API port
EXPOSE 8080 
COPY --from=build /app/publish .

# The entrypoint targets the new API's DLL
ENTRYPOINT ["dotnet", "EhrBridge.Api.dll"]
