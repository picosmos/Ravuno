FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
WORKDIR "/src"
RUN dotnet restore "./src/WebAPI/WebAPI.csproj"
RUN dotnet build "./src/WebAPI/WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./src/WebAPI/WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Create directories for persistent data and configuration
RUN mkdir -p /app/data /app/config/updates && chown -R app:app /app/data /app/config

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "WebAPI.dll"]