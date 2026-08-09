# Clutch server image. Build context is the repository root:
#   docker build -f src/Clutch.Server/Dockerfile -t jchristn77/clutch-server:v0.1.0 .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ ./src/
RUN dotnet publish src/Clutch.Server/Clutch.Server.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# REST + WebSocket port and Prometheus scrape port.
EXPOSE 8080
EXPOSE 9464

ENTRYPOINT ["dotnet", "Clutch.Server.dll"]
