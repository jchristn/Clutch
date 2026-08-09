# Clutch server image. Build context is the repository root:
#   docker build -f src/Clutch.Server/Dockerfile -t jchristn77/clutch-server:v0.1.0 .
# The projects multi-target net8.0;net10.0, so the build uses the .NET 10 SDK
# (which can compile both) and pins the published output to net8.0 to match the
# net8.0 runtime base image below.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ ./src/
RUN dotnet publish src/Clutch.Server/Clutch.Server.csproj -c Release -f net8.0 -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# REST + WebSocket port, Prometheus scrape port, and native MCP server port.
EXPOSE 8080
EXPOSE 9464
EXPOSE 8100

ENTRYPOINT ["dotnet", "Clutch.Server.dll"]
